using System;
using UnityEngine;

/// <summary>
/// 일반 몬스터(NormalMonster 등)의 체력/공격력 스탯 진입점.
/// PlayerCharacterModel과 동일하게 HealthComponent(체력/피격/사망)와 CombatStatComponent(공격력/방어력)에
/// 실제 로직을 위임하는 얇은 어댑터다. 다만 몬스터는 유저처럼 저장 데이터(UserSaveData)가 없으므로,
/// UserStats로부터 계산하는 대신 Inspector에 직접 입력한 기본값(maxHp, CombatStatComponent의 baseStat)을
/// 스폰 시 그대로 적용한다.
/// </summary>
[RequireComponent(typeof(HealthComponent), typeof(CombatStatComponent))]
public class MonsterModel : MonoBehaviour
{
    [Header("Monster Base Stat")]
    [Tooltip("스폰 시 채워지는 최대 체력. 공격력/방어력은 같은 오브젝트의 CombatStatComponent에서 직접 설정한다.")]
    [SerializeField] private int maxHp = 30;

    private HealthComponent healthComponent;
    private CombatStatComponent combatStatComponent;

    public int MaxHp => healthComponent.MaxHp;
    public int CurrentHp => healthComponent.CurrentHp;
    public int AttackPower => combatStatComponent.AttackPower;
    public int Defense => combatStatComponent.Defense;
    public bool IsDead => healthComponent.IsDead;

    private void Awake()
    {
        healthComponent = GetComponent<HealthComponent>();
        combatStatComponent = GetComponent<CombatStatComponent>();

        if (healthComponent == null || combatStatComponent == null)
        {
            DebugLogController.GenerateErrorMessage<MonsterModel>("HealthComponent/CombatStatComponent가 없어 체력/공격력 계산이 동작하지 않습니다.");
            return;
        }

        healthComponent.ApplyHealth(maxHp, maxHp);
        healthComponent.OnDied += HandleDied;
    }

    private void OnDestroy()
    {
        if (healthComponent != null)
        {
            healthComponent.OnDied -= HandleDied;
        }
    }

    /// <summary>
    /// 체력이 0이 되면 즉시 제거한다. 사망 연출/보상 지급(경험치 등)이 필요해지면 이 메서드를 확장하면 된다.
    /// </summary>
    private void HandleDied()
    {
        Destroy(gameObject);
    }
}
