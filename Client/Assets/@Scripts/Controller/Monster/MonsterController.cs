using UnityEngine;

/// <summary>
/// NormalMonster 등 근접형 몬스터의 추적/공격 AI. Rigidbody 기반으로 X축만 이동시키는 방식은
/// PlayerController와 동일한 2.5D 규칙(Z축 고정)을 따른다.
/// detectionRange 안에 타겟(유저)이 들어오면 다가가고, attackRange 안에 들어오면 멈춰서
/// attackCooldown 간격으로 공격한다. 타겟이 detectionRange 밖으로 나가면 즉시 추적을 멈춘다
/// (별도의 어그로 유지/귀환 로직은 없다).
/// NormalMonster는 유저 캐릭터(BasicCharacter)와 동일한 Animator Controller(BasicCharacterStance)를
/// 사용하므로, 이동/공격 애니메이션도 PlayerController와 같은 파라미터(IsIdle/IsMove, ComboIndex,
/// Attack Layer)로 재생한다.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CombatStatComponent))]
public class MonsterController : MonoBehaviour
{
    #region Variable
    [Header("Detect/Chase Settings")]
    [Tooltip("이 범위(X축 거리) 안에 타겟이 들어오면 추적을 시작한다. 범위를 벗어나면 즉시 추적을 멈춘다.")]
    [SerializeField] private float detectionRange = 5f;
    [Tooltip("타겟과의 거리가 이 값 이하가 되면 이동을 멈추고 공격한다.")]
    [SerializeField] private float attackRange = 1.2f;
    [SerializeField] private float moveSpeed = 2f;

    [Header("Attack Settings")]
    [Tooltip("공격 사이의 최소 대기 시간(초). Combo01 애니메이션 길이보다 짧으면 재생 중 다음 공격이 씹힐 수 있다.")]
    [SerializeField] private float attackCooldown = 1.5f;
    [Tooltip("공격 판정 원점(캐릭터 기준 로컬 오프셋). PlayerController.TryDealDamage와 동일한 방식(OverlapSphere)을 사용한다.")]
    [SerializeField] private Vector3 attackHitLocalOffset = new Vector3(0f, 1f, 1f);
    [SerializeField] private float attackHitRadius = 1f;
    [Tooltip("데미지 판정에 포함할 레이어. 기본은 전체 레이어이며, 유저 전용 레이어가 생기면 그 레이어만 선택하는 것을 권장한다.")]
    [SerializeField] private LayerMask attackHitTargetMask = ~0;

    [Header("Facing Settings")]
    [SerializeField] private float rightFacingYRotation = 90f;
    [SerializeField] private float leftFacingYRotation = -90f;

    /// <summary>
    /// PlayerController의 SingleSword 콤보 1단계 클립을 그대로 공격 모션으로 사용한다.
    /// 클립을 찾지 못하면 FallbackAttackClipLength로 대체한다.
    /// </summary>
    private const string AttackClipName = "Combo01_InPlace_SingleSword";
    private const float FallbackAttackClipLength = 0.6f;

    /// <summary>공격 모션이 위치한 BasicCharacterStance의 별도 레이어 이름. PlayerController와 동일하다.</summary>
    private const string AttackLayerName = "Attack Layer";
    private int attackLayerIndex = -1;
    private float attackAnimationDuration = FallbackAttackClipLength;

    private static readonly int IsIdleHash = Animator.StringToHash(nameof(PlayerMoveState.IsIdle));
    private static readonly int IsMoveHash = Animator.StringToHash(nameof(PlayerMoveState.IsMove));
    private static readonly int ComboIndexHash = Animator.StringToHash("ComboIndex");

    private static readonly Collider[] AttackHitBuffer = new Collider[8];

    private Rigidbody rb;
    private Animator animator;
    private CombatStatComponent combatStat;
    private Transform target;
    private float nextAttackTime;
    private bool isMoving;
    #endregion

