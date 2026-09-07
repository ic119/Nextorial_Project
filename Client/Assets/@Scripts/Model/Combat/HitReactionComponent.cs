using System;
using UnityEngine;

/// <summary>
/// 피격 시 GetHit01/02류 애니메이션을 랜덤 재생하고 HitNormal 이펙트를 소환하는 공용 컴포넌트.
/// PlayerController와 MonsterController가 각자 구현하던 피격 리액션(애니메이션/이펙트/이동 잠금) 로직을
/// 하나로 모았다. HealthComponent.OnDamaged를 직접 구독하므로, 소유 컨트롤러는 이 컴포넌트를
/// GetComponent로 참조해 IsHitReacting만 확인하면 된다.
///
/// 콤보/스킬/공격처럼 같은 Attack Layer를 공유하는 다른 애니메이션과의 상호작용은 두 콜백으로
/// 소유 컨트롤러에 위임한다:
/// - BeforeHitReaction: 피격 연출 시작 직전, 진행 중이던 다른 애니메이션의 타이머/파라미터를 정리할 기회.
/// - ShouldKeepAttackLayerActiveAfterReaction: 피격 연출 종료 시 Attack Layer를 꺼도 되는지 여부
///   (예: 플레이어는 콤보/스킬이 여전히 재생 중이면 꺼서는 안 된다).
/// </summary>
[RequireComponent(typeof(Animator))]
public class HitReactionComponent : MonoBehaviour
{
    [Tooltip("피격 시 랜덤으로 재생할 애니메이션 클립 이름들. Attack Layer 안에 있는 GetHit01/02류 상태와 이름이 일치해야 한다.")]
    [SerializeField] private string[] hitClipNames = { "GetHit01", "GetHit02" };

    [Tooltip("피격 순간 재생할 이펙트(캐릭터 기준 로컬 오프셋).")]
    [SerializeField] private Vector3 hitEffectLocalOffset = new Vector3(0f, 1f, 0f);

    [Tooltip("피격 애니메이션이 위치한 Animator 레이어 이름.")]
    [SerializeField] private string attackLayerName = "Attack Layer";

    private const string HitEffectKey = "HitNormal";
    private const int HitEffectPrewarmCount = 3;
    private const float FallbackHitReactionDuration = 0.6f;
    private static readonly int HitIndexHash = Animator.StringToHash("HitIndex");

    private Animator animator;
    private HealthComponent healthComponent;
    private int attackLayerIndex = -1;
    private float[] hitAnimationDurations;

    /// <summary>피격 연출이 재생 중인 동안 true. 소유 컨트롤러는 이 값으로 이동/공격/점프 등을 잠근다.</summary>
    public bool IsHitReacting { get; private set; }

    /// <summary>
    /// 피격 연출을 시작하기 직전(HitIndex를 세팅하기 전)에 호출된다. 소유 컨트롤러가 진행 중이던
    /// 다른 Attack Layer 애니메이션(콤보/스킬/공격)의 타이머와 파라미터를 정리할 기회를 준다.
    /// </summary>
    public event Action BeforeHitReaction;

    /// <summary>
    /// 피격 연출이 끝났을 때 Attack Layer weight를 0으로 되돌려도 되는지 묻는다.
    /// null이거나 false를 반환하면 되돌린다. 콤보/스킬 등이 여전히 재생 중이면 true를 반환해 레이어를 켜진 채로 유지시킨다.
    /// </summary>
    public Func<bool> ShouldKeepAttackLayerActiveAfterReaction;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        healthComponent = GetComponent<HealthComponent>();

        hitAnimationDurations = new float[hitClipNames.Length];
        for (int i = 0; i < hitAnimationDurations.Length; i++)
        {
            hitAnimationDurations[i] = FallbackHitReactionDuration;
        }

        if (healthComponent != null)
        {
            healthComponent.OnDamaged += PlayHitReaction;
        }
        else
        {
            DebugLogController.GenerateErrorMessage<HitReactionComponent>("HealthComponent가 없어 피격 리액션을 구독할 수 없습니다.");
        }

