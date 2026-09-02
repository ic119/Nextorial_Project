using System;

[Serializable]
public class UserSaveData
{
    public string userID;
    public int userLevel;
    public float userExp;
    public UserStats userStats;
    public int maxHp;
    public int currentHp;
    public int hairIndex;
    public int eyeIndex;
    public int mouthIndex;

    private const int DefaultLevel = 1;
    private const float DefaultExp = 0f;
    private const int DefaultMaxHp = 100;

    /// <summary>
    /// 캐릭터 생성 시 사용할 기본 스펙(레벨/경험치/능력치/체력)으로 채운 UserSaveData를 만든다.
    /// 외형(hairIndex/eyeIndex/mouthIndex)과 닉네임은 호출부(캐릭터 생성 UI)에서 정해지므로 인자로 받고,
    /// 그 외 기본값은 여기 한 곳에서만 관리해 View와 Model이 서로 다른 기본값을 갖지 않도록 한다.
    /// </summary>
    public static UserSaveData CreateDefault(string userID, int hairIndex, int eyeIndex, int mouthIndex)
    {
        return new UserSaveData
        {
            userID = userID,
            userLevel = DefaultLevel,
            userExp = DefaultExp,
            userStats = UserStats.CreateDefault(),
            maxHp = DefaultMaxHp,
            currentHp = DefaultMaxHp,
            hairIndex = hairIndex,
            eyeIndex = eyeIndex,
            mouthIndex = mouthIndex
        };
    }
}

[Serializable]
public class DragonSaveData
{
    public string dragonID;
    public int dragonLevel;
    public DragonStats dragonStats;
}

[Serializable]
public class SaveUserDataModel
{
    public UserSaveData user;
    public DragonSaveData dragon;
}
