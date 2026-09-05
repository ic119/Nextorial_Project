using System;

/// <summary>
/// 공격력/방어력 스탯 블록. UserStats(str/agi/intel)와 마찬가지로 IStatBlock을 구현해
/// 같은 방식으로 다뤄질 수 있게 한다.
/// </summary>
[Serializable]
public class CombatStat : IStatBlock
{
    public int attackPower;
    public int defense;

    public int GetTotal() => attackPower + defense;
}
