using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;


public class UI_GameSceneView : MonoBehaviour
{
    #region Variable
    [Header("UI Variable")]
    [SerializeField] private TextMeshProUGUI nameLabel;
    [SerializeField] private Slider hpSlider;
    [SerializeField] private TextMeshProUGUI hpValueLabel;
    [SerializeField] private Slider expSlider;
    [SerializeField] private TextMeshProUGUI expValueLabel;

    [Header("Presentation Settings")]
    [Tooltip("HpSlider가 초당 줄어드는 체력량. 값이 클수록 피격 시 슬라이더가 더 빨리 감소한다.")]
    [SerializeField] private float hpDrainSpeed = 80f;
    [Tooltip("ExpSlider가 초당 채워지는 경험치량. 값이 클수록 경험치 획득 시 슬라이더가 더 빨리 증가한다.")]
    [SerializeField] private float expGainSpeed = 40f;

    [Header("Player Skill Slot_A/S/D/F")]
    [SerializeField] private Image SkillSlotA;
    [SerializeField] private Image SkillSlotS;
    [SerializeField] private Image SkillSlotD;
    [SerializeField] private Image SkillSlotF;

    [Header("Dragon Skill Slot_Q/W/E/R")]
    [SerializeField] private Image skillSlotQ;
    [SerializeField] private Image skillSlotW;
    [SerializeField] private Image skillSlotE;
    [SerializeField] private Image skillSlotR;

    [System.Serializable]
    private struct SkillCooldownVisual
    {
        public Image cooldownFillImage;
        public TextMeshProUGUI cooldownText;
    }

    private class SkillCooldownState
    {
        public float remaining;
        public float duration;
    }

    public enum PlayerSkillSlot
    {
        A = 0,
        S = 1,
        D = 2,
        F = 3,
        Q,
        W,
        E,
        R
    }

    [Header("Player Skill Cooldown")]
    [Tooltip("각 슬롯(A/S/D/F 순서)의 쿸타임 연출용 오버레이(Filled Image)와 남은 시간 텍스트. 배열 순서는 PlayerSkillSlot(A/S/D/F)과 일치해야 한다.")]
    [SerializeField] private SkillCooldownVisual[] playerSkillCooldownVisuals = new SkillCooldownVisual[4];

    [Header("Dragon Skill Cooldown")]
    [Tooltip("각 슬롯(Q/W/E/R 순서)의 쿨타임 연출용 오버레이(Filled Image)와 남은 시간 텍스트. 배열 순서는 DragonSkillSlot(Q/W/E/R)과 일치해야 한다.")]
    [SerializeField] private SkillCooldownVisual[] dragonSkillCooldownVisuals = new SkillCooldownVisual[4];

    [Tooltip("쿨타임 중인 스킬 아이콘에 곱해지는 색상(어둡게 표시). 쿸타임이 끝나면 흰색으로 복구된다.")]
    [SerializeField] private Color cooldownIconTint = new Color(0.4f, 0.4f, 0.4f, 1f);

    [Tooltip("쿨타임이 시작된 직후(남은 시간이 가장 많을 때) 오버레이/텍스트 색상.")]
    [SerializeField] private Color cooldownStartColor = new Color(0.75f, 0.15f, 0.15f, 0.7f);

    [Tooltip("쿨타임이 거의 끝나갈 때(남은 시간이 0에 가까울 때) 오버레이/텍스트 색상.")]
    [SerializeField] private Color cooldownEndColor = new Color(0.2f, 0.85f, 0.35f, 0.35f);


    private readonly SkillCooldownState[] playerSkillCooldownStates =
    {
        new SkillCooldownState(),
        new SkillCooldownState(),
        new SkillCooldownState(),
        new SkillCooldownState()
    };

    private Image[] playerSkillIcons;

    /// <summary>슬롯별 진행 중인 쿸타임 색상 트윈(DOTween). 재트리거 시 이전 트윈을 Kill하기 위해 보관한다.</summary>
    private readonly Tween[] playerSkillCooldownTweens = new Tween[4];