    #region LifeCycle
    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            DebugLogController.GenerateErrorMessage<MonsterController>("Rigidbody가 없어 이동/추적이 동작하지 않습니다.");
            return;
        }

        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionZ;

        animator = GetComponent<Animator>();
        combatStat = GetComponent<CombatStatComponent>();

        if (combatStat == null)
        {
            DebugLogController.GenerateErrorMessage<MonsterController>("CombatStatComponent가 없어 공격력을 계산할 수 없습니다.");
        }

        if (animator == null)
        {
            DebugLogController.GenerateErrorMessage<MonsterController>("Animator가 없어 이동/공격 애니메이션을 재생할 수 없습니다.");
        }
        else
        {
            attackLayerIndex = animator.GetLayerIndex(AttackLayerName);
            if (attackLayerIndex < 0)
            {
                DebugLogController.GenerateErrorMessage<MonsterController>($"Animator에 '{AttackLayerName}' 레이어가 없어 공격 모션이 표시되지 않을 수 있습니다.");
            }

            CacheAttackAnimationDuration();
        }
    }

    private void FixedUpdate()
    {
        if (target == null)
        {
            StopMoving();
            return;
        }

        float deltaX = target.position.x - transform.position.x;
        float distance = Mathf.Abs(deltaX);

        if (distance > detectionRange)
        {
            StopMoving();
            return;
        }

        if (distance <= attackRange)
        {
            StopMoving();
            TryAttack();
            return;
        }

        float direction = Mathf.Sign(deltaX);
        Vector3 velocity = rb.linearVelocity;
        velocity.x = direction * moveSpeed;
        rb.linearVelocity = velocity;

        UpdateFacing(direction);
        SetMoveAnimation(true);
    }
    #endregion

    #region Method
    /// <summary>
    /// 이 몬스터가 추적/공격할 대상(보통 유저 캐릭터)을 지정한다. GameSceneController가
    /// 캐릭터/몬스터 스폰(둘 다 독립적인 비동기 Addressable 로드)이 모두 끝난 뒤 호출해준다.
    /// </summary>
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }

    private void StopMoving()
    {
        Vector3 velocity = rb.linearVelocity;
        velocity.x = 0f;
        rb.linearVelocity = velocity;

        SetMoveAnimation(false);
    }

    private void UpdateFacing(float direction)
    {
        float yRotation = direction > 0f ? rightFacingYRotation : leftFacingYRotation;
        rb.MoveRotation(Quaternion.Euler(0f, yRotation, 0f));
    }

    /// <summary>
    /// PlayerController.UpdateMoveAnimation과 동일하게, 상태가 바뀔 때만 IsIdle/IsMove를 갱신한다.
    /// </summary>
    private void SetMoveAnimation(bool moving)
    {
        if (animator == null || moving == isMoving)
        {
            return;
        }

        isMoving = moving;
        animator.SetBool(IsIdleHash, !moving);
        animator.SetBool(IsMoveHash, moving);
    }

    /// <summary>
    /// attackCooldown이 지났을 때만 실제 공격(콤보 애니메이션 + 즉시 데미지 판정)을 수행한다.
    /// 별도의 타격 타이밍 지연 없이 즉시 판정한다(모션과 판정 시점을 분리하려면 PlayerController의
    /// slashEffectTriggerDelay처럼 Invoke로 지연시키면 된다).
    /// </summary>
    private void TryAttack()
    {
        if (Time.time < nextAttackTime)
        {
            return;
        }

        nextAttackTime = Time.time + attackCooldown;

        PlayAttackAnimation();
        DealDamage();
    }

    /// <summary>
    /// ComboIndex를 1로 세팅하고 Attack Layer를 켜서 Combo01 모션을 재생한 뒤,
    /// 클립 길이만큼 지나면 자동으로 되돌린다.
    /// </summary>
    private void PlayAttackAnimation()
    {
        if (animator == null)
        {
            return;
        }

        animator.SetInteger(ComboIndexHash, 1);
        SetAttackLayerWeight(1f);

        CancelInvoke(nameof(FinishAttackAnimation));
        Invoke(nameof(FinishAttackAnimation), attackAnimationDuration);
    }

    private void FinishAttackAnimation()
    {
        if (animator == null)
        {
            return;
        }

        animator.SetInteger(ComboIndexHash, 0);
        SetAttackLayerWeight(0f);
    }

    private void SetAttackLayerWeight(float weight)
    {
        if (animator == null || attackLayerIndex < 0)
        {
            return;
        }

        animator.SetLayerWeight(attackLayerIndex, weight);
    }

    /// <summary>
    /// attackHitLocalOffset/Radius 범위 안의 IDamageable 대상에게 공격력만큼 데미지를 적용한다.
    /// 자기 자신은 제외한다. PlayerController.TryDealDamage와 동일한 구조다.
    /// </summary>
    private void DealDamage()
    {
        if (combatStat == null)
        {
            return;
        }

        Vector3 origin = transform.TransformPoint(attackHitLocalOffset);
        int hitCount = Physics.OverlapSphereNonAlloc(origin, attackHitRadius, AttackHitBuffer, attackHitTargetMask, QueryTriggerInteraction.Collide);

        if (hitCount == 0)
        {
            return;
        }

        int rawDamage = CombatCalculator.CalculateAttackDamage(combatStat.AttackPower, 0);
        DamageInfo damageInfo = new DamageInfo(rawDamage, gameObject);

        for (int i = 0; i < hitCount; i++)
        {
            Collider hitCollider = AttackHitBuffer[i];
            if (hitCollider.transform.root == transform.root)
            {
                continue;
            }

            IDamageable damageable = hitCollider.GetComponentInParent<IDamageable>();
            damageable?.TakeDamage(damageInfo);
        }
    }

    /// <summary>
    /// AttackClipName(Combo01_InPlace_SingleSword) 클립의 실제 길이를 읽어둔다.
    /// 클립을 찾지 못하면 FallbackAttackClipLength를 그대로 사용한다.
    /// </summary>
    private void CacheAttackAnimationDuration()
    {
        attackAnimationDuration = FallbackAttackClipLength;

        if (animator.runtimeAnimatorController == null)
        {
            return;
        }

        foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
        {
            if (clip != null && clip.name == AttackClipName)
            {
                attackAnimationDuration = clip.length;
                break;
            }
        }
    }
    #endregion
}
