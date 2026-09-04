using UnityEngine;

/// <summary>
/// KeyboardInputController로부터 좌우 이동 축과 점프/공격 이벤트를 받아 Rigidbody를 통해 캐릭터를 X축으로 이동시킨다.
/// 중력/충돌은 Rigidbody가 처리하므로, Y·Z는 건드리지 않고 X 속도만 제어한다.
/// 좌우 입력 방향에 맞춰 캐릭터가 해당 방향을 바라보도록 Y축 회전도 함께 갱신하고,
/// PlayerMoveState(IsIdle/IsMove)와 IsJump에 따라 BasicCharacterStance Animator 파라미터를 갱신한다.
/// SpaceBar 입력 시 접지 상태에서만 위로 속도를 부여해 중력에 의한 포물선 점프를 만든다.
/// C키 입력 시 PlayerCharacterModel에 장착된 무기 타입에 맞는 콤보 공격을 진행한다.
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
        new SlashEffectPose { localOffset = new Vector3(0f, 1f, 1f), localEulerAngles = new Vector3(0f, 180f, 90f) },
        new SlashEffectPose { localOffset = new Vector3(0f, 0.65f, 1f), localEulerAngles = new Vector3(1f, 180f, 5f) }
    };
    [Tooltip("콤보 입력 시점부터 스래시 이펙트를 터뜨리까지의 지연 시간(초). 애니메이션 이벤트 대신 이 값으로 스윈 타이밍에 맞춰 튜닝한다.")]
    [SerializeField] private float slashEffectTriggerDelay = 0.12f;

    private const string SlashEffectKey = "SlashNormal";
    private const int SlashEffectPrewarmCount = 3;
    private int pendingSlashEffectComboStep;

    /// <summary>
    /// D(속성베기) 스킬 사용 시, 이 시간 동안 기본 공격(콤보) 이펙트가 SlashFireForceEffectKey로 바뀓다.
    /// </summary>
    private const string SlashFireForceEffectKey = "SlashFireForce";
    private const float FireForceBuffDuration = 8f;

    /// <summary>
    /// F(회전베기) 스킬 사용 시 사용하는 이펙트 Addressable 키.
    /// </summary>
    private const string WheelWindEffectKey = "WheelWindNormal";

    /// <summary>
    /// 스킬 이펙트를 애니메이션 종료 시간에 맞춰야 할 때(예: F/WheelWind), 목표 재생 시간이 이 값 미만으로 내려가지 않도록(0 나누기 방지) 하단을 설정한다.
    /// </summary>
    private const float MinEffectSyncDuration = 0.05f;

    /// <summary>
    /// SyncParticleDurationToTarget이 계산하는 simulationSpeed의 허용 범위. 애니메이션이 극단적으로 짧아도/길어도
    /// 이펙트가 순간 사라지거나(과도하게 빨라짐) 정지된 듯 보이지(과도하게 느려짐) 않도록 clamp한다.
    /// </summary>
    private const float MinEffectSimulationSpeed = 0.1f;
    private const float MaxEffectSimulationSpeed = 10f;

    /// <summary>
    /// 기본 공격(콤보)이 실제로 사용하는 이펙트 키. 평소엔 SlashEffectKey이고, D 스킬 사용 직후
    /// FireForceBuffDuration(8초) 동안만 SlashFireForceEffectKey로 바뀌었다가 자동으로 되돌아간다.
    /// </summary>
    private string currentComboEffectKey = SlashEffectKey;


    [Header("Skill VFX")]
    [Tooltip("스킬 슬롯(A/S/D/F 순서)별 이펙트 배치. 위치/회전 모두 transform 기준 로컬 값이라 캐릭터가 왜쪽/오른쪽 어느 쪽을 보든 자동 반영된다.")]
    [SerializeField]
    private SlashEffectPose[] skillEffectPosesBySlot =
    {
        new SlashEffectPose { localOffset = new Vector3(0f, 0.65f, 1f), localEulerAngles = new Vector3(0f, 180f, 90f) },
        new SlashEffectPose { localOffset = new Vector3(0f, 0.65f, 1f), localEulerAngles = new Vector3(0f, 180f, 90f) },
        new SlashEffectPose { localOffset = new Vector3(0f, 0f, 0f), localEulerAngles = new Vector3(0f, 180f, 0f) },
        new SlashEffectPose { localOffset = new Vector3(0f, 0.65f, 0f), localEulerAngles = new Vector3(0f, 180f, 0f) }
    };
    [Tooltip("스킬 시전 시점부터 이펙트를 터뜨리까지의 지연 시간(초).")]
    [SerializeField] private float skillEffectTriggerDelay = 0.12f;
    [Tooltip("스킬 슬롯(A/S/D/F 순서)별로 사용할 이펙트의 Addressable 키. 기본은 콤보와 동일한 Slash_Normal이며, 슬롯마다 다른 이펙트를 쓰려면 값을 바꾸면 된다(단, 그 키가 Addressable로 등록되어 있어야 한다).")]
    [SerializeField]
    private string[] skillEffectKeysBySlot =
    {
        SlashEffectKey,
        SlashEffectKey,
        SlashEffectKey, // D: AttributeAssignmentEffect는 루프 파티클이라 풀링 대신 attributeAssignmentEffectInstance.SetActive로 별도 관리한다.
        WheelWindEffectKey
    };


    private UI_GameSceneView.PlayerSkillSlot pendingSkillSlot;

    private const string AttributeAssignmentEffectKey = "AttributeAssignmentEffect";

    /// <summary>
    /// D(속성베기) 사용 중(FireForceBuffDuration 동안) 켜지는 지속형 오라 이펙트 인스턴스.
    /// 파티클이 전부 looping이라 원샷 풀링(SpawnSlashEffect/ObjectPoolController)과 맞지 않아
    /// 캐릭터 자식으로 한 번만 만들어두고 SetActive만 토글한다.
    /// </summary>
    private GameObject attributeAssignmentEffectInstance;
    private ParticleSystem attributeAssignmentRootParticle;






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
    private static readonly int SkillIndexHash = Animator.StringToHash("SkillIndex");

    /// <summary>
    /// A/S/D/F 스킬 애니메이션이 재생 중인지 여부. 콤보 공격과 스킬은 같은 Attack Layer를 공유하므로
    /// 서로 동시에 진입하지 못하도록 이 플래그와 comboStep으로 상호 배제한다.
    /// </summary>
    private bool isSkillPlaying;

    /// <summary>
    /// A(찌르기)/D(속성베기)/F(회전베기)는 단일 클립, S(삼단베기)는 Combo01~03을 순차
    /// 재생하므로 세 클립 길이의 합을 총 재생 시간으로 캩0싱해둔다. 인덱스는 UI_GameSceneView.PlayerSkillSlot과 일치한다.
    /// </summary>
    private const string SwordPierceClipName = "Combo03_InPlace_SingleSword";
    private const string AttributeAssignmentClipName = "Defend_SingleSword";
    private const string WheelwindClipName = "Combo04_InPlace_SingleSword";
    private static readonly string[] TripleSlashClipNames =
    {
        "Combo01_InPlace_SingleSword",
        "Combo02_InPlace_SingleSword",
        "Combo03_InPlace_SingleSword"
    };

    private const float FallbackSkillClipLength = 0.6f;
    private readonly float[] skillAnimationDurations = new float[4];

    /// <summary>
    /// S(삼단베기)를 구성하는 Combo01~03 각 클립의 개별 길이. 타격마다 이펙트를 나눈 소환할 때
    /// 각 타격이 시작되는 시점(누적 시간)을 계산하는 데 쓰인다.
    /// </summary>
    private readonly float[] tripleSlashClipLengths = new float[TripleSlashClipNames.Length];


    /// <summary>
    /// 공격 모션(Attack1/Attack2)이 위치한 BasicCharacterStance의 별도 레이어 이름.
    /// 평소엔 weight 0으로 까거져 있다가, 콤보 중(comboStep > 0)에만 1로 켜서 공격 모션이 보이도록 한다.
    /// </summary>
    private const string AttackLayerName = "Attack Layer";
    private int attackLayerIndex = -1;


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

    [Tooltip("마지막 콤보 단계(MaxComboStep) 공격 이후, 새 콤보를 다시 시작할 수 있을 때까지의 대기 시간(초).")]
    [SerializeField] private float postComboLockoutDuration = 0.5f;
    private float postComboLockoutEndTime;

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
            CacheSkillAnimationDurations();

            attackLayerIndex = animator.GetLayerIndex(AttackLayerName);
            if (attackLayerIndex < 0)
            {
                DebugLogController.GenerateErrorMessage<PlayerController>($"Animator에 '{AttackLayerName}' 레이어가 없어 공격 모션이 표시되지 않을 수 있습니다.");
            }
        }

        if (KeyboardInputController.Instance != null)
        {
            KeyboardInputController.Instance.OnJumpPressed += HandleJumpPressed;
            KeyboardInputController.Instance.OnAttackPressed += HandleAttackPressed;
        }
        else
        {
            DebugLogController.GenerateErrorMessage<PlayerController>("KeyboardInputController.Instance가 없어 점프/공격 입력을 받을 수 없습니다.");
        }

        PreloadEffectPools();
        LoadAttributeAssignmentEffect();
    }

    private void OnDestroy()
    {
        if (KeyboardInputController.Instance != null)
        {
            KeyboardInputController.Instance.OnJumpPressed -= HandleJumpPressed;
            KeyboardInputController.Instance.OnAttackPressed -= HandleAttackPressed;
        }
    }




    private void FixedUpdate()
    {
        float direction = KeyboardInputController.Instance != null ? KeyboardInputController.Instance.MoveAxis : 0f;

        isGrounded = CheckGrounded();

        Vector3 velocity = rb.linearVelocity;
        // 콤보 공격/스킬 재생 중에는 제자리에서 동작하는 모션이므로 좌우 이동을 멈추다.
        velocity.x = (comboStep > 0 || isSkillPlaying) ? 0f : direction * moveSpeed;

        if (jumpRequested)
        {
            jumpRequested = false;

            // 콤보 공격/스킬 중에는 점프를 막아 Attack 상태와 Jump 상태가 동시에 요구되는
            // 상황(애니메이터 충돌) 자체가 생기지 않도록 한다.
            if (isGrounded && comboStep == 0 && !isSkillPlaying)
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
    /// C키 입력을 콤보 단계(ComboIndex)로 변환한다.
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

            if (!isSkillPlaying)
            {
                SetAttackLayerWeight(0f);
            }

            // 리셋 직후 같은 프레임에서 곰바로 새 콤보를 시작하면 Animator가 ComboIndex의
            // 변화(예: 1 -> 0 -> 1)를 감지하지 못해 공격 이펙트는 나오지만 애니메이션은
            // 재생되지 않는 문제가 있었다. 리셋과 신규 공격 판정 사이에 최소 한 프레임을 두어
            // (attackRequested는 그대로 유지되므로 다음 FixedUpdate에서 정상 처리된다) 이 문제를 막는다.
            return;
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
            // 마지막 콤보 공격 직후에는 postComboLockoutEndTime까지 새 콤보를 다시 시작할 수 없고,
            // 스킬(A/S/D/F)이 재생 중일 때도 같은 Attack Layer를 쓰므로 콤보를 시작하지 않는다.
            if (!isGrounded || Time.time < postComboLockoutEndTime || isSkillPlaying)
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

        if (nextStep == MaxComboStep)
        {
            postComboLockoutEndTime = comboWindowEndTime + postComboLockoutDuration;
        }

        animator.SetInteger(ComboIndexHash, comboStep);
        SetAttackLayerWeight(1f);

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
        SlashEffectPose pose = GetSlashEffectPose(pendingSlashEffectComboStep);
        SpawnSlashEffect(pose, currentComboEffectKey);
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


/// <summary>
    /// BasicCharacterStance의 Attack Layer weight를 설정한다. 공격 중에만 1로 켜서 Attack1/Attack2
    /// 모션이 Base Layer(Idle/Move/Jump) 위에 덮여율게 하고, 평소엔 0으로 되돌려 Base Layer만 보인다.
    /// </summary>
    private void SetAttackLayerWeight(float weight)
    {
        if (animator == null || attackLayerIndex < 0)
        {
            return;
        }

        animator.SetLayerWeight(attackLayerIndex, weight);
    }


/// <summary>KeyboardInputController의 점프 입력 이벤트 핸들러. 다음 FixedUpdate에서 처리되도록 요청만 기록한다.</summary>
    private void HandleJumpPressed()
    {
        jumpRequested = true;
    }

    /// <summary>KeyboardInputController의 공격 입력 이벤트 핸들러. 다음 FixedUpdate에서 처리되도록 요청만 기록한다.</summary>
    private void HandleAttackPressed()
    {
        attackRequested = true;
    }


/// <summary>
    /// A/D/F 스킬 클립 길이와, S(삼단베기)를 구성하는 Combo01~03 클립 길이의 합을 미리 읽어둔다.
    /// 이 값만큼 뒤에 SkillIndex를 0으로 되돌려 애니메이션이 끝나는 시점과 맞춘다.
    /// </summary>
private void CacheSkillAnimationDurations()
    {
        for (int i = 0; i < skillAnimationDurations.Length; i++)
        {
            skillAnimationDurations[i] = FallbackSkillClipLength;
        }

        for (int i = 0; i < tripleSlashClipLengths.Length; i++)
        {
            tripleSlashClipLengths[i] = FallbackComboClipLength;
        }

        if (animator.runtimeAnimatorController == null)
        {
            return;
        }

        float tripleSlashTotal = 0f;

        foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
        {
            if (clip == null)
            {
                continue;
            }

            if (clip.name == SwordPierceClipName)
            {
                skillAnimationDurations[(int)UI_GameSceneView.PlayerSkillSlot.A] = clip.length;
            }
            else if (clip.name == AttributeAssignmentClipName)
            {
                skillAnimationDurations[(int)UI_GameSceneView.PlayerSkillSlot.D] = clip.length;
            }
            else if (clip.name == WheelwindClipName)
            {
                skillAnimationDurations[(int)UI_GameSceneView.PlayerSkillSlot.F] = clip.length;
            }

            for (int i = 0; i < TripleSlashClipNames.Length; i++)
            {
                if (clip.name == TripleSlashClipNames[i])
                {
                    tripleSlashClipLengths[i] = clip.length;
                    tripleSlashTotal += clip.length;
                }
            }
        }

        if (tripleSlashTotal > 0f)
        {
            skillAnimationDurations[(int)UI_GameSceneView.PlayerSkillSlot.S] = tripleSlashTotal;
        }
    }

    /// <summary>
    /// slot에 해당하는 스킬 애니메이션을 BasicCharacterStance의 Attack Layer에서 재생한다.
    /// 콤보 공격 중이거나 이미 다른 스킬이 재생 중이면 무시한다(같은 레이어를 공유하므로 상호 배제).
    /// GameSceneController가 UI_GameSceneView.TryStartPlayerSkillCooldown이 성공했을 때만 호출한다.
    /// </summary>
public void PlaySkillAnimation(UI_GameSceneView.PlayerSkillSlot slot)
    {
        if (animator == null || comboStep > 0 || isSkillPlaying)
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

        // D(속성베기)는 원샷 풀링 이펙트 대신 ActivateFireForceBuff가 켜는 지속형 오라(attributeAssignmentEffectInstance)를 쓰므로 여기서는 생략한다.
        if (slot != UI_GameSceneView.PlayerSkillSlot.D)
        {
            pendingSkillSlot = slot;
            ScheduleSkillEffects(slot);
        }

        CancelInvoke(nameof(FinishSkillAnimation));
        Invoke(nameof(FinishSkillAnimation), skillAnimationDurations[index]);

        if (slot == UI_GameSceneView.PlayerSkillSlot.D)
        {
            ActivateFireForceBuff();
        }
    }

    /// <summary>
    /// slot에 대응하는 스킬 이펙트 소환을 예약한다. S(삼단베기)는 Combo01~03 세 번의 타격이
    /// 순차적으로 재생되므로, 각 타격이 시작되는 시점(누적 클립 길이)마다 skillEffectTriggerDelay를 더해
    /// 이펙트를 세 번 나눠 소환한다. 그 외 슬롯은 기존처럼 skillEffectTriggerDelay 후 한 번만 소환한다.
    /// </summary>
    private void ScheduleSkillEffects(UI_GameSceneView.PlayerSkillSlot slot)
    {
        if (slot == UI_GameSceneView.PlayerSkillSlot.S)
        {
            float elapsed = 0f;
            for (int i = 0; i < tripleSlashClipLengths.Length; i++)
            {
                float delay = elapsed + skillEffectTriggerDelay;
                if (delay > 0f)
                {
                    Invoke(nameof(PlaySkillSlashEffect), delay);
                }
                else
                {
                    PlaySkillSlashEffect();
                }

                elapsed += tripleSlashClipLengths[i];
            }
            return;
        }

        if (skillEffectTriggerDelay > 0f)
        {
            Invoke(nameof(PlaySkillSlashEffect), skillEffectTriggerDelay);
        }
        else
        {
            PlaySkillSlashEffect();
        }
    }

    /// <summary>
    /// 스킬 애니메이션 재생이 끝난 뒤 SkillIndex를 0으로 되돌려 AttackLayerIdle로 복귀시키고,
    /// 콤보가 진행 중이 아니면 Attack Layer weight도 0으로 되돌린다.
    /// </summary>
    private void FinishSkillAnimation()
    {
        isSkillPlaying = false;

        if (animator != null)
        {
            animator.SetInteger(SkillIndexHash, 0);
        }

        if (comboStep == 0)
        {
            SetAttackLayerWeight(0f);
        }
    }


/// <summary>
    /// pose(로컬 오프셋/회전)를 캐릭터의 현재 transform 기준 월드 좌표로 변환해 풀링된 Slash_Normal
    /// 이펙트를 소환한다. 콤보 공격/스킬 이펙트가 공통으로 사용하는 실제 스폰 로직이다.
    /// </summary>
private void SpawnSlashEffect(SlashEffectPose pose, string effectKey, float? syncDurationSeconds = null)
    {
        if (ObjectPoolController.Instance == null || string.IsNullOrEmpty(effectKey))
        {
            return;
        }

        Vector3 spawnPosition = transform.TransformPoint(pose.localOffset);
        Quaternion spawnRotation = transform.rotation * Quaternion.Euler(pose.localEulerAngles);

        GameObject effectInstance = ObjectPoolController.Instance.Get(effectKey, spawnPosition, spawnRotation);

        if (syncDurationSeconds.HasValue)
        {
            SyncParticleDurationToTarget(effectInstance, syncDurationSeconds.Value);
        }
    }

    /// <summary>
    /// pendingSkillSlot에 대응하는 이펙트를 소환한다. PlaySkillAnimation에서 skillEffectTriggerDelay 후 호출된다.
    /// </summary>
private void PlaySkillSlashEffect()
    {
        SlashEffectPose pose = GetSkillEffectPose(pendingSkillSlot);
        string effectKey = GetSkillEffectKey(pendingSkillSlot);

        // F(회전베기)는 WheelWindNormal 이펙트의 자체 재생 시간이 스킬 애니메이션보다 훨씬 길어
        // 애니메이션이 끝난 뒤에도 이펙트만 남아 재생되는 문제가 있어, 둘이 동시에 끝나도록
        // 이펙트의 simulationSpeed를 맞추다.
        float? syncDurationSeconds = null;
        if (pendingSkillSlot == UI_GameSceneView.PlayerSkillSlot.F)
        {
            int index = (int)pendingSkillSlot;
            float animationDuration = index < skillAnimationDurations.Length ? skillAnimationDurations[index] : FallbackSkillClipLength;
            float remainingAnimationTime = animationDuration - skillEffectTriggerDelay;
            syncDurationSeconds = Mathf.Max(remainingAnimationTime, MinEffectSyncDuration);
        }

        SpawnSlashEffect(pose, effectKey, syncDurationSeconds);
    }

    /// <summary>
    /// effectInstance(및 자식)에 포함된 모든 ParticleSystem의 simulationSpeed를 조정해,
    /// 원래 재생 시간(startDelay+duration+startLifetime 기준 가장 늘게 끝나는 자식 파티클 기준)과 무관하게
    /// targetDurationSeconds 안에 재생이 끝나도록 맞추다. PooledParticleEffect가 IsAlive(withChildren:true)로
    /// 반환 시점을 판단하므로, 가장 늘게 끝나는 자식 기준으로 배율을 계산해 전체가 함께 끝나도록 한다.
    /// duration/startDelay/startLifetime은 simulationSpeed의 영향을 받지 않는 값이라, 풀에서 재사용되어도
    /// 매번 원본 기준으로 정확하게 재계산된다.
    /// </summary>
    private static void SyncParticleDurationToTarget(GameObject effectInstance, float targetDurationSeconds)
    {
        if (effectInstance == null || targetDurationSeconds <= 0f)
        {
            return;
        }

        ParticleSystem[] particleSystems = effectInstance.GetComponentsInChildren<ParticleSystem>(true);
        if (particleSystems.Length == 0)
        {
            return;
        }

        float naturalDurationSeconds = 0f;
        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem.MainModule main = particleSystems[i].main;
            float end = main.startDelay.constantMax + main.duration + (main.loop ? 0f : main.startLifetime.constantMax);
            naturalDurationSeconds = Mathf.Max(naturalDurationSeconds, end);
        }

        if (naturalDurationSeconds <= 0f)
        {
            return;
        }

        float simulationSpeed = Mathf.Clamp(naturalDurationSeconds / targetDurationSeconds, MinEffectSimulationSpeed, MaxEffectSimulationSpeed);

        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem.MainModule main = particleSystems[i].main;
            main.simulationSpeed = simulationSpeed;
        }
    }

    /// <summary>
    /// slot(A/S/D/F)에 대응하는 스킬 이펙트 배치를 가져온다. 배열 범위를 벗어나면 마지막 값을 재사용한다.
    /// </summary>
    private SlashEffectPose GetSkillEffectPose(UI_GameSceneView.PlayerSkillSlot slot)
    {
        if (skillEffectPosesBySlot == null || skillEffectPosesBySlot.Length == 0)
        {
            return default;
        }

        int index = Mathf.Clamp((int)slot, 0, skillEffectPosesBySlot.Length - 1);
        return skillEffectPosesBySlot[index];
    }


