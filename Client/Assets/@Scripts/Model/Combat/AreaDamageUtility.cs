using System;
using UnityEngine;

/// <summary>
/// OverlapSphereNonAlloc으로 범위 내 IDamageable을 찾아 데미지를 적용하는 공용 로직.
/// PlayerController(근접 콤보/스킬), MonsterController(몬스터 공격), DragonController(메테오 등 즉시 범위 스킬),
/// DragonProjectile(파이어볼 등 투사체)이 각자 구현하던
/// "구체 판정 -> 자기 진영 제외 -> (선택) 화이트리스트 필터 -> IDamageable.TakeDamage" 절차를 하나로 모은다.
/// </summary>
public static class AreaDamageUtility
{
    /// <summary>
    /// origin 기준 radius 범위의 targetMask 대상 중 IDamageable에게 rawDamage를 적용한다.
    /// excludeRoot와 transform.root가 같은 대상(공격자 자신)은 제외하고, whitelistFilter가 주어지면
    /// 그 조건을 통과한 Collider만 데미지를 받는다(예: 드래곤 스킬의 몬스터 전용 화이트리스트).
    /// </summary>
    /// <returns>실제로 데미지가 적용된 대상 수. 호출측이 "하나라도 명중했는지"를 알아야 할 때(예: 관통하지 않는 투사체) 0보다 큰지로 판단하면 된다.</returns>
    public static int ApplyOverlapDamage(
        Vector3 origin,
        float radius,
        Collider[] buffer,
        LayerMask targetMask,
        Transform excludeRoot,
        int rawDamage,
        GameObject attacker,
        Func<Collider, bool> whitelistFilter = null)
    {
        int hitCount = Physics.OverlapSphereNonAlloc(origin, radius, buffer, targetMask, QueryTriggerInteraction.Collide);
        if (hitCount == 0)
        {
            return 0;
        }

        DamageInfo damageInfo = new DamageInfo(rawDamage, attacker);
        int appliedCount = 0;

        for (int i = 0; i < hitCount; i++)
        {
            Collider hitCollider = buffer[i];

            if (excludeRoot != null && hitCollider.transform.root == excludeRoot)
            {
                continue;
            }

            if (whitelistFilter != null && !whitelistFilter(hitCollider))
            {
                continue;
            }

            IDamageable damageable = hitCollider.GetComponentInParent<IDamageable>();
            if (damageable == null)
            {
                continue;
            }

            damageable.TakeDamage(damageInfo);
            appliedCount++;
        }

        return appliedCount;
    }
}
