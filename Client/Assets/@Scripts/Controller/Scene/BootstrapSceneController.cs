using System;
using System.Threading;
using UnityEngine;

public class BootstrapSceneController : MonoBehaviour
{
    #region Variable
    [Header("UI Variable")]
    [SerializeField] private UI_ProgressBar progressBar;

    [Header("Setting")]
    [SerializeField] private float minSceneTransitionDelaySeconds = 0.5f;
    [SerializeField] private float maxSceneTransitionDelaySeconds = 1.0f;

    private const string mainSceneTag = "main";

    private float currentProgressValue;
    private CancellationTokenSource lifetimeCts;
    #endregion

    private class LoadSceneManage : ISequenceStep
    {
        private readonly Action<string> onStatusChanged;

        public LoadSceneManage(Action<string> _onStatusChanged)
        {
            onStatusChanged = _onStatusChanged;
        }

        public async Awaitable<bool> Execute(CancellationToken _cancellationToken)
        {
            onStatusChanged?.Invoke("씬 데이터 로딩 중...");

            if (LoadSceneController.Instance == null)
            {
                DebugLogController.GenerateErrorMessage<BootstrapSceneController>("LoadSceneController.Instance가 null입니다.");
                return false;
            }

            bool isDone = false;
            bool isSuccess = false;

            LoadSceneController.Instance.Init(success =>
            {
                isSuccess = success;
                isDone = true;
            });

            while (!isDone)
            {
                await Awaitable.NextFrameAsync(_cancellationToken);
            }

            if (!isSuccess)
            {
                DebugLogController.GenerateErrorMessage<BootstrapSceneController>("LoadSceneController Init 실패로 Bootstrap 시퀀스를 중단합니다.");
            }

            return isSuccess;
        }
    }

    private class AddressableAssetManage : ISequenceStep
    {
        private readonly Action<string> onStatusChanged;

        public AddressableAssetManage(Action<string> _onStatusChanged)
        {
            onStatusChanged = _onStatusChanged;
        }

        public async Awaitable<bool> Execute(CancellationToken _cancellationToken)
        {
            onStatusChanged?.Invoke("리소스 초기화 중...");

            if (AddressableAssetController.Instance != null)
            {
                AddressableAssetController.Instance.Init();
            }

            await Awaitable.NextFrameAsync(_cancellationToken);

            return true;
        }
    }

    private class JsonDataParseManage : ISequenceStep
    {
        private readonly Action<string> onStatusChanged;

        public JsonDataParseManage(Action<string> _onStatusChanged)
        {
            onStatusChanged = _onStatusChanged;
        }

        public async Awaitable<bool> Execute(CancellationToken _cancellationToken)
        {
            onStatusChanged?.Invoke("데이터 파싱 중...");

            await Awaitable.NextFrameAsync(_cancellationToken);

            return true;
        }
    }

    private class SoundManage : ISequenceStep
    {
        private readonly Action<string> onStatusChanged;

        public SoundManage(Action<string> _onStatusChanged)
        {
            onStatusChanged = _onStatusChanged;
        }

        public async Awaitable<bool> Execute(CancellationToken _cancellationToken)
        {
            onStatusChanged?.Invoke("사운드 초기화 중...");

            _ = SoundController.Instance;

            await Awaitable.NextFrameAsync(_cancellationToken);

            return true;
        }
    }


    #region LifeCycle
    private void Start()
    {
        lifetimeCts = new CancellationTokenSource();
        currentProgressValue = 0f;

        LoadSceneManage loadSceneManage = new LoadSceneManage(SetStatusMessage);
        AddressableAssetManage addressableAssetManage = new AddressableAssetManage(SetStatusMessage);
        JsonDataParseManage jsonDataParseManage = new JsonDataParseManage(SetStatusMessage);
        SoundManage soundManage = new SoundManage(SetStatusMessage);

        SequenceManager.Instance.Enqueue(loadSceneManage);
        SequenceManager.Instance.Enqueue(addressableAssetManage);
        SequenceManager.Instance.Enqueue(jsonDataParseManage);
        SequenceManager.Instance.Enqueue(soundManage);

        // 큐에 등록된 단계 수(sequenceQueue.Count)를 실행 시작 시점 기준 totalCount로 넘겨주므로,
        // 이 이벤트 하나로 "100 / 단계 수" 만큼씩 슬라이더를 증가시키는 진행률 계산이 가능하다.
        SequenceManager.Instance.OnStepCompleted += OnSequenceStepCompleted;
        SequenceManager.Instance.DoSequenceAction(OnBootstrapSequenceCompleted);
    }

