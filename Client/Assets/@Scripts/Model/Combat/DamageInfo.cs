using UnityEngine;

/// <summary>
/// 한 번의 피격에 필요한 정보. Amount는 방어력 적용 전(공격측 계산까지 끝난) 원본 데미지이며,
/// 방어력 반영은 피격 측(HealthComponent.TakeDamage)에서 CombatCalculator.ApplyDefense로 수행한다.
/// </summary>
public readonly struct DamageInfo
{
    public readonly int Amount;
    public readonly GameObject Attacker;

    public DamageInfo(int amount, GameObject attacker)
    {
        Amount = amount;
        Attacker = attacker;
    }
}