    private readonly SkillCooldownState[] dragonSkillCooldownStates =
    {
        new SkillCooldownState(),
        new SkillCooldownState(),
        new SkillCooldownState(),
        new SkillCooldownState()
    };

    private Image[] dragonSkillIcons;

    /// <summary>드래곤 스킬 슬롯별 진행 중인 쿨타임 색상 트윈(DOTween).</summary>
    private readonly Tween[] dragonSkillCooldownTweens = new Tween[4];



    /// <summary>
    /// 아직 레벨업/다음 레벨 경험치 요구량 공식이 없어 EXPBar가 채워질 기준값이 정해져 있지 않다.
    /// 실제 경험치 곡선이 생기면 이 상수 대신 그 값을 써야 한다.
    /// </summary>
    private const float PlaceholderMaxExp = 100f;

    private PlayerCharacterModel characterModel;

    // Slider에 실제로 표시 중인 값. characterModel의 실제 값(목표치)과 분리해서, 값이 바뀌어도
    // 즉시 스냅하지 않고 hpDrainSpeed/expGainSpeed 속도로 서서히 따라가는 연출을 만든다.
    private float displayedHp;
    private float displayedExp;

    private int lastDisplayedCurrentHp = -1;
    private int lastDisplayedMaxHp = -1;
    private int lastDisplayedExp = -1;
    #endregion

    #region LifeCycle
private void Update()
    {
        UpdateHpDisplay();
        UpdateExpDisplay();
        UpdatePlayerSkillCooldowns();
        UpdateDragonSkillCooldowns();
    }

private void OnDestroy()
    {
        for (int i = 0; i < playerSkillCooldownTweens.Length; i++)
        {
            playerSkillCooldownTweens[i]?.Kill();
        }

        for (int i = 0; i < dragonSkillCooldownTweens.Length; i++)
        {
            dragonSkillCooldownTweens[i]?.Kill();
        }
    }

    #endregion

    #region Method
    /// <summary>
    /// 스폰된 캐릭터 정보로 UI를 초기화한다. 닉네임은 세이브 데이터 스냅샷이라 한 번만 표시하고,
    /// 체력/경험치는 PlayerCharacterModel(전투/경험치 획득 등으로 계속 바뀔 수 있는 값)을
    /// Update에서 계속 참조해 갱신한다. 최초 표시는 연출 없이 바로 스냅한다.
    /// </summary>
public void Bind(UserSaveData userData, PlayerCharacterModel model)
    {
        characterModel = model;

        SetNickname(userData != null ? userData.userID : string.Empty);

        displayedHp = characterModel != null ? characterModel.CurrentHp : 0;
        displayedExp = characterModel != null ? characterModel.CurrentExp : 0f;

        lastDisplayedCurrentHp = -1;
        lastDisplayedMaxHp = -1;
        lastDisplayedExp = -1;

        UpdateHpDisplay();
        UpdateExpDisplay();

        for (int i = 0; i < playerSkillCooldownStates.Length; i++)
        {
            playerSkillCooldownStates[i].remaining = 0f;
            playerSkillCooldownStates[i].duration = 0f;
            playerSkillCooldownTweens[i]?.Kill();
            playerSkillCooldownTweens[i] = null;
            ResetSkillCooldownVisual(i);
        }
        UpdatePlayerSkillCooldowns();
    }

    private void SetNickname(string nickname)
    {
        if (nameLabel != null)
        {
            nameLabel.text = nickname;
        }
    }

