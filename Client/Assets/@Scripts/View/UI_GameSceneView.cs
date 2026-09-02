using TMPro;
using UnityEngine;
using UnityEngine.UI;

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
}
