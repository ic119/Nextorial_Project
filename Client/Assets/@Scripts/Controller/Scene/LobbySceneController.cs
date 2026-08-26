using UnityEngine;

public class LobbySceneController : MonoBehaviour
{
    #region Variable
    [Header("UI Reference")]
    [SerializeField] private UI_LobbySceneView lobbySceneView;
    #endregion

    #region LifeCycle
    private void Start()
    {
        InitializeLobbyUI();
    }
    #endregion

    #region Method
    private void InitializeLobbyUI()
    {
        // 씬에 이미 UI_LobbySceneView가 배치되어 있는지 확인
        if (lobbySceneView == null)
        {
            lobbySceneView = Object.FindAnyObjectByType<UI_LobbySceneView>();
        }

        if (lobbySceneView != null)
        {
            return;
        }

        // AddressableAssetController를 통한 비동기 UI 프리팹 로드
        string key = AddressableKey.UI_LobbyScene.ToString();

        if (AddressableAssetController.Instance != null)
        {
            AddressableAssetController.Instance.LoadPrefabAddress<GameObject>(key, prefab =>
            {
                if (prefab != null)
                {
                    var uiObj = AddressableAssetController.Instance.InstantiatePrefab(prefab);
                    lobbySceneView = uiObj.GetComponent<UI_LobbySceneView>();
                }
                else
                {
                    DebugLogController.GenerateErrorMessage<LobbySceneController>($"LobbyScene UI 로드 실패 Key: {key}");
                }
            });
        }
    }
    #endregion
}