/// <summary>
    /// slot(A/S/D/F)에 대응하는 이펙트 Addressable 키를 가져온다. 배열이 비어있거나 범위를 벗어나거나
    /// 해당 원소가 비어있으면 콤보와 같은 SlashEffectKey(Slash_Normal)로 폴백한다.
    /// </summary>
    private string GetSkillEffectKey(UI_GameSceneView.PlayerSkillSlot slot)
    {
        if (skillEffectKeysBySlot == null || skillEffectKeysBySlot.Length == 0)
        {
            return SlashEffectKey;
        }

        int index = Mathf.Clamp((int)slot, 0, skillEffectKeysBySlot.Length - 1);
        string key = skillEffectKeysBySlot[index];

        return string.IsNullOrEmpty(key) ? SlashEffectKey : key;
    }

    /// <summary>
    /// 콤보/스킬이 사용하는 모든 이펙트 풀을 Awake에서 미리 프리로드한다. skillEffectKeysBySlot이
    /// 콤보와 다른 키로 바뀌어도(예: 속성별 이펙트) 첫 사용 시점에 "아직 로드 안 됨" 오류가 나지 않도록
    /// 겹치지 않는 키만 모아 함께 프리로드한다.
    /// </summary>
    private void PreloadEffectPools()
    {
        ObjectPoolController.Instance?.Preload(SlashEffectKey, SlashEffectPrewarmCount);
        ObjectPoolController.Instance?.Preload(SlashFireForceEffectKey, SlashEffectPrewarmCount);

        if (skillEffectKeysBySlot == null)
        {
            return;
        }

        for (int i = 0; i < skillEffectKeysBySlot.Length; i++)
        {
            string key = skillEffectKeysBySlot[i];
            if (string.IsNullOrEmpty(key) || key == SlashEffectKey || key == SlashFireForceEffectKey)
            {
                continue;
            }

            ObjectPoolController.Instance?.Preload(key, SlashEffectPrewarmCount);
        }
    }


