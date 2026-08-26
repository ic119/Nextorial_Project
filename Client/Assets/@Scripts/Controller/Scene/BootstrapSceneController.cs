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

    private const string lobbySceneTag = "LobbyScene";

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
            onStatusChanged?.Invoke("Scene Data Load...");

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
            onStatusChanged?.Invoke("Resource initialization...");

            if (AddressableAssetController.Instance != null)
            {
                AddressableAssetController.Instance.Init();
            }

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
        SoundManage soundManage = new SoundManage(SetStatusMessage);

        SequenceManager.Instance.Enqueue(loadSceneManage);
        SequenceManager.Instance.Enqueue(addressableAssetManage);
        SequenceManager.Instance.Enqueue(soundManage);

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

    private async Awaitable TransitionToMainSceneAsync()
    {
        try
        {
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

            bool started = LoadSceneController.Instance.LoadSceneByTags(lobbySceneTag, isSuccess =>
            {
                if (!isSuccess)
                {
                    DebugLogController.GenerateErrorMessage<BootstrapSceneController>("LobbyScene 전환 실패.");
                }
            });

            if (!started)
            {
                DebugLogController.GenerateErrorMessage<BootstrapSceneController>("LobbyScene 전환 시작에 실패.");
            }
        }
        catch (OperationCanceledException)
        {
            // Bootstrap 오브젝트/씬이 먼저 파괴된 경우(OnDestroy의 lifetimeCts.Cancel) 발생하는 정상적인 취소이므로 별도 처리 없이 종료한다.
        }
        catch (Exception exception)
        {
            DebugLogController.GenerateErrorMessage<BootstrapSceneController>($"LobbyScene 전환 중 예외가 발생했습니다 : {exception}");
        }
    }
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
