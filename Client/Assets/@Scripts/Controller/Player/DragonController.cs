using UnityEngine;

/// <summary>
/// 유저 캐릭터를 따라다니는 드래곤 동료의 이동/애니메이션을 담당한다.
/// 물리 기반 이동(Rigidbody)을 쓰는 PlayerController와 달리, 드래곤은 지형 충돌이나
/// 점프가 필요 없는 비행형 동료로 간주해 Transform을 직접 이동시키는 방식을 사용한다.
/// DragonMoveState(IsIdle/IsMove)에 대응하는 BasicDragonStance Animator 파라미터를 갱신한다.
/// </summary>
[RequireComponent(typeof(Animator))]
public class DragonController : MonoBehaviour
{
    #region Variable
    [Header("Follow Settings")]
    [SerializeField] private float moveSpeed = 4f;
    [Tooltip("따라갈 대상과의 거리가 이 값보다 커지면 이동을 시작한다.")]
    [SerializeField] private float followStartDistance = 2.5f;
    [Tooltip("따라갈 대상과의 거리가 이 값보다 작아지면 멈춘다. followStartDistance보다 작아야 한다(진동 방지용 히스테리시스).")]
    [SerializeField] private float followStopDistance = 1.2f;

    [Header("Facing Settings")]
    [SerializeField] private float rightFacingYRotation = 90f;
    [SerializeField] private float leftFacingYRotation = -90f;

    private static readonly int IsIdleHash = Animator.StringToHash(nameof(DragonMoveState.IsIdle));
    private static readonly int IsMoveHash = Animator.StringToHash(nameof(DragonMoveState.IsMove));
    private static readonly int SkillIndexHash = Animator.StringToHash("SkillIndex");

    /// <summary>
    /// Q/W/E/R 스킬 애니메이션이 재생 중인지 여부. 재생 중에는 따라가기 이동을 멈추고(제자리 시전 모션),
    /// 새 스킬 입력도 무시한다.
    /// </summary>
    private bool isSkillPlaying;

    /// <summary>
    /// Q(화염 브레스)/W(비행 화염 브레스)/E(포효)/R(돌진) 각 스킬 애니메이션 클립 길이.
    /// 인덱스는 DragonSkillSlot(Q/W/E/R)과 일치한다.
    /// </summary>
    private readonly float[] skillAnimationDurations = new float[4];

    private const float FallbackDragonSkillClipLength = 1f;

    private const string DragonFireClipName = "Anim_Dra_Fire";
    private const string DragonFlyFireClipName = "Anim_Dra_Fly_Fire";
    private const string DragonRoarClipName = "Anim_Dra_Roar";
    private const string DragonDashClipName = "Anim_Dra_Jump";

    /// <summary>
    /// 공격 모션이 위치한 BasicDragonStance의 별도 레이어 이름. 평소엔 weight 0으로 꺼져 있다가,
    /// 스킬 재생 중에만 1로 켜서 애니메이션이 보이도록 한다.
    /// </summary>
    private const string AttackLayerName = "Attack Layer";
    private int attackLayerIndex = -1;

    private Animator animator;
    private Transform followTarget;
    private DragonMoveState currentMoveState = DragonMoveState.IsIdle;
    private bool isFollowing;
    #endregion

    #region LifeCycle
private void Awake()
    {
        animator = GetComponent<Animator>();

        attackLayerIndex = animator.GetLayerIndex(AttackLayerName);
        if (attackLayerIndex < 0)
        {
            DebugLogController.GenerateErrorMessage<DragonController>($"Animator에 '{AttackLayerName}' 레이어가 없어 스킬 모션이 표시되지 않을 수 있습니다.");
        }

        CacheSkillAnimationDurations();
    }

private void Update()
    {
        float direction = isSkillPlaying ? 0f : ComputeFollowDirection();

        if (direction != 0f)
        {
            transform.Translate(Vector3.right * direction * moveSpeed * Time.deltaTime, Space.World);
            UpdateFacingDirection(direction);
        }

        UpdateMoveAnimation(direction);
    }
    #endregion

    #region Method
    /// <summary>
    /// 이 드래곤이 따라다닐 대상(보통 유저 캐릭터)을 지정한다. GameSceneController가
    /// 캐릭터/드래곤 스폰(둘 다 독립적인 비동기 Addressable 로드)이 모두 끝난 뒤 호출해준다.
    /// </summary>
    public void SetFollowTarget(Transform target)
    {
        followTarget = target;
    }


