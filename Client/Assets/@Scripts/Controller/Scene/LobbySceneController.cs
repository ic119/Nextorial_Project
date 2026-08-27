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
            WireCharacterCreatePopup(lobbySceneView);
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
                    WireCharacterCreatePopup(lobbySceneView);
                }
                else
                {
                    DebugLogController.GenerateErrorMessage<LobbySceneController>($"LobbyScene UI 로드 실패 Key: {key}");
                }
            });
        }
    }

    private void WireCharacterCreatePopup(UI_LobbySceneView view)
    {
        var popup = view != null ? view.CharacterCreatePopup : null;
        if (popup != null)
        {
            popup.OnCreateRequested = HandleCharacterCreateRequested;
        }
    }

    /// <summary>
    /// UI_CharacterCreatePopup으로부터 위임받은 캐릭터 저장 및 게임 상태 갱신 처리.
    /// 성공 시 true, 실패 시 false를 반환.
    /// </summary>
    private bool HandleCharacterCreateRequested(UserSaveData userSaveData)
    {
        var fullSaveModel = new SaveUserDataModel
        {
            user = userSaveData,
            dragon = new DragonSaveData
            {
                dragonID = "BabyDragon",
                dragonLevel = 1,
                dragonStats = new DragonStats { str = 5, mana = 10 }
            }
        };

        if (SaveDataController.Instance == null)
        {
            return false;
        }

        bool saveSuccess = SaveDataController.Instance.Save(fullSaveModel);
        if (!saveSuccess)
        {
            return false;
        }

        DebugLogController.GenerateLogMessage<LobbySceneController>($"캐릭터 생성 및 저장 성공: {userSaveData.userID} ({userSaveData.gender})");

        if (GameManager.Instance != null)
        {
            GameManager.Instance.HasSaveData = true;
        }

        return true;
    }
    #endregion
}