    /// <summary>
    /// HpSlider를 currentHp(목표치)를 향해 hpDrainSpeed 속도로 서서히 줄인다(또는 늘린다).
    /// 피격으로 currentHp가 줄어들면 displayedHp가 그 값을 뒤따라가며 슬라이더가 서서히 빠지는
    /// 연출이 만들어진다. 목표치가 바뀌지 않는 동안은 MoveTowards가 즉시 수렴해 추가 비용이 없다.
    /// </summary>
    private void UpdateHpDisplay()
    {
        if (characterModel == null)
        {
            return;
        }

        int targetHp = characterModel.CurrentHp;
        int maxHp = characterModel.MaxHp;

        displayedHp = Mathf.MoveTowards(displayedHp, targetHp, hpDrainSpeed * Time.deltaTime);

        if (hpSlider != null)
        {
            hpSlider.minValue = 0f;
            hpSlider.maxValue = maxHp;
            hpSlider.value = displayedHp;
        }

        int roundedHp = Mathf.RoundToInt(displayedHp);
        if (roundedHp == lastDisplayedCurrentHp && maxHp == lastDisplayedMaxHp)
        {
            return;
        }

        lastDisplayedCurrentHp = roundedHp;
        lastDisplayedMaxHp = maxHp;

        if (hpValueLabel != null)
        {
            hpValueLabel.text = $"{roundedHp} / {maxHp}";
        }
    }

    /// <summary>
    /// ExpSlider를 currentExp(목표치)를 향해 expGainSpeed 속도로 서서히 채운다.
    /// 경험치를 얻으면 displayedExp가 목표치를 뒤따라가며 슬라이더가 서서히 차오르는 연출이 만들어진다.
    /// 최대치는 실제 레벨업 곡선이 없어 PlaceholderMaxExp로 임시 고정한다.
    /// </summary>
    private void UpdateExpDisplay()
    {
        if (characterModel == null)
        {
            return;
        }

        float targetExp = characterModel.CurrentExp;

        displayedExp = Mathf.MoveTowards(displayedExp, targetExp, expGainSpeed * Time.deltaTime);

        if (expSlider != null)
        {
            expSlider.minValue = 0f;
            expSlider.maxValue = PlaceholderMaxExp;
            expSlider.value = Mathf.Clamp(displayedExp, 0f, PlaceholderMaxExp);
        }

        int roundedExp = Mathf.RoundToInt(displayedExp);
        if (roundedExp == lastDisplayedExp)
        {
            return;
        }

        lastDisplayedExp = roundedExp;

        if (expValueLabel != null)
        {
            expValueLabel.text = roundedExp.ToString();
        }
    }
    #endregion
/// <summary>
    /// 플레이어 스킬 슬롯(A/S/D/F)의 쿸타임을 시작한다. 이미 쿸타임 중이면 무시하고 false를 반환하므로,
    /// 스킬 발동 로직(예: PlayerController/향후 SkillController)이 이 반환값으로 실제 발동 성공 여부를 판단할 수 있다.
    /// </summary>
public bool TryStartPlayerSkillCooldown(PlayerSkillSlot slot, float cooldownDuration)
    {
        int index = (int)slot;
        if (index < 0 || index >= playerSkillCooldownStates.Length || cooldownDuration <= 0f)
        {
            return false;
        }

        if (playerSkillCooldownStates[index].remaining > 0f)
        {
            return false;
        }

        playerSkillCooldownStates[index].duration = cooldownDuration;
        playerSkillCooldownStates[index].remaining = cooldownDuration;

        PlayCooldownColorTween(index, cooldownDuration);
        return true;
    }

    /// <summary>
    /// slot이 현재 쿸타임 중인지 여부.
    /// </summary>
    public bool IsPlayerSkillOnCooldown(PlayerSkillSlot slot)
    {
        int index = (int)slot;
        return index >= 0 && index < playerSkillCooldownStates.Length && playerSkillCooldownStates[index].remaining > 0f;
    }


    /// <summary>
    /// 드래곤 스킬 슬롯(Q/W/E/R)의 쿨타임을 시작한다. 이미 쿨타임 중이면 무시하고 false를 반환한다.
    /// </summary>
    public bool TryStartDragonSkillCooldown(DragonSkillSlot slot, float cooldownDuration)
    {
        int index = (int)slot;
        if (index < 0 || index >= dragonSkillCooldownStates.Length || cooldownDuration <= 0f)
        {
            return false;
        }

        if (dragonSkillCooldownStates[index].remaining > 0f)
        {
            return false;
        }

        dragonSkillCooldownStates[index].duration = cooldownDuration;
        dragonSkillCooldownStates[index].remaining = cooldownDuration;

        PlayDragonCooldownColorTween(index, cooldownDuration);
        return true;
    }