    /// <summary>
    /// slot(Q/W/E/R)에 해당하는 스킬 애니메이션을 BasicDragonStance의 Attack Layer에서 재생한다.
    /// 이미 다른 스킬이 재생 중이면 무시한다. GameSceneController가
    /// UI_GameSceneView.TryStartDragonSkillCooldown이 성공했을 때만 호출한다.
    /// </summary>
    public void PlaySkillAnimation(DragonSkillSlot slot)
    {
        if (animator == null || isSkillPlaying)
        {
            return;
        }

        int index = (int)slot;
        if (index < 0 || index >= skillAnimationDurations.Length)
        {
            return;
        }

        isSkillPlaying = true;
        animator.SetInteger(SkillIndexHash, index + 1);
        SetAttackLayerWeight(1f);

        CancelInvoke(nameof(FinishSkillAnimation));
        Invoke(nameof(FinishSkillAnimation), skillAnimationDurations[index]);
    }

    /// <summary>
    /// 스킬 애니메이션 재생이 끝난 뒤 SkillIndex를 0으로 되돌려 AttackLayerIdle로
    /// 복귀시키고, Attack Layer weight도 0으로 되돌린다.
    /// </summary>
    private void FinishSkillAnimation()
    {
        isSkillPlaying = false;

        if (animator != null)
        {
            animator.SetInteger(SkillIndexHash, 0);
        }

        SetAttackLayerWeight(0f);
    }

    /// <summary>
    /// BasicDragonStance의 Attack Layer weight를 설정한다. 스킬 재생 중에만 1로 켜서
    /// 스킬 모션이 Base Layer(Idle/Move/Jump) 위에 덮여이게 하고, 평소엔 0으로 되돌려 Base Layer만 보인다.
    /// </summary>
    private void SetAttackLayerWeight(float weight)
    {
        if (animator == null || attackLayerIndex < 0)
        {
            return;
        }

        animator.SetLayerWeight(attackLayerIndex, weight);
    }

    /// <summary>
    /// Q/W/E/R 각 스킬 클립 길이를 미리 읽어둔다. 클립을 찾지 못하면 FallbackDragonSkillClipLength로 대체한다.
    /// </summary>
    private void CacheSkillAnimationDurations()
    {
        for (int i = 0; i < skillAnimationDurations.Length; i++)
        {
            skillAnimationDurations[i] = FallbackDragonSkillClipLength;
        }

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

            if (clip.name == DragonFireClipName)
            {
                skillAnimationDurations[(int)DragonSkillSlot.Q] = clip.length;
            }
            else if (clip.name == DragonFlyFireClipName)
            {
                skillAnimationDurations[(int)DragonSkillSlot.W] = clip.length;
            }
            else if (clip.name == DragonRoarClipName)
            {
                skillAnimationDurations[(int)DragonSkillSlot.E] = clip.length;
            }
            else if (clip.name == DragonDashClipName)
            {
                skillAnimationDurations[(int)DragonSkillSlot.R] = clip.length;
            }
        }
    }


    /// <summary>
    /// followTarget과의 X축 거리를 기준으로 이동 방향(-1/0/1)을 계산한다.
    /// followStartDistance/followStopDistance 사이의 히스테리시스로, 목표 지점 근처에서
    /// 이동과 정지가 매 프레임 번갈아 일어나며 떨리는 현상을 막는다.
    /// </summary>
    private float ComputeFollowDirection()
    {
        if (followTarget == null)
        {
            isFollowing = false;
            return 0f;
        }

        float deltaX = followTarget.position.x - transform.position.x;
        float distance = Mathf.Abs(deltaX);

        isFollowing = isFollowing ? distance > followStopDistance : distance > followStartDistance;

        if (!isFollowing)
        {
            return 0f;
        }

        return Mathf.Sign(deltaX);
    }

    /// <summary>
    /// 이동 방향에 맞춰 Y축 회전으로 좌우를 바라보게 한다. PlayerController의 UpdateFacingDirection과 동일한 규칙.
    /// </summary>
    private void UpdateFacingDirection(float direction)
    {
        float yRotation = direction > 0f ? rightFacingYRotation : leftFacingYRotation;
        transform.rotation = Quaternion.Euler(0f, yRotation, 0f);
    }

    /// <summary>
    /// 이동 여부로 DragonMoveState(IsIdle/IsMove)를 판정하고, 상태가 바뀔 때만
    /// BasicDragonStance Animator의 IsIdle/IsMove bool 파라미터를 갱신한다.
    /// </summary>
    private void UpdateMoveAnimation(float direction)
    {
        if (animator == null)
        {
            return;
        }

        DragonMoveState newState = direction != 0f ? DragonMoveState.IsMove : DragonMoveState.IsIdle;

        if (newState == currentMoveState)
        {
            return;
        }

        currentMoveState = newState;

        animator.SetBool(IsIdleHash, newState == DragonMoveState.IsIdle);
        animator.SetBool(IsMoveHash, newState == DragonMoveState.IsMove);
    }
    #endregion
}