/// <summary>
    /// D(속성베기) 스킬 사용 시 호출된다. 기본 공격 이펙트를 SlashFireForceEffectKey로 바꾸고,
    /// FireForceBuffDuration(8초) 후 자동으로 원래(SlashEffectKey)로 되돌린다. 재사용 시 기존
    /// 타이머를 취소하고 새로 8초를 재장한다(중첩되지 않고 갱신된다).
    /// </summary>
    private void ActivateFireForceBuff()
    {
        currentComboEffectKey = SlashFireForceEffectKey;

        if (attributeAssignmentEffectInstance != null)
        {
            attributeAssignmentEffectInstance.SetActive(true);

            // 자식 파티클이 전부 Play On Awake라 SetActive(true)만으로도 재생되지만,
            // 잔여 파티클(예: 로드 직후 Instantiate가 즉시 트리거한 재생의 잔상)이 남아있으면
            // 새 재생과 겹쳐 이펙트가 두 번 나오는 것처럼 보인다. Clear 후 새로 재생해 한 번만 보이게 한다.
            attributeAssignmentRootParticle?.Clear(true);
            attributeAssignmentRootParticle?.Play(true);
        }

        CancelInvoke(nameof(DeactivateFireForceBuff));
        Invoke(nameof(DeactivateFireForceBuff), FireForceBuffDuration);
    }

    /// <summary>FireForce 버프가 끝나면 기본 공격 이펙트를 원래(SlashEffectKey)로 되돌립다.</summary>
    private void DeactivateFireForceBuff()
    {
        currentComboEffectKey = SlashEffectKey;

        if (attributeAssignmentEffectInstance != null)
        {
            // 다음 ActivateFireForceBuff 때 잔여 파티클이 남지 않도록 완전히 멈추고 비운 뒤 끈다.
            attributeAssignmentRootParticle?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            attributeAssignmentEffectInstance.SetActive(false);
        }
    }


