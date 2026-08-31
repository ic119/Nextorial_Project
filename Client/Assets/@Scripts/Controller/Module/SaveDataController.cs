using System;
using System.IO;
using UnityEngine;

public class SaveDataController : SingletonObject<SaveDataController>
{
    #region Variable
    private const string saveFileName = "SaveUserData.json";

    private string SaveFilePath => Path.Combine(Application.persistentDataPath, saveFileName);

    /// <summary>
    /// 마지막으로 Save/Load된 데이터의 메모리 캐시. Load를 먼저 호출해야 채워진다.
    /// </summary>
    public SaveUserDataModel CurrentData { get; private set; }

    /// <summary>
    /// 씬 전환 중에도 CurrentData 캐시가 유지되도록 한다. false(기본값)로 두면 이 컨트롤러가 배치된 씬이
    /// 언로드될 때(예: Bootstrap → Lobby 전환) 함께 파괴되어, 이후 Instance 접근 시 캐시가 사라진다.
    /// </summary>
    protected override bool PersistAcrossScenes => true;
    #endregion

    #region Method
    /// <summary>
    /// _data를 JsonUtility로 직렬화해 Application.persistentDataPath에 저장한다.
    /// 성공 여부를 반환하며, 실패 시(잘못된 경로, 쓰기 권한 등) 원인을 로그로 남기고 false를 반환한다.
    /// </summary>
    public bool Save(SaveUserDataModel _data)
    {
        if (_data == null)
        {
            DebugLogController.GenerateErrorMessage<SaveDataController>("저장할 데이터가 null입니다.");
            return false;
        }

        try
        {
            string json = JsonUtility.ToJson(_data, true);
            File.WriteAllText(SaveFilePath, json);
            CurrentData = _data;
            return true;
        }
        catch (Exception exception)
        {
            DebugLogController.GenerateErrorMessage<SaveDataController>(
                $"저장 실패 Path : {SaveFilePath}, Exception : {exception}");
            return false;
        }
    }

    /// <summary>
    /// 저장 파일을 읽어 JsonUtility로 역직렬화한다.
    /// 파일이 없거나(최초 실행) 읽기/파싱에 실패하면 빈 SaveUserDataModel을 새로 만들어 반환한다.
    /// </summary>
    public SaveUserDataModel Load()
    {
        if (!File.Exists(SaveFilePath))
        {
            CurrentData = new SaveUserDataModel();
            return CurrentData;
        }

        try
        {
            string json = File.ReadAllText(SaveFilePath);
            CurrentData = JsonUtility.FromJson<SaveUserDataModel>(json) ?? new SaveUserDataModel();
        }
        catch (Exception exception)
        {
            DebugLogController.GenerateErrorMessage<SaveDataController>(
                $"불러오기 실패 Path : {SaveFilePath}, Exception : {exception}");
            CurrentData = new SaveUserDataModel();
        }

        return CurrentData;
    }



    /// <summary>
    /// Application.persistentDataPath에 저장 파일이 존재하는지 확인한다.
    /// </summary>
    public bool HasSaveData()
    {
        return File.Exists(SaveFilePath);
    }

    /// <summary>
    /// 이미 생성된 캐릭터의 외형(헤어/눈/입)만 갱신해서 다시 저장한다.
    /// 최초 캐릭터 생성은 LobbySceneController가 SaveUserDataModel 전체를 구성해 Save()를 직접 호출하므로,
    /// 이 메서드는 "이미 생성된 캐릭터의 외형을 나중에 다시 바꾸는" 시나리오 전용이다.
    /// CurrentData가 비어있으면 먼저 Load를 시도하고, 그래도 user가 없으면(아직 캐릭터 생성 전) 실패로 처리한다.
    /// </summary>
    public bool UpdateCharacterCustomization(int hairIndex, int eyeIndex, int mouthIndex)
    {
        if (CurrentData == null)
        {
            Load();
        }

        if (CurrentData?.user == null)
        {
            DebugLogController.GenerateErrorMessage<SaveDataController>(
                "갱신할 캐릭터 세이브 데이터가 없습니다. 캐릭터를 먼저 생성해야 합니다.");
            return false;
        }

        CurrentData.user.hairIndex = hairIndex;
        CurrentData.user.eyeIndex = eyeIndex;
        CurrentData.user.mouthIndex = mouthIndex;

        return Save(CurrentData);
    }

    #endregion
}
