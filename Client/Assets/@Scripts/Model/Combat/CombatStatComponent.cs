using UnityEngine;

/// <summary>
/// 공격력/방어력을 들고 있는 컴포넌트. 공격 주체(PlayerController 등)는 AttackPower를,
/// HealthComponent는 Defense를 같은 GameObject에서 GetComponent로 참조해 공용으로 사용한다.
/// PlayerCharacterModel뿐 아니라 향후 Enemy 쪽에도 그대로 붙여 재사용할 수 있도록
/// 특정 캐릭터 종류에 의존하지 않는다.
/// </summary>
public class CombatStatComponent : MonoBehaviour
{
    [Tooltip("ApplyFromUserStats가 호출되기 전까지 사용되는 기본 공격력/방어력.")]
    [SerializeField] private CombatStat baseStat = new CombatStat { attackPower = 5, defense = 0 };

    public int AttackPower => baseStat.attackPower;
    public int Defense => baseStat.defense;

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
}
