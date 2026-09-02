using UnityEngine;

public class LobbySceneController : MonoBehaviour
{
    #region Variable
    [Header("UI Reference")]
    [SerializeField] private UI_LobbySceneView lobbySceneView;

    private const string gameSceneTag = "GameScene";
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
        if (lobbySceneView == null)
        {
            lobbySceneView = Object.FindAnyObjectByType<UI_LobbySceneView>();
        }

        if (lobbySceneView != null)
        {
            WireCharacterCreatePopup(lobbySceneView);
            return;
        }

        if (AddressableAssetController.Instance == null)
        {
            DebugLogController.GenerateErrorMessage<LobbySceneController>("AddressableAssetController.Instance가 없어 LobbyScene UI를 로드할 수 없습니다.");
            return;
        }

        AddressableAssetController.Instance.LoadAndBindUI<UI_LobbySceneView>(AddressableKey.UI_LobbyScene, view =>
        {
            lobbySceneView = view;
            WireCharacterCreatePopup(view);
        });
    }

private void WireCharacterCreatePopup(UI_LobbySceneView view)
    {
        var popup = view != null ? view.CharacterCreatePopup : null;

        // 진단용 로그: OnCreateRequested가 놀 상태로 남는 증상을 추적하기 위해, 어떤 popup 인스턴스(InstanceID)에
        // 배선했는지를 기록한다. 다음 재현 시 버튼 클릭 시점의 InstanceID와 비교해 동일 인스턴스인지 확인한다.
        if (popup == null)
        {
            DebugLogController.GenerateErrorMessage<LobbySceneController>(
                $"WireCharacterCreatePopup: view 또는 popup이 null입니다. view null? {view == null}");
            return;
        }

        popup.OnCreateRequested = HandleCharacterCreateRequested;

        DebugLogController.GenerateLogMessage<LobbySceneController>(
            $"WireCharacterCreatePopup 완료. popup InstanceID={popup.GetInstanceID()}, view InstanceID={view.GetInstanceID()}, OnCreateRequested 설정됨={popup.OnCreateRequested != null}");
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

        DebugLogController.GenerateLogMessage<LobbySceneController>($"캐릭터 생성 및 저장 성공: {userSaveData.userID} (헤어:{userSaveData.hairIndex}, 눈:{userSaveData.eyeIndex}, 입:{userSaveData.mouthIndex})");

        if (GameManager.Instance != null)
        {
            GameManager.Instance.HasSaveData = true;
        }

        TransitionToGameScene();

        return true;
    }

    /// <summary>
    /// 캐릭터 생성/저장이 끝난 뒤 GameScene으로 전환한다.
    /// Bootstrap → Lobby 전환과 동일한 LoadSceneController 인프라(LoadSceneByTags)를 재사용한다.
    /// </summary>
    private void TransitionToGameScene()
    {
        if (LoadSceneController.Instance == null)
        {
            DebugLogController.GenerateErrorMessage<LobbySceneController>("LoadSceneController.Instance가 없어 GameScene으로 전환할 수 없습니다.");
            return;
        }

        bool started = LoadSceneController.Instance.LoadSceneByTags(gameSceneTag, isSuccess =>
        {
            if (!isSuccess)
            {
                DebugLogController.GenerateErrorMessage<LobbySceneController>("GameScene 전환 실패.");
            }
        });

        if (!started)
        {
            DebugLogController.GenerateErrorMessage<LobbySceneController>("GameScene 전환 시작에 실패.");
        }
    }
    #endregion
}