        if (animator == null)
        {
            DebugLogController.GenerateErrorMessage<HitReactionComponent>("Animator가 없어 피격 애니메이션을 재생할 수 없습니다.");
        }
        else
        {
            attackLayerIndex = animator.GetLayerIndex(attackLayerName);
            if (attackLayerIndex < 0)
            {
                DebugLogController.GenerateErrorMessage<HitReactionComponent>($"Animator에 '{attackLayerName}' 레이어가 없어 피격 애니메이션이 표시되지 않을 수 있습니다.");
            }

            CacheHitAnimationDurations();
        }

        // 몬스터처럼 여러 인스턴스가 동시에 존재하는 경우, 이미 풀이 있으면 중복 프리워밍하지 않도록 가드한다.
        if (ObjectPoolController.Instance != null && !ObjectPoolController.Instance.HasPool(HitEffectKey))
        {
            ObjectPoolController.Instance.Preload(HitEffectKey, HitEffectPrewarmCount);
        }
    }

    private void OnDestroy()
    {
        if (healthComponent != null)
        {
            healthComponent.OnDamaged -= PlayHitReaction;
        }
    }

    /// <summary>
    /// 프리팹에 미리 배치되지 않고 런타임에 AddComponent로 붙는 경우(예: PlayerController와 함께
    /// 스폰되는 플레이어 캐릭터), 인스펙터로 지정할 수 없는 hitClipNames를 코드로 주입한다.
    /// Awake 이후 아무 때나 호출해도 안전하며, 클립 길이를 즉시 다시 캐싱한다.
    /// </summary>
    public void SetHitClipNames(params string[] clipNames)
    {
        if (clipNames == null || clipNames.Length == 0)
        {
            return;
        }

        hitClipNames = clipNames;
        hitAnimationDurations = new float[hitClipNames.Length];
        for (int i = 0; i < hitAnimationDurations.Length; i++)
        {
            hitAnimationDurations[i] = FallbackHitReactionDuration;
        }

        if (animator != null)
        {
            CacheHitAnimationDurations();
        }
    }

    private void PlayHitReaction()
    {
        if (animator == null || attackLayerIndex < 0)
        {
            return;
        }

        IsHitReacting = true;
        BeforeHitReaction?.Invoke();

        int hitIndex = UnityEngine.Random.Range(0, hitClipNames.Length);
        animator.SetInteger(HitIndexHash, hitIndex + 1);
        animator.SetLayerWeight(attackLayerIndex, 1f);
        SpawnHitEffect();

        CancelInvoke(nameof(FinishHitReaction));
        Invoke(nameof(FinishHitReaction), hitAnimationDurations[hitIndex]);
    }

    private void FinishHitReaction()
    {
        IsHitReacting = false;

        if (animator == null)
        {
            return;
        }

        animator.SetInteger(HitIndexHash, 0);

        bool keepActive = ShouldKeepAttackLayerActiveAfterReaction != null && ShouldKeepAttackLayerActiveAfterReaction();
        if (!keepActive && attackLayerIndex >= 0)
        {
            animator.SetLayerWeight(attackLayerIndex, 0f);
        }
    }

    /// <summary>hitClipNames의 실제 클립 길이를 읽어둔다. 찾지 못하면 FallbackHitReactionDuration을 그대로 유지한다.</summary>
    private void CacheHitAnimationDurations()
    {
        if (animator.runtimeAnimatorController == null)
        {
            return;
        }

        foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
        {
            if (clip == null)
            {
                continue;
            }

            for (int i = 0; i < hitClipNames.Length; i++)
            {
                if (clip.name == hitClipNames[i])
                {
                    hitAnimationDurations[i] = clip.length;
                }
            }
        }
    }

    /// <summary>hitEffectLocalOffset 위치에 풀링된 HitNormal 이펙트를 소환한다.</summary>
    private void SpawnHitEffect()
    {
        if (ObjectPoolController.Instance == null)
        {
            return;
        }

        Vector3 spawnPosition = transform.TransformPoint(hitEffectLocalOffset);
        ObjectPoolController.Instance.Get(HitEffectKey, spawnPosition, transform.rotation);
    }
}
