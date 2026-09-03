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

    private Animator animator;
    private Transform followTarget;
    private DragonMoveState currentMoveState = DragonMoveState.IsIdle;
    private bool isFollowing;
    #endregion

    #region LifeCycle
    private void Awake()
    {
        animator = GetComponent<Animator>();
    }

    private void Update()
    {
        float direction = ComputeFollowDirection();

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