    /// <summary>
    /// slot이 현재 쿨타임 중인지 여부.
    /// </summary>
    public bool IsDragonSkillOnCooldown(DragonSkillSlot slot)
    {
        int index = (int)slot;
        return index >= 0 && index < dragonSkillCooldownStates.Length && dragonSkillCooldownStates[index].remaining > 0f;
    }


    private void EnsureSkillIconArray()
    {
        if (playerSkillIcons == null)
        {
            playerSkillIcons = new[] { SkillSlotA, SkillSlotS, SkillSlotD, SkillSlotF };
        }
    }


    private void EnsureDragonSkillIconArray()
    {
        if (dragonSkillIcons == null)
        {
            dragonSkillIcons = new[] { skillSlotQ, skillSlotW, skillSlotE, skillSlotR };
        }
    }



    /// <summary>
    /// SkillDataModelSO에 등록된 스킬 데이터를 슬롯(A/S/D/F)별로 조회해 해당 슬롯 Image의 sprite를 아이콘으로 설정한다.
    /// 슬롯에 매칭되는 스킬이 없거나 skillIcon이 비어 있으면 해당 슬롯은 건드리지 않는다.
    /// </summary>
    public void ApplySkillIcons(SkillDataModelSO skillDataModel)
    {
        if (skillDataModel == null)
        {
            return;
        }

        EnsureSkillIconArray();

        foreach (PlayerSkillSlot slot in System.Enum.GetValues(typeof(PlayerSkillSlot)))
        {
            SkillData skill = skillDataModel.GetSkill(slot);
            if (skill == null || skill.skillIcon == null)
            {
                continue;
            }

            int index = (int)slot;
            if (index >= 0 && index < playerSkillIcons.Length && playerSkillIcons[index] != null)
            {
                playerSkillIcons[index].sprite = skill.skillIcon;
            }
        }
    }


    /// <summary>
    /// DragonSkillDataModelSO에 등록된 스킬 데이터를 슬롯(Q/W/E/R)별로 조회해 해당 슬롯 Image의 sprite를 아이콘으로 설정한다.
    /// 슬롯에 매칭되는 스킬이 없거나 skillIcon이 비어 있으면 해당 슬롯은 건드리지 않는다.
    /// </summary>
    public void ApplyDragonSkillIcons(DragonSkillDataModelSO skillDataModel)
    {
        if (skillDataModel == null)
        {
            return;
        }

        EnsureDragonSkillIconArray();

        foreach (DragonSkillSlot slot in System.Enum.GetValues(typeof(DragonSkillSlot)))
        {
            DragonSkillData skill = skillDataModel.GetSkill(slot);
            if (skill == null || skill.skillIcon == null)
            {
                continue;
            }

            int index = (int)slot;
            if (index >= 0 && index < dragonSkillIcons.Length && dragonSkillIcons[index] != null)
            {
                dragonSkillIcons[index].sprite = skill.skillIcon;
            }
        }
    }



