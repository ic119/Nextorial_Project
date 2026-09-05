using UnityEngine;

/// <summary>
/// 공격력/방어력으로부터 실제 데미지를 계산하는 유일한 기준(source of truth).
/// 밸런스 공식이 바뀌어도 이 클래스만 수정하면 되도록, 데미지 계산 로직을 여기 한 곳에 모은다.
/// </summary>
public static class CombatCalculator
{
    /// <summary>방어력을 아무리 많이 적용해도 이 값 밑으로는 내려가지 않는다(딜 0 방지).</summary>
    private const int MinDamage = 1;

    /// <summary>
    /// 공격 측 원본 데미지를 계산한다. 콤보(기본 공격)는 skillDamage에 0을 넘기면 attackPower만 적용되고,
    /// 스킬은 SkillData.damage를 skillDamage로 넘겨 attackPower에 더한다.
    /// </summary>
    public static int CalculateAttackDamage(int attackPower, int skillDamage)
    {
        return Mathf.Max(0, attackPower) + Mathf.Max(0, skillDamage);
    }

    /// <summary>방어 측 defense를 적용해 최종 데미지를 계산한다.</summary>
    public static int ApplyDefense(int rawDamage, int defense)
    {
        return Mathf.Max(MinDamage, rawDamage - Mathf.Max(0, defense));
    }
}
