using System;
using UnityEngine;

public interface IStatBlock 
{
    int GetTotal();
}

[Serializable]
public class UserStats : IStatBlock
{
    public int str;
    public int agi;
    public int intel;

    public int GetTotal() => str + agi + intel;
}

[Serializable]
public class DragonStats : IStatBlock
{
    public int str;
    public int mana;

    public int GetTotal() => str + mana;
}