    /// <summary>
    /// 매 프레임 남은 쿸타임을 줄이고, 슬롯별 오버레이(fillAmount)/카운트다운 텍스트/아이콘 색상을 갱신한다.
    /// fillAmount는 duration 대비 remaining 비율이라 쿸타임이 끝나갈수록(값이 작아질수록) 오버레이가 걸힌다.
    /// </summary>
private void UpdatePlayerSkillCooldowns()
    {
        for (int i = 0; i < playerSkillCooldownStates.Length; i++)
        {
            SkillCooldownState state = playerSkillCooldownStates[i];
            SkillCooldownVisual visual = i < playerSkillCooldownVisuals.Length ? playerSkillCooldownVisuals[i] : default;

            if (state.remaining > 0f)
            {
                state.remaining = Mathf.Max(0f, state.remaining - Time.deltaTime);
            }

            bool onCooldown = state.remaining > 0f;
            // 1(방금 시작) -> 0(거의 끝남)으로 줄어드는 비율. fillAmount와 카운트다운 숫자만 여기서 다루고,
            // 색상은 PlayCooldownColorTween이 시작할 때 DOTween으로 이미 재생하고 있다.
            float remainingRatio = onCooldown && state.duration > 0f ? state.remaining / state.duration : 0f;

            if (visual.cooldownFillImage != null)
            {
                visual.cooldownFillImage.fillAmount = remainingRatio;
            }

            if (visual.cooldownText != null)
            {
                visual.cooldownText.text = onCooldown ? Mathf.CeilToInt(state.remaining).ToString() : string.Empty;
            }
        }
    }


    /// <summary>
    /// UpdatePlayerSkillCooldowns와 동일한 방식으로 드래곤 스킬 슬롯(Q/W/E/R)의 남은 쿨타임/오버레이/카운트다운 텍스트를 갱신한다.
    /// </summary>
    private void UpdateDragonSkillCooldowns()
    {
        for (int i = 0; i < dragonSkillCooldownStates.Length; i++)
        {
            SkillCooldownState state = dragonSkillCooldownStates[i];
            SkillCooldownVisual visual = i < dragonSkillCooldownVisuals.Length ? dragonSkillCooldownVisuals[i] : default;

            if (state.remaining > 0f)
            {
                state.remaining = Mathf.Max(0f, state.remaining - Time.deltaTime);
            }

            bool onCooldown = state.remaining > 0f;
            float remainingRatio = onCooldown && state.duration > 0f ? state.remaining / state.duration : 0f;

            if (visual.cooldownFillImage != null)
            {
                visual.cooldownFillImage.fillAmount = remainingRatio;
            }

            if (visual.cooldownText != null)
            {
                visual.cooldownText.text = onCooldown ? Mathf.CeilToInt(state.remaining).ToString() : string.Empty;
            }
        }
    }



/// <summary>
    /// DOTween으로 해당 슬롯의 오버레이/텍스트/아이콘 색상을 cooldownStartColor에서 쿸타임 전체 duration에
    /// 걸쳐 cooldownEndColor(아이콘은 흰색)까지 선형으로 변화시킨다. 재트리거(이미 재생 중이면) 시에는
    /// 이전 트윈을 즉시 Kill하고 새로 시작한다. fillAmount/카운트다운 숫자는 여기서 건드리지 않고
    /// UpdatePlayerSkillCooldowns가 매 프레임 remaining 기준으로 갱신한다.
    /// </summary>
    private void PlayCooldownColorTween(int index, float duration)
    {
        EnsureSkillIconArray();

        playerSkillCooldownTweens[index]?.Kill();

        SkillCooldownVisual visual = index < playerSkillCooldownVisuals.Length ? playerSkillCooldownVisuals[index] : default;
        Image icon = index < playerSkillIcons.Length ? playerSkillIcons[index] : null;

        Sequence sequence = DOTween.Sequence();
        bool hasTarget = false;

        if (visual.cooldownFillImage != null)
        {
            visual.cooldownFillImage.color = cooldownStartColor;
            sequence.Join(visual.cooldownFillImage.DOColor(cooldownEndColor, duration).SetEase(Ease.Linear));
            hasTarget = true;
        }

        if (visual.cooldownText != null)
        {
            Color startTextColor = cooldownStartColor;
            startTextColor.a = 1f;
            Color endTextColor = cooldownEndColor;
            endTextColor.a = 1f;

            visual.cooldownText.color = startTextColor;
            sequence.Join(visual.cooldownText.DOColor(endTextColor, duration).SetEase(Ease.Linear));
            hasTarget = true;
        }

        if (icon != null)
        {
            icon.color = cooldownIconTint;
            sequence.Join(icon.DOColor(Color.white, duration).SetEase(Ease.Linear));
            hasTarget = true;
        }

        if (!hasTarget)
        {
            return;
        }

        sequence.OnComplete(() => ResetSkillCooldownVisual(index));
        playerSkillCooldownTweens[index] = sequence;
    }


