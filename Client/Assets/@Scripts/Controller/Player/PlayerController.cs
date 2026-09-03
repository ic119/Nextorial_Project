using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 키보드 좌우 화살표 입력을 받아 Rigidbody를 통해 캐릭터를 X축으로 이동시킨다.
/// 중력/충돌은 Rigidbody가 처리하므로, Y·Z는 건드리지 않고 X 속도만 제어한다.
/// 좌우 입력 방향에 맞춰 캐릭터가 해당 방향을 바라보도록 Y축 회전도 함께 갱신하고,
/// PlayerMoveState(IsIdle/IsMove)와 IsJump에 따라 BasicCharacterStance Animator 파라미터를 갱신한다.
/// SpaceBar 입력 시 접지 상태에서만 위로 속도를 부여해 중력에 의한 포물선 점프를 만든다.
/// F 입력 시 PlayerCharacterModel에 장착된 무기 타입에 맞는 콤보 공격을 진행한다.
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
    [System.Serializable]
    private struct SlashEffectPose
    {
        public Vector3 localOffset;
        public Vector3 localEulerAngles;
    }

    [Header("Attack VFX")]
    [Tooltip("콤보 스텝(1부터 시작)별 스래시 이펙트 배치. 인덱스 0 = 1콤보, 인덱스 1 = 2콤보. 배열 길이를 넘는 스텝은 마지막 값을 재사용한다. 위치/회전 모두 transform 기준 로컬 값이라 캐릭터가 왜쪽/오른쪽 어느 쪽을 보든 자동 반영된다.")]
    [SerializeField]
    private SlashEffectPose[] slashEffectPosesByComboStep =
    {
        new SlashEffectPose { localOffset = new Vector3(0.75f, 1f, 1f), localEulerAngles = new Vector3(0f, 180f, 90f) },
        new SlashEffectPose { localOffset = new Vector3(0.75f, 0.65f, 1f), localEulerAngles = new Vector3(1f, 180f, 5f) }
    };
    [Tooltip("콤보 입력 시점부터 스래시 이펙트를 터뜨리까지의 지연 시간(초). 애니메이션 이벤트 대신 이 값으로 스윈 타이밍에 맞춰 튜닝한다.")]
    [SerializeField] private float slashEffectTriggerDelay = 0.12f;

    private const string SlashEffectKey = "Slash_Normal";
    private const int SlashEffectPrewarmCount = 3;
    private int pendingSlashEffectComboStep;



    private const string JumpClipName = "JumpFull_RM_NoWeapon";
    private const float FallbackJumpClipLength = 0.8f;
    private const float MinJumpSpeedMultiplier = 0.3f;
    private const float MaxJumpSpeedMultiplier = 3f;
    private const float JumpAscentVelocityThreshold = 0.01f;

    /// <summary>
    /// SingleSword 콤보 애니메이션 클립 이름(콤보 단계 순서). BasicCharacterStance의
    /// Attack1/Attack2 상태에 연결된 모션과 이름이 일치해야 한다.
    /// 다른 무기 타입을 추가할 때는 이 배열과 같은 형태로 세트를 하나 더 만들면 된다.
    /// </summary>
    private static readonly string[] SingleSwordComboClipNames =
    {
        "Combo01_InPlace_SingleSword",
        "Combo02_InPlace_SingleSword"
    };

    private const float FallbackComboClipLength = 0.6f;
    private const int MaxComboStep = 2;

    private static readonly int IsIdleHash = Animator.StringToHash(nameof(PlayerMoveState.IsIdle));
    private static readonly int IsMoveHash = Animator.StringToHash(nameof(PlayerMoveState.IsMove));
    private static readonly int IsJumpHash = Animator.StringToHash(nameof(PlayerMoveState.IsJump));
    private static readonly int JumpSpeedHash = Animator.StringToHash("JumpSpeed");
    private static readonly int ComboIndexHash = Animator.StringToHash("ComboIndex");

    private Rigidbody rb;
    private Animator animator;
    private PlayerCharacterModel characterModel;
    private PlayerMoveState currentMoveState = PlayerMoveState.IsIdle;
    private bool jumpRequested;
    private bool isGrounded = true;
    private bool isJumpAnimating;

    private bool attackRequested;
    private int comboStep;
    private float comboWindowEndTime;
    private readonly float[] comboClipLengths = new float[MaxComboStep];
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
        characterModel = GetComponent<PlayerCharacterModel>();

        if (characterModel == null)
        {
            DebugLogController.GenerateErrorMessage<PlayerController>("PlayerCharacterModel이 없어 장착된 무기 타입을 확인할 수 없어 콤보 공격이 비활성화됩니다.");
        }

        if (animator == null)
        {
            DebugLogController.GenerateErrorMessage<PlayerController>("Animator 컴포넌트가 없어 이동 애니메이션 파라미터를 갱신할 수 없습니다.");
        }
        else
        {
            ApplyJumpAnimationSpeed();
            CacheComboClipLengths();
        }

        ObjectPoolController.Instance?.Preload(SlashEffectKey, SlashEffectPrewarmCount);
    }

    private void Update()
    {
        // FixedUpdate보다 프레임이 더 자주 도는 Update에서 눌림을 감지해 다음 FixedUpdate까지 요청을 보관한다.
        // FixedUpdate에서만 폴링하면 짧게 누른 입력이 프레임 사이에 씹힐 수 있다.
        if (Keyboard.current == null)
        {
            return;
        }

        if (Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            jumpRequested = true;
        }

        if (Keyboard.current.fKey.wasPressedThisFrame)
        {
            attackRequested = true;
        }
    }

    private void FixedUpdate()
    {
        float direction = GetHorizontalInput();

        isGrounded = CheckGrounded();

        Vector3 velocity = rb.linearVelocity;
        // 콤보 공격 중에는 제자리에서 휘두르는 모션(_InPlace_)이므로 좌우 이동을 멈춘다.
        velocity.x = comboStep > 0 ? 0f : direction * moveSpeed;

        if (jumpRequested)
        {
            jumpRequested = false;

            // 콤보 공격 중에는 점프를 막아 Attack 상태와 Jump 상태가 동시에 요구되는
            // 상황(애니메이터 충돌) 자체가 생기지 않도록 한다.
            if (isGrounded && comboStep == 0)
            {
                velocity.y = jumpForce;
                isGrounded = false;
            }
        }

        rb.linearVelocity = velocity;

        UpdateFacingDirection(direction);
        UpdateMoveAnimation(direction);
        UpdateJumpAnimation();
        UpdateCombo();
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

    /// <summary>
    /// 콤보 단계별 클립 길이를 미리 읽어둔다. 각 단계의 콤보 유예 시간(다음 입력을 받아줄 창)을
    /// 해당 단계 클립 길이만큼으로 잡는 데 쓴다. 클립을 찾지 못하면 FallbackComboClipLength로 대체한다.
    /// </summary>
    private void CacheComboClipLengths()
    {
        for (int i = 0; i < comboClipLengths.Length; i++)
        {
            comboClipLengths[i] = FallbackComboClipLength;
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

            for (int i = 0; i < SingleSwordComboClipNames.Length; i++)
            {
                if (clip.name == SingleSwordComboClipNames[i])
                {
                    comboClipLengths[i] = clip.length;
                }
            }
        }
    }

    /// <summary>
    /// F 입력을 콤보 단계(ComboIndex)로 변환한다.
    /// - 대기 중(comboStep == 0)이고 접지 상태일 때만 콤보를 시작한다.
    /// - 콤보 진행 중 다음 입력이 현재 단계의 유예 시간(comboWindowEndTime) 안에 들어오면 다음 단계로 이어간다.
    /// - 유예 시간을 넘기면 자동으로 대기 상태로 되돌아간다(ComboIndex = 0).
    /// 장착된 무기 타입은 PlayerCharacterModel이 갖고 있으며, 여기서는 참조만 한다.
    /// 현재는 SingleSword 클립 세트만 있어 다른 무기 타입일 때는 콤보를 시작하지 않는다.
    /// </summary>
private void UpdateCombo()
    {
        if (animator == null)
        {
            return;
        }

        if (comboStep > 0 && Time.time > comboWindowEndTime)
        {
            comboStep = 0;
            animator.SetInteger(ComboIndexHash, 0);
        }

        if (!attackRequested)
        {
            return;
        }

        attackRequested = false;

        WeaponType equippedWeaponType = characterModel != null ? characterModel.CurrentWeaponType : WeaponType.NoWeapon;
        if (equippedWeaponType != WeaponType.OneHanded)
        {
            return;
        }

        int nextStep;
        if (comboStep == 0)
        {
            if (!isGrounded)
            {
                return;
            }

            nextStep = 1;
        }
        else if (comboStep < MaxComboStep && Time.time <= comboWindowEndTime)
        {
            nextStep = comboStep + 1;
        }
        else
        {
            return;
        }

        comboStep = nextStep;
        comboWindowEndTime = Time.time + comboClipLengths[nextStep - 1];
        animator.SetInteger(ComboIndexHash, comboStep);

        pendingSlashEffectComboStep = comboStep;
        if (slashEffectTriggerDelay > 0f)
        {
            Invoke(nameof(PlayAttackSlashEffect), slashEffectTriggerDelay);
        }
        else
        {
            PlayAttackSlashEffect();
        }
    }
    #endregion


/// <summary>
    /// 현재 캐릭터 앞쪽(rightFacingYRotation/leftFacingYRotation이 반영된 transform.rotation 기준)에
    /// slashEffectLocalOffset만큼 띄운 위치에 풀링된 Slash_Normal 이펙트를 소환한다.
    /// UpdateCombo에서 콤보 입력이 성공할 때마다 slashEffectTriggerDelay후 호출된다.
    /// </summary>
private void PlayAttackSlashEffect()
    {
        if (ObjectPoolController.Instance == null)
        {
            return;
        }

        SlashEffectPose pose = GetSlashEffectPose(pendingSlashEffectComboStep);

        Vector3 spawnPosition = transform.TransformPoint(pose.localOffset);
        Quaternion spawnRotation = transform.rotation * Quaternion.Euler(pose.localEulerAngles);

        ObjectPoolController.Instance.Get(SlashEffectKey, spawnPosition, spawnRotation);
    }


/// <summary>
    /// comboStepValue(1부터 시작)에 대응하는 스래시 이펙트 배치를 가져온다.
    /// 배열 길이를 넘어서는 콤보 스텝은 마지막 값을 그대로 재사용한다.
    /// </summary>
    private SlashEffectPose GetSlashEffectPose(int comboStepValue)
    {
        if (slashEffectPosesByComboStep == null || slashEffectPosesByComboStep.Length == 0)
        {
            return default;
        }

        int index = Mathf.Clamp(comboStepValue - 1, 0, slashEffectPosesByComboStep.Length - 1);
        return slashEffectPosesByComboStep[index];
    }
}
