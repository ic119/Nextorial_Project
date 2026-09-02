using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 키보드 좌우 화살표 입력을 받아 Rigidbody를 통해 캐릭터를 X축으로 이동시킨다.
/// 중력/충돌은 Rigidbody가 처리하므로, Y·Z는 건드리지 않고 X 속도만 제어한다.
/// 좌우 입력 방향에 맞춰 캐릭터가 해당 방향을 바라보도록 Y축 회전도 함께 갱신하고,
/// PlayerMoveState(IsIdle/IsMove)와 IsJump에 따라 BasicCharacterStance Animator 파라미터를 갱신한다.
/// SpaceBar 입력 시 접지 상태에서만 위로 속도를 부여해 중력에 의한 포물선 점프를 만든다.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    #region Variable
    [SerializeField] private float moveSpeed = 5f;

    [Header("Facing Settings")]
    [SerializeField] private float rightFacingYRotation = 90f;
    [SerializeField] private float leftFacingYRotation = -90f;

    [Header("Jump Settings")]
    [SerializeField] private float jumpForce = 4f;
    [SerializeField] private float groundCheckOriginOffset = 0.1f;
    [SerializeField] private float groundCheckDistance = 0.2f;
    [SerializeField] private LayerMask groundLayerMask = ~0;

    private const string JumpClipName = "JumpFull_RM_NoWeapon";
    private const float FallbackJumpClipLength = 0.8f;
    private const float MinJumpSpeedMultiplier = 0.3f;
    private const float MaxJumpSpeedMultiplier = 3f;
    private const float JumpAscentVelocityThreshold = 0.01f;

    private static readonly int IsIdleHash = Animator.StringToHash(nameof(PlayerMoveState.IsIdle));
    private static readonly int IsMoveHash = Animator.StringToHash(nameof(PlayerMoveState.IsMove));
    private static readonly int IsJumpHash = Animator.StringToHash(nameof(PlayerMoveState.IsJump));
    private static readonly int JumpSpeedHash = Animator.StringToHash("JumpSpeed");

    private Rigidbody rb;
    private Animator animator;
    private PlayerMoveState currentMoveState = PlayerMoveState.IsIdle;
    private bool jumpRequested;
    private bool isGrounded = true;
    private bool isJumpAnimating;
    #endregion

    #region Property
    /// <summary>현재 X축 이동 속력(부호 없음). 카메라 연출 등 외부에서 이동 여부 판단에 사용한다.</summary>
    public float CurrentSpeed => rb != null ? Mathf.Abs(rb.linearVelocity.x) : 0f;
    #endregion

    #region LifeCycle
    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        animator = GetComponent<Animator>();

        if (animator == null)
        {
            DebugLogController.GenerateErrorMessage<PlayerController>("Animator 컴포넌트가 없어 이동 애니메이션 파라미터를 갱신할 수 없습니다.");
        }
        else
        {
            ApplyJumpAnimationSpeed();
        }
    }

    private void Update()
    {
        // FixedUpdate보다 프레임이 더 자주 도는 Update에서 눌림을 감지해 다음 FixedUpdate까지 요청을 보관한다.
        // FixedUpdate에서만 폴링하면 짧게 누른 SpaceBar 입력이 프레임 사이에 씹힐 수 있다.
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            jumpRequested = true;
        }
    }

    private void FixedUpdate()
    {
        float direction = GetHorizontalInput();

        isGrounded = CheckGrounded();

        Vector3 velocity = rb.linearVelocity;
        velocity.x = direction * moveSpeed;

        if (jumpRequested)
        {
            jumpRequested = false;

            if (isGrounded)
            {
                velocity.y = jumpForce;
                isGrounded = false;
            }
        }

        rb.linearVelocity = velocity;

        UpdateFacingDirection(direction);
        UpdateMoveAnimation(direction);
        UpdateJumpAnimation();
    }
    #endregion

    #region Method
    private static float GetHorizontalInput()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return 0f;
        }

        float direction = 0f;

        if (keyboard.leftArrowKey.isPressed)
        {
            direction -= 1f;
        }

        if (keyboard.rightArrowKey.isPressed)
        {
            direction += 1f;
        }

        return direction;
    }

    /// <summary>
    /// 캐릭터 발 위치(로컬 y=0) 바로 위에서 아래로 짧게 Raycast를 쏴 접지 여부를 판정한다.
    /// Ray 시작점이 캡슐 콜라이더 내부에 있어도 Unity의 Raycast는 자기 자신의 콜라이더를
    /// 감지하지 않으므로 별도의 자기 제외 처리는 필요 없다.
    /// </summary>
    private bool CheckGrounded()
    {
        // 상승 중(점프 직후)에는 아직 Raycast 사거리 안에 캐릭터가 있어도 접지 상태가 아니다.
        // 위쪽 속도가 남아있는 동안은 무조건 false로 처리해, 착지 오탐으로 Jump 애니메이션이
        // 한 번 끊겼다가 재생되어 더블 점프처럼 보이는 현상을 막는다.
        if (rb.linearVelocity.y > JumpAscentVelocityThreshold)
        {
            return false;
        }

        Vector3 origin = transform.position + Vector3.up * groundCheckOriginOffset;
        float distance = groundCheckOriginOffset + groundCheckDistance;

        return Physics.Raycast(origin, Vector3.down, distance, groundLayerMask, QueryTriggerInteraction.Ignore);
    }

    /// <summary>
    /// 좌우 입력 방향에 맞춰 캐릭터를 회전시킨다. 입력이 없을 때(direction == 0)는
    /// 마지막으로 바라보던 방향을 그대로 유지한다.
    /// </summary>
    private void UpdateFacingDirection(float direction)
    {
        if (direction == 0f)
        {
            return;
        }

        float yRotation = direction > 0f ? rightFacingYRotation : leftFacingYRotation;
        rb.MoveRotation(Quaternion.Euler(0f, yRotation, 0f));
    }

    /// <summary>
    /// 입력 방향으로 PlayerMoveState(IsIdle/IsMove)를 판정하고, 상태가 바뀔 때만
    /// BasicCharacterStance Animator의 IsIdle/IsMove bool 파라미터를 갱신한다.
    /// </summary>
    private void UpdateMoveAnimation(float direction)
    {
        if (animator == null)
        {
            return;
        }

        PlayerMoveState newState = direction != 0f ? PlayerMoveState.IsMove : PlayerMoveState.IsIdle;

        if (newState == currentMoveState)
        {
            return;
        }

        currentMoveState = newState;

        animator.SetBool(IsIdleHash, newState == PlayerMoveState.IsIdle);
        animator.SetBool(IsMoveHash, newState == PlayerMoveState.IsMove);
    }

    /// <summary>
    /// IsJump는 IsIdle/IsMove와 달리 이동 상태와 동시에 참일 수 있는 독립적인 플래그다
    /// (예: 이동하며 점프). 접지 여부가 바뀔 때만 Animator에 반영한다.
    /// </summary>
    private void UpdateJumpAnimation()
    {
        if (animator == null)
        {
            return;
        }

        bool jumping = !isGrounded;

        if (jumping == isJumpAnimating)
        {
            return;
        }

        isJumpAnimating = jumping;
        animator.SetBool(IsJumpHash, jumping);
    }

    /// <summary>
    /// 점프 애니메이션 클립의 원래 길이를 물리 기반 예상 체공 시간(위로 오를 때와 내려올 때가
    /// 대칭이라고 가정한 2 * jumpForce / g)에 맞도록 Jump 상태의 재생 속도(JumpSpeed 파라미터)를
    /// 계산해 반영한다. jumpForce는 런타임에 바뀌지 않으므로 Awake에서 한 번만 계산하면 충분하다.
    /// Animator에서 클립을 찾지 못하면 FallbackJumpClipLength(현재 클립 실측값)로 대체한다.
    /// </summary>
    private void ApplyJumpAnimationSpeed()
    {
        float clipLength = FallbackJumpClipLength;

        if (animator.runtimeAnimatorController != null)
        {
            foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
            {
                if (clip != null && clip.name == JumpClipName)
                {
                    clipLength = clip.length;
                    break;
                }
            }
        }

        float gravity = Mathf.Abs(Physics.gravity.y);
        if (gravity <= 0f || clipLength <= 0f || jumpForce <= 0f)
        {
            return;
        }

        float expectedAirTime = 2f * jumpForce / gravity;
        float speedMultiplier = Mathf.Clamp(clipLength / expectedAirTime, MinJumpSpeedMultiplier, MaxJumpSpeedMultiplier);

        animator.SetFloat(JumpSpeedHash, speedMultiplier);
    }
    #endregion
}