    /// <summary>PlayCooldownColorTween과 동일한 방식으로 드래곤 스킬 슬롯의 쿨타임 색상 연출을 재생한다.</summary>
    private void PlayDragonCooldownColorTween(int index, float duration)
    {
        EnsureDragonSkillIconArray();

        dragonSkillCooldownTweens[index]?.Kill();

        SkillCooldownVisual visual = index < dragonSkillCooldownVisuals.Length ? dragonSkillCooldownVisuals[index] : default;
        Image icon = index < dragonSkillIcons.Length ? dragonSkillIcons[index] : null;

        Sequence sequence = DOTween.Sequence();
        bool hasTarget = false;

        if (visual.cooldownFillImage != null)
        {
            visual.cooldownFillImage.color = cooldownStartColor;
            sequence.Join(visual.cooldownFillImage.DOColor(cooldownEndColor, duration).SetEase(Ease.Linear));
            hasTarget = true;
        }

        if (visual.cooldownText != null)
        {
            Color startTextColor = cooldownStartColor;
            startTextColor.a = 1f;
            Color endTextColor = cooldownEndColor;
            endTextColor.a = 1f;

            visual.cooldownText.color = startTextColor;
            sequence.Join(visual.cooldownText.DOColor(endTextColor, duration).SetEase(Ease.Linear));
            hasTarget = true;
        }

        if (icon != null)
        {
            icon.color = cooldownIconTint;
            sequence.Join(icon.DOColor(Color.white, duration).SetEase(Ease.Linear));
            hasTarget = true;
        }

        if (!hasTarget)
        {
            return;
        }

        sequence.OnComplete(() => ResetDragonSkillCooldownVisual(index));
        dragonSkillCooldownTweens[index] = sequence;
    }


    /// <summary>
    /// 해당 슬롯의 오버레이/텍스트/아이콘을 "준비됨" 상태(fillAmount 0, 빈 텍스트, 흰색)로 되돌린다.
    /// 쿸타임 트윈이 자연스럽게 끝났을 때(OnComplete)와 Bind()에서 재사용한다.
    /// </summary>
    private void ResetSkillCooldownVisual(int index)
    {
        EnsureSkillIconArray();

        if (index < 0)
        {
            return;
        }

        if (index < playerSkillCooldownVisuals.Length)
        {
            SkillCooldownVisual visual = playerSkillCooldownVisuals[index];
            if (visual.cooldownFillImage != null)
            {
                visual.cooldownFillImage.fillAmount = 0f;
            }

            if (visual.cooldownText != null)
            {
                visual.cooldownText.text = string.Empty;
                visual.cooldownText.color = Color.white;
            }
        }

        if (index < playerSkillIcons.Length && playerSkillIcons[index] != null)
        {
            playerSkillIcons[index].color = Color.white;
        }
    }


    /// <summary>ResetSkillCooldownVisual과 동일한 방식으로 드래곤 스킬 슬롯을 "준비됨" 상태로 되돌린다.</summary>
    private void ResetDragonSkillCooldownVisual(int index)
    {
        EnsureDragonSkillIconArray();

        if (index < 0)
        {
            return;
        }

        if (index < dragonSkillCooldownVisuals.Length)
        {
            SkillCooldownVisual visual = dragonSkillCooldownVisuals[index];
            if (visual.cooldownFillImage != null)
            {
                visual.cooldownFillImage.fillAmount = 0f;
            }

            if (visual.cooldownText != null)
            {
                visual.cooldownText.text = string.Empty;
                visual.cooldownText.color = Color.white;
            }
        }

        if (index < dragonSkillIcons.Length && dragonSkillIcons[index] != null)
        {
            dragonSkillIcons[index].color = Color.white;
        }
    }

}