    private void OnDestroy()
    {
        if (SequenceManager.Instance != null)
        {
            SequenceManager.Instance.OnStepCompleted -= OnSequenceStepCompleted;
        }

        lifetimeCts?.Cancel();
        lifetimeCts?.Dispose();
    }

    /// <summary>
    /// SequenceManager가 등록된 모든 부트스트랩 단계를 마쳤을 때 호출된다.
    /// 실패 시(예: LoadSceneManage/AddressableAssetManage 단계 실패)에는 씬 전환을 시도하지 않고 로그만 남긴다.
    /// 성공 시에는 슬라이더가 100에 도달했는지 확인한 뒤 0.5~1초 대기 후 main 씬으로 전환한다.
    /// </summary>
    private void OnBootstrapSequenceCompleted(bool _isSuccess)
    {
        SequenceManager.Instance.OnStepCompleted -= OnSequenceStepCompleted;

        if (!_isSuccess)
        {
            DebugLogController.GenerateErrorMessage<BootstrapSceneController>(
                "부트스트랩 시퀀스가 실패로 종료되었습니다. 게임을 재시작해야 할 수 있습니다.");
            return;
        }

        _ = TransitionToMainSceneAsync();
    }
    #endregion

    #region Method
    /// <summary>
    /// SequenceManager.OnStepCompleted 콜백. sequenceQueue의 전체 수량(_totalCount)만큼 100을 나눈 값을
    /// 완료된 단계 수(_completedCount)만큼 곱해 진행률을 계산하고 ProgressBar의 slider에 반영한다.
    /// </summary>
    private void OnSequenceStepCompleted(int _completedCount, int _totalCount)
    {
        if (progressBar == null || _totalCount <= 0)
        {
            return;
        }

        float progressPerStep = 100f / _totalCount;
        currentProgressValue = Mathf.Min(100f, _completedCount * progressPerStep);

        progressBar.SetProgress(currentProgressValue);
    }

    /// <summary>
    /// 모든 준비 단계가 성공적으로 끝난 뒤 호출된다. progressBar가 실제로 100%를 화면에 보여줄 시간을
    /// 확보하기 위해 0.5~1초 대기한 다음 main 씬으로 전환한다.
    /// LoadSceneByTags 호출 실패, Bootstrap 오브젝트가 먼저 파괴되어 대기가 취소되는 경우,
    /// 그 외 예상치 못한 예외까지 모두 여기서 처리해 Bootstrap 화면이 아무 로그 없이 멈추지 않도록 한다.
    /// </summary>
    private async Awaitable TransitionToMainSceneAsync()
    {
        try
        {
            // 부동소수점 누적 오차 등으로 마지막 단계 완료 후에도 100에 정확히 도달하지 못했을 가능성을 방어한다.
            if (currentProgressValue < 100f)
            {
                currentProgressValue = 100f;
            }

            if (progressBar != null)
            {
                progressBar.SetProgress(currentProgressValue);
            }

            float delaySeconds = UnityEngine.Random.Range(minSceneTransitionDelaySeconds, maxSceneTransitionDelaySeconds);
            await Awaitable.WaitForSecondsAsync(delaySeconds, lifetimeCts.Token);

            if (LoadSceneController.Instance == null)
            {
                DebugLogController.GenerateErrorMessage<BootstrapSceneController>("LoadSceneController.Instance가 null이라 main 씬으로 전환할 수 없습니다.");
                return;
            }

            bool started = LoadSceneController.Instance.LoadSceneByTags(mainSceneTag, isSuccess =>
            {
                if (!isSuccess)
                {
                    DebugLogController.GenerateErrorMessage<BootstrapSceneController>("main 씬으로 전환하는 데 실패했습니다.");
                }
            });

            if (!started)
            {
                DebugLogController.GenerateErrorMessage<BootstrapSceneController>("main 씬 전환 시작에 실패했습니다.");
            }
        }
        catch (OperationCanceledException)
        {
            // Bootstrap 오브젝트/씬이 먼저 파괴된 경우(OnDestroy의 lifetimeCts.Cancel) 발생하는 정상적인 취소이므로 별도 처리 없이 종료한다.
        }
        catch (Exception exception)
        {
            DebugLogController.GenerateErrorMessage<BootstrapSceneController>($"main 씬 전환 중 예외가 발생했습니다 : {exception}");
        }
    }

    /// <summary>
    /// 각 ISequenceStep이 시작될 때 호출되어 현재 작업 단계를 ProgressBar의 상태 텍스트에 반영한다.
    /// </summary>
    private void SetStatusMessage(string _message)
    {
        if (progressBar == null)
        {
            return;
        }

        progressBar.SetStatusMessage(_message);
    }
    #endregion
}
