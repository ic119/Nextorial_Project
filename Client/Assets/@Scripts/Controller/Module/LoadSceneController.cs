using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.SceneManagement;

public class LoadSceneController : SingletonObject<LoadSceneController>
{
    #region Nested
    /// <summary>
    /// SceneLoadAsync에서 순차 처리할 단일 로드 업무
    /// </summary>
    private interface ILoadTask
    {
        Awaitable ExecuteAsync();
    }

    private class AddressableLoadTask : ILoadTask
    {
        private readonly string key;
        private const float loadTimeoutSeconds = 30.0f;

        public AddressableLoadTask(string _key)
        {
            key = _key;
        }

        public async Awaitable ExecuteAsync()
        {
            if (AddressableAssetController.Instance == null || string.IsNullOrEmpty(key))
            {
                return;
            }

            AddressableAssetController controller = AddressableAssetController.Instance;
            controller.AddKeyHashSet(key);
            controller.LoadPrefabAddress<GameObject>(key);

            // 이미 캐시되어 있으면 즉시 완료
            if (controller.IsLoaded(key))
            {
                return;
            }

            // 핸들 폴링 + NextFrameAsync 루프는 AddressableAssetController.WaitForLoadAsync로 공통화되어 있으므로
            // 여기서는 타임아웃 조건만 전달해 중복 구현을 피한다.
            float startTime = Time.unscaledTime;
            await controller.WaitForLoadAsync(key, () => Time.unscaledTime - startTime >= loadTimeoutSeconds);

            if (controller.IsLoaded(key))
            {
                return;
            }

            if (controller.HasLoadFailed(key))
            {
                DebugLogController.GenerateErrorMessage<LoadSceneController>(
                    $"Addressable 프리로드 실패 Key : {key}");
                return;
            }

            // 위 두 경우가 아니라면 타임아웃으로 대기가 중단된 것이다.
            DebugLogController.GenerateErrorMessage<LoadSceneController>(
                $"Addressable 프리로드 타임아웃 Key : {key}, Timeout : {loadTimeoutSeconds}s");
        }
    }

    private class AdditiveSceneLoadTask : ILoadTask
    {
        private readonly string sceneName;
        private readonly string activeSceneName;

        public AdditiveSceneLoadTask(string _sceneName, string _activeSceneName)
        {
            sceneName = _sceneName;
            activeSceneName = _activeSceneName;
        }

        public async Awaitable ExecuteAsync()
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                return;
            }

