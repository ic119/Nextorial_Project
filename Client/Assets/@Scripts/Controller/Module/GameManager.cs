using UnityEngine;

public class GameManager : SingletonObject<GameManager>
{
    #region Variable
    /// <summary>
    /// Application.persistentDataPath에 저장 데이터가 있는지 여부.
    /// BootstrapSceneController가 부트스트랩 단계에서 SaveDataController.HasSaveData() 결과로 채우고,
    /// LobbyScene의 UI_LobbySceneView 등에서 참조해 이어하기(continueButton) 노출 여부를 결정한다.
    /// </summary>
    public bool HasSaveData;

    /// <summary>
    /// BootstrapScene에서 설정한 값이 LobbyScene 전환 이후에도 유지되도록 한다.
    /// </summary>
    protected override bool PersistAcrossScenes => true;
    #endregion
}
