using UnityEngine;

/// <summary>
/// 공격력/방어력을 들고 있는 컴포넌트. 공격 주체(PlayerController 등)는 AttackPower를,
/// HealthComponent는 Defense를 같은 GameObject에서 GetComponent로 참조해 공용으로 사용한다.
/// PlayerCharacterModel뿐 아니라 향후 Enemy 쪽에도 그대로 붙여 재사용할 수 있도록
/// 특정 캐릭터 종류에 의존하지 않는다.
/// </summary>
public class CombatStatComponent : MonoBehaviour
{
    [Tooltip("ApplyFromUserStats/ApplyGrade가 호출되기 전까지 사용되는 기본 공격력/방어력.")]
    [SerializeField] private CombatStat baseStat = new CombatStat { attackPower = 5, defense = 0 };

    [Tooltip("MonsterGrade에 따른 공격력/방어력 배율. baseStat에 곱해져서 최종 스탯이 된다. 플레이어는 항상 Normal(배율 1)이라 영향이 없다.")]
    [SerializeField] private MonsterGrade grade = MonsterGrade.Normal;

    public int AttackPower => Mathf.RoundToInt(baseStat.attackPower * GetGradeMultiplier());
    public int Defense => Mathf.RoundToInt(baseStat.defense * GetGradeMultiplier());

    /// <summary>
    /// UserStats(str/agi/intel)로부터 공격력/방어력을 계산해 반영한다.
    /// str 1당 공격력 1, agi 2당 방어력 1로 잡은 임시 공식이며, 실제 밸런스 기획이 정해지면
    /// 이 메서드 하나만 바꾸면 된다(PlaceholderMaxExp와 같은 성격의 임시값).
    /// </summary>
    public void ApplyFromUserStats(UserStats userStats)
    {
        if (userStats == null)
        {
            return;
        }

        baseStat.attackPower = userStats.str;
        baseStat.defense = userStats.agi / 2;
    }

    /// <summary>
    /// 몬스터 등급을 지정한다. baseStat 자체는 건드리지 않고 GetGradeMultiplier()를 통해
    /// AttackPower/Defense를 읽을 때만 배율을 적용하므로, 여러 번 호출해도 값이 누적되지 않는다.
    /// </summary>
    public void ApplyGrade(MonsterGrade newGrade)
    {
        grade = newGrade;
    }

    /// <summary>
    /// 등급별 공격력/방어력 배율. 밸런스가 정해지기 전까지의 임시값이며, 필요해지면 이 메서드만 바꾸면 된다.
    /// </summary>
    private float GetGradeMultiplier()
    {
        switch (grade)
        {
            case MonsterGrade.Boss:
                return 3f;
            case MonsterGrade.Normal:
            default:
                return 1f;
        }
    }
}