            AsyncOperation async = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);

            if (async == null)
            {
                // 빌드 세팅에 없는 씬 이름 등으로 로드 자체가 시작되지 못한 경우.
                // null 상태로 async.isDone에 접근하면 NullReferenceException이 발생하므로 여기서 방어한다.
                DebugLogController.GenerateErrorMessage<LoadSceneController>(
                    $"Additive Scene 로드 실패(빌드 세팅에 없는 씬일 수 있음) SceneName : {sceneName}");
                return;
            }

            while (!async.isDone)
            {
                await Awaitable.NextFrameAsync();
            }

            if (activeSceneName == sceneName)
            {
                Scene targetActiveScene = SceneManager.GetSceneByName(sceneName);
                if (targetActiveScene.IsValid())
                {
                    SceneManager.SetActiveScene(targetActiveScene);
                }
            }
        }
    }
    #endregion

    #region Variable
    [SerializeField] SceneDataModel currentSceneDataModel;
    private Dictionary<string, SceneDataModel> sceneDataModelDictionary;
    private Dictionary<string, AddressableAssetModel> addressableAssetModelDictionary;
    private List<SceneDataModel> sceneDataModelList;
    private const string sceneDataScriptableObjectName = "SceneDataModelSO";
    private const string addressableAssetScriptableObjectName = "AddressableAssetModelSO";

    /// <summary>
    /// Init이 성공 완료되었는지 여부
    /// </summary>
    public bool IsInitialized { get; private set; }

    /// <summary>
    /// 0 ~ 100. Queue 업무가 완료될수록 100에 가까워진다.
    /// </summary>
    public float currentLoadProgressValue = 0.0f;

    private readonly Queue<ILoadTask> loadTaskQueue = new Queue<ILoadTask>();
    private int totalLoadTaskCount;
    private int completedLoadTaskCount;
    private string currentSceneTag;

    /// <summary>
    /// SceneLoadAsync가 진행 중인 동안 true. LoadSceneByTags의 중복/재진입 호출을 막는 데 사용한다.
    /// </summary>
    private bool isSceneLoading;
    #endregion

    #region Method
    /// <summary>
    /// SceneDataModelSO / AddressableAssetModelSO를 Addressable로 로드한다.
    /// 완료 시 _onComplete(true/false)를 반드시 호출하여 호출측이 무한 대기하지 않도록 한다.
    /// </summary>
    public void Init(Action<bool> _onComplete = null)
    {
        IsInitialized = false;
        LoadSceneDataModelSO(sceneSuccess =>
        {
            if (!sceneSuccess)
            {
                _onComplete?.Invoke(false);
                return;
            }

            LoadAddressableAssetModelSO(addressableSuccess =>
            {
                if (!addressableSuccess)
                {
                    IsInitialized = false;
                    _onComplete?.Invoke(false);
                    return;
                }

                IsInitialized = true;
                _onComplete?.Invoke(true);
            });
        });
    }
   
    private void LoadSceneDataModelSO(Action<bool> _onComplete)
    {
        AsyncOperationHandle<SceneDataModelSO> handle;

        try
        {
            handle = Addressables.LoadAssetAsync<SceneDataModelSO>(sceneDataScriptableObjectName);
        }
        catch (Exception exception)
        {
            DebugLogController.GenerateErrorMessage<LoadSceneController>($"SceneDataModelSO 로드 실패(잘못된 Key) Key : {sceneDataScriptableObjectName}, Exception : {exception}");
            _onComplete?.Invoke(false);
            return;
        }

        handle.Completed += result =>
        {
            if (result.Status != AsyncOperationStatus.Succeeded || result.Result == null)
            {
                sceneDataModelDictionary = null;
                sceneDataModelList = null;

                _onComplete?.Invoke(false);
                return;
            }

            sceneDataModelDictionary = new Dictionary<string, SceneDataModel>();
            sceneDataModelList = result.Result.sceneDataModels;

            if (sceneDataModelList == null || sceneDataModelList.Count == 0)
            {
                _onComplete?.Invoke(false);
                return;
            }

            for (int i = 0; i < sceneDataModelList.Count; i++)
            {
                SceneDataModel model = sceneDataModelList[i];
                if (model == null || string.IsNullOrEmpty(model.tags))
                {
                    continue;
                }

                if (!sceneDataModelDictionary.ContainsKey(model.tags))
                {
                    sceneDataModelDictionary.Add(model.tags, model);
                }
            }

            if (sceneDataModelDictionary.Count == 0)
            {
                _onComplete?.Invoke(false);
                return;
            }
            _onComplete?.Invoke(true);
        };
    }

    private void LoadAddressableAssetModelSO(Action<bool> _onComplete)
    {
        AsyncOperationHandle<AddressableAssetModelSO> handle;

        try
        {
            handle = Addressables.LoadAssetAsync<AddressableAssetModelSO>(addressableAssetScriptableObjectName);
        }
        catch (Exception exception)
        {
            DebugLogController.GenerateErrorMessage<LoadSceneController>(
                $"AddressableAssetModelSO 로드 실패(잘못된 Key) Key : {addressableAssetScriptableObjectName}, Exception : {exception}");
            _onComplete?.Invoke(false);
            return;
        }

        handle.Completed += result =>
        {
            if (result.Status != AsyncOperationStatus.Succeeded || result.Result == null)
            {
                addressableAssetModelDictionary = null;

                _onComplete?.Invoke(false);
                return;
            }

            addressableAssetModelDictionary = new Dictionary<string, AddressableAssetModel>();
            List<AddressableAssetModel> models = result.Result.addressableAssetModels;

            if (models == null || models.Count == 0)
            {
                _onComplete?.Invoke(false);
                return;
            }

            for (int i = 0; i < models.Count; i++)
            {
                AddressableAssetModel model = models[i];
                if (model == null || string.IsNullOrEmpty(model.tags))
                {
                    continue;
                }

                if (!addressableAssetModelDictionary.ContainsKey(model.tags))
                {
                    addressableAssetModelDictionary.Add(model.tags, model);
                }
            }

            if (addressableAssetModelDictionary.Count == 0)
            {
                _onComplete?.Invoke(false);
                return;
            }
            _onComplete?.Invoke(true);
        };
    }

    /// <summary>
    /// _tagName에 해당하는 씬으로 전환을 시작한다.
    /// 반환값은 "전환이 시작됐는지"만 나타낸다 — 초기화 안 됨/빈 tag/존재하지 않는 tag/중복 호출 등
    /// 즉시 판단 가능한 사유로 시작조차 못 하면 false를 반환하고 _onComplete(false)도 함께 호출한다.
    /// 실제 전환(Queue 로딩 + 씬 언로드)의 최종 성공/실패는 _onComplete로만 알 수 있다.
    /// </summary>
    public bool LoadSceneByTags(string _tagName, Action<bool> _onComplete = null)
    {
        if (!IsInitialized || sceneDataModelDictionary == null || addressableAssetModelDictionary == null)
        {
            DebugLogController.GenerateErrorMessage<LoadSceneController>($"LoadSceneByTags 호출 전에 Init이 완료되지 않았습니다. tag : {_tagName}");
            _onComplete?.Invoke(false);
            return false;
        }

        if (string.IsNullOrEmpty(_tagName))
        {
            DebugLogController.GenerateErrorMessage<LoadSceneController>("LoadSceneByTags에 빈 tag가 전달되었습니다.");
            _onComplete?.Invoke(false);
            return false;
        }

        if (!sceneDataModelDictionary.ContainsKey(_tagName))
        {
            DebugLogController.GenerateErrorMessage<LoadSceneController>($"존재하지 않는 Scene tag : {_tagName}. 등록된 tags : [{string.Join(", ", sceneDataModelDictionary.Keys)}]");
            _onComplete?.Invoke(false);
            return false;
        }

        if (isSceneLoading)
        {
            // 이전 SceneLoadAsync가 아직 끝나지 않은 상태에서 재호출되면 loadTaskQueue/진행률 카운터가
            // 두 요청 사이에서 공유되어 꼬이고, ReleaseAllHandler가 이전 요청이 로딩 중인 핸들을
            // 강제로 해제해버리므로 여기서 막는다.
            DebugLogController.GenerateErrorMessage<LoadSceneController>($"이전 씬 로드가 아직 끝나지 않아 요청을 무시합니다. 요청한 tag : {_tagName}, 진행 중인 tag : {currentSceneTag}");
            _onComplete?.Invoke(false);
            return false;
        }

        currentSceneTag = _tagName;
        currentSceneDataModel = sceneDataModelDictionary[_tagName];
        _ = SceneLoadAsync(currentSceneDataModel, _onComplete);
        return true;
    }

    /// <summary>
    /// 현재 씬(Bootstrap)에서 Queue 업무 처리(진행률 0→100) → 완료 후 원래 씬을 언로드하며 전환 마무리.
    /// 더 이상 별도의 LoadingScene으로 전환하지 않는다.
    /// 성공/실패와 무관하게 _onComplete를 정확히 한 번 호출한다(예상 못한 예외 포함) — 호출측이 무한 대기하지 않도록 한다.
    /// </summary>
    private async Awaitable SceneLoadAsync(SceneDataModel _target, Action<bool> _onComplete)
    {
        isSceneLoading = true;
        bool isSuccess = false;

        try
        {
            // 이전 태그 세션에서 로드했던 Addressable 핸들 / 오브젝트 풀 정리 (최초 실행 시 keyDictionary가 비어 있어 no-op)
            AddressableAssetController.Instance.ReleaseAllHandler();
            ObjectPoolController.Instance.Init();

            currentLoadProgressValue = 0.0f;
            loadTaskQueue.Clear();
            totalLoadTaskCount = 0;
            completedLoadTaskCount = 0;

            // 이 씬(대개 Bootstrap)을 진행률 100% 완료 후 언로드하기 위해 미리 캡처해둔다.
            Scene originScene = SceneManager.GetActiveScene();

            EnqueueLoadTasks(_target);
            await ProcessLoadTaskQueueAsync();

            // Queue가 비워져 Progress가 100이 된 뒤에만 씬 전환
            currentLoadProgressValue = 100.0f;

            // currentLoadProgressValue는 즉시 100이 되지만, 이를 화면에 그리는 쪽(BootstrapSceneController.Update →
            // UI_ProgressBar)은 다음 프레임에야 반영한다. UI가 100%를 실제로 표시하기도 전에 씬이 언로드되어
            // ProgressBar 자체가 사라지는 것을 막기 위해, 씬 전환 전에 잠깐 대기해 100% 표시를 보장한다.
            await Awaitable.WaitForSecondsAsync(0.2f);

            // Queue의 각 업무는 실패해도 completedLoadTaskCount만 증가시키고 넘어가므로, 여기서 대상 씬이
            // 실제로 로드/활성화됐는지 확인하지 않으면 언로드 하나만 남은 씬(Bootstrap)을 지워 화면이 텅 비는
            // 상황이 생길 수 있다. 검증에 실패하면 언로드하지 않고 Bootstrap을 남겨둔 채로 실패를 보고한다.
            isSuccess = ValidateTargetSceneLoaded(_target);

            if (!isSuccess)
            {
                DebugLogController.GenerateErrorMessage<LoadSceneController>(
                    $"'{_target.tags}' 태그의 대상 씬이 로드되지 않아 원래 씬을 언로드하지 않고 중단합니다. activeSceneName : {_target.activeSceneName}");
            }
            else if (originScene.IsValid() && originScene.isLoaded)
            {
                AsyncOperation unloadOriginScene = SceneManager.UnloadSceneAsync(originScene);

                while (unloadOriginScene != null && !unloadOriginScene.isDone)
                {
                    await Awaitable.NextFrameAsync();
                }
            }
        }
        catch (Exception exception)
        {
            isSuccess = false;
            DebugLogController.GenerateErrorMessage<LoadSceneController>($"씬 전환 중 예외가 발생해 중단합니다 : {exception}");
        }
        finally
        {
            isSceneLoading = false;
            _onComplete?.Invoke(isSuccess);
        }
    }

    /// <summary>
    /// _target.activeSceneName이 실제로 로드되어 있는지 확인한다.
    /// 원래 씬(Bootstrap)을 언로드하기 전 마지막 안전장치 — 이 확인 없이 언로드하면
    /// 대상 씬 로드가 실패했을 때 로드된 씬이 하나도 없는 상태(빈 화면)에 빠질 수 있다.
    /// </summary>
    private bool ValidateTargetSceneLoaded(SceneDataModel _target)
    {
        if (_target == null || string.IsNullOrEmpty(_target.activeSceneName))
        {
            return false;
        }

        Scene targetScene = SceneManager.GetSceneByName(_target.activeSceneName);
        return targetScene.IsValid() && targetScene.isLoaded;
    }

    /// <summary>
    /// Addressable / Additive 씬 로드 업무를 Queue에 등록한다.
    /// </summary>
    private void EnqueueLoadTasks(SceneDataModel _target)
    {
        List<string> addressableKeys = CollectPreloadKeyStrings(currentSceneTag);
        for (int i = 0; i < addressableKeys.Count; i++)
        {
            loadTaskQueue.Enqueue(new AddressableLoadTask(addressableKeys[i]));
        }

        List<string> sceneTargets = _target.loadedSceneList ?? new List<string>();
        for (int i = 0; i < sceneTargets.Count; i++)
        {
            loadTaskQueue.Enqueue(new AdditiveSceneLoadTask(sceneTargets[i], _target.activeSceneName));
        }

        totalLoadTaskCount = loadTaskQueue.Count;
        completedLoadTaskCount = 0;
        UpdateProgressByQueue();
    }

    /// <summary>
    /// Queue에 쌓인 업무를 모두 동시에 시작한다.
    /// 태스크를 순차로 기다리면 하나가 느려질 때 전체 진행률이 함께 멈추는 문제가 있어,
    /// 각 업무를 병렬로 실행하고 완료된 개수만큼만 진행률을 올린다.
    /// </summary>
    private async Awaitable ProcessLoadTaskQueueAsync()
    {
        if (totalLoadTaskCount <= 0)
        {
            currentLoadProgressValue = 100.0f;
            return;
        }

        while (loadTaskQueue.Count > 0)
        {
            ILoadTask task = loadTaskQueue.Dequeue();
            _ = RunLoadTaskAsync(task);
        }

        while (completedLoadTaskCount < totalLoadTaskCount)
        {
            await Awaitable.NextFrameAsync();
        }

        currentLoadProgressValue = 100.0f;
    }

    private async Awaitable RunLoadTaskAsync(ILoadTask _task)
    {
        try
        {
            await _task.ExecuteAsync();
        }
        catch (Exception exception)
        {
            DebugLogController.GenerateErrorMessage<LoadSceneController>($"로드 업무 처리 중 예외 발생 : {exception}");
        }
        finally
        {
            completedLoadTaskCount++;
            UpdateProgressByQueue();
        }
    }

    private void UpdateProgressByQueue()
    {
        if (totalLoadTaskCount <= 0)
        {
            currentLoadProgressValue = 100.0f;
            return;
        }

        currentLoadProgressValue = ((float)completedLoadTaskCount / totalLoadTaskCount) * 100.0f;
    }

    /// <summary>
    /// AddressableAssetModelSO에서 씬 태그에 매칭되는 preload Key 목록을 가져온다.
    /// </summary>
    private List<string> CollectPreloadKeyStrings(string _tagName)
    {
        List<string> keyStrings = new List<string>();

        if (addressableAssetModelDictionary == null || string.IsNullOrEmpty(_tagName))
        {
            return keyStrings;
        }

        if (!addressableAssetModelDictionary.TryGetValue(_tagName, out AddressableAssetModel model) || model == null)
        {
            DebugLogController.GenerateLogMessage<LoadSceneController>(
                $"AddressableAssetModelSO에 tag '{_tagName}'에 대한 preload 설정이 없습니다.");
            return keyStrings;
        }

        List<AddressableKey> preloadKeys = model.preloadAddressableKeys;
        if (preloadKeys == null)
        {
            return keyStrings;
        }

        for (int i = 0; i < preloadKeys.Count; i++)
        {
            AddressableKey key = preloadKeys[i];
            if (key == AddressableKey.None)
            {
                continue;
            }

            keyStrings.Add(key.ToString());
        }

        return keyStrings;
    }
    #endregion
}