/// <summary>
    /// D 사용 중에만 켜지는 attributeAssignmentEffectInstance를 캐릭터 자식으로 미리 인스턴스화해둔다.
    /// skillEffectPosesBySlot[D]와 같은 배치값을 쓰며, 비활성화 상태로 시작해 ActivateFireForceBuff가
    /// 켜기 전까지는 보이지 않는다. 루프 파티클이라 풀링(SpawnSlashEffect)이 아니라
    /// 인스턴스 하나를 계속 재사용(SetActive 토글)한다.
    /// </summary>
    private void LoadAttributeAssignmentEffect()
    {
        if (AddressableAssetController.Instance == null)
        {
            DebugLogController.GenerateErrorMessage<PlayerController>("AddressableAssetController.Instance가 없어 AttributeAssignmentEffect를 준비할 수 없습니다.");
            return;
        }

        AddressableAssetController.Instance.LoadPrefabAddress<GameObject>(AttributeAssignmentEffectKey, prefab =>
        {
            if (prefab == null || attributeAssignmentEffectInstance != null)
            {
                return;
            }

            attributeAssignmentEffectInstance = AddressableAssetController.Instance.InstantiatePrefab(prefab);
            attributeAssignmentEffectInstance.transform.SetParent(transform, false);

            SlashEffectPose pose = GetSkillEffectPose(UI_GameSceneView.PlayerSkillSlot.D);
            attributeAssignmentEffectInstance.transform.localPosition = pose.localOffset;
            attributeAssignmentEffectInstance.transform.localRotation = Quaternion.Euler(pose.localEulerAngles);

            attributeAssignmentRootParticle = attributeAssignmentEffectInstance.GetComponent<ParticleSystem>();

            // 자식 파티클이 전부 Play On Awake라 Instantiate 직후 곧바로 재생이 시작된다.
            // 이 잔여 재생을 지우지 않은 채 SetActive(false)만 하면, 이후 ActivateFireForceBuff가
            // 다시 켤 때 이 잔상과 새 재생이 겹쳐 이펙트가 두 번 재생되는 것처럼 보인다.
            attributeAssignmentRootParticle?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            attributeAssignmentEffectInstance.SetActive(false);
        });
    }
}
