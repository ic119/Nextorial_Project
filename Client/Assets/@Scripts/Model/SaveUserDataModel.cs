using System;

[Serializable]
public class UserSaveData
{
    public string userID;
    public Gender gender;
    public int userLevel;
    public float userExp;
    public UserStats userStats;
    public int hairIndex;
    public int eyeIndex;
    public int mouthIndex;
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