using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 캐릭터 생성 팝업 UI View
/// - 3D 캐릭터 실시간 프리뷰 영역
/// - 닉네임 입력 및 글자 수/유효성 검사
/// - 성별(남/여) 선택 및 3D 모델 실시간 연동
/// - 기본 능력치 표시
/// - 캐릭터 저장 및 팝업 닫기/취소 기능
/// </summary>
public class UI_CharacterCreatePopup : MonoBehaviour
{
    #region Variable
    [Header("3D Preview Components")]
    [SerializeField] private RawImage characterPreviewImage;
    [SerializeField] private UI_DragRotateHandler dragRotateHandler;
    [SerializeField] private Button rotateLeftButton;
    [SerializeField] private Button rotateRightButton;
    [SerializeField] private Button resetRotationButton;
    [SerializeField] private TextMeshProUGUI previewNameBadge;
    [SerializeField] private TextMeshProUGUI previewGenderBadge;
    [SerializeField] private CharacterPreviewStage previewStage;

    [Header("Nickname Input")]
    [SerializeField] private TMP_InputField nicknameInputField;
    [SerializeField] private TextMeshProUGUI charCountText;
    [SerializeField] private TextMeshProUGUI feedbackText;
    [SerializeField] private int minNicknameLength = 2;
    [SerializeField] private int maxNicknameLength = 12;

    [Header("Gender Selection")]
    [SerializeField] private Button maleButton;
    [SerializeField] private Button femaleButton;
    [SerializeField] private Image maleButtonHighlight;
    [SerializeField] private Image femaleButtonHighlight;
    [SerializeField] private TextMeshProUGUI maleButtonText;
    [SerializeField] private TextMeshProUGUI femaleButtonText;

    [Header("Stats Display")]
    [SerializeField] private TextMeshProUGUI strValueText;
    [SerializeField] private TextMeshProUGUI agiValueText;
    [SerializeField] private TextMeshProUGUI intValueText;
    [SerializeField] private TextMeshProUGUI totalValueText;

    [Header("Action Buttons")]
    [SerializeField] private Button createButton;
    [SerializeField] private Button cancelButton;
    [SerializeField] private Button closeButton;

    [Header("Color Palettes")]
    [SerializeField] private Color activeMaleColor = new Color(0.2f, 0.7f, 1f, 1f);
    [SerializeField] private Color activeFemaleColor = new Color(1f, 0.45f, 0.65f, 1f);
    [SerializeField] private Color selectedBgColor = new Color(0.15f, 0.25f, 0.4f, 0.9f);
    [SerializeField] private Color normalBgColor = new Color(0.1f, 0.12f, 0.16f, 0.8f);
    [SerializeField] private Color normalTextColor = new Color(0.75f, 0.8f, 0.88f, 1f);

    private Gender selectedGender = Gender.Male;
    private UserStats baseUserStats;
    private bool isInitialized = false;

    public event Action<UserSaveData> OnCharacterCreated;
    public event Action OnPopupClosed;
    #endregion

    #region LifeCycle
    private void Awake()
    {
        InitializeComponents();
    }

    private void OnEnable()
    {
        ResetForm();
        if (previewStage != null && characterPreviewImage != null)
        {
            var rt = previewStage.SetupPreview(1024, 1024);
            characterPreviewImage.texture = rt;
            previewStage.SetGender(selectedGender);
        }
    }

    private void OnDestroy()
    {
        UnregisterEvents();
    }
    #endregion

    #region Method
    public void InitializeComponents()
    {
        if (isInitialized) return;
        isInitialized = true;

        // 기본 능력치 데이터 세팅
        baseUserStats = new UserStats
        {
            str = 10,
            agi = 10,
            intel = 10
        };

        UpdateStatsUI();

        // 3D 프리뷰 스테이지 동적 생성 또는 연결
        if (previewStage == null)
        {
            var stageGo = new GameObject("CharacterPreviewStage");
            stageGo.transform.position = new Vector3(500f, 500f, 500f);
            previewStage = stageGo.AddComponent<CharacterPreviewStage>();
        }

        if (characterPreviewImage != null && previewStage != null)
        {
            var rt = previewStage.SetupPreview(1024, 1024);
            characterPreviewImage.texture = rt;
        }

        RegisterEvents();
        SelectGender(Gender.Male);
    }

    private void RegisterEvents()
    {
        if (nicknameInputField != null)
        {
            nicknameInputField.characterLimit = maxNicknameLength;
            nicknameInputField.onValueChanged.AddListener(OnNicknameValueChanged);
        }

        if (maleButton != null)
        {
            maleButton.onClick.AddListener(() => SelectGender(Gender.Male));
        }

        if (femaleButton != null)
        {
            femaleButton.onClick.AddListener(() => SelectGender(Gender.Female));
        }

        if (rotateLeftButton != null)
        {
            rotateLeftButton.onClick.AddListener(() => previewStage?.RotateLeft(45f));
        }

        if (rotateRightButton != null)
        {
            rotateRightButton.onClick.AddListener(() => previewStage?.RotateRight(45f));
        }

        if (resetRotationButton != null)
        {
            resetRotationButton.onClick.AddListener(() => previewStage?.ResetRotation());
        }

        if (dragRotateHandler != null)
        {
            dragRotateHandler.OnRotateDelta += (delta) => previewStage?.AddRotation(delta);
        }

        if (createButton != null)
        {
            createButton.onClick.AddListener(OnClickCreateButton);
        }

        if (cancelButton != null)
        {
            cancelButton.onClick.AddListener(ClosePopup);
        }

        if (closeButton != null)
        {
            closeButton.onClick.AddListener(ClosePopup);
        }
    }

    private void UnregisterEvents()
    {
        if (nicknameInputField != null)
        {
            nicknameInputField.onValueChanged.RemoveAllListeners();
        }

        if (maleButton != null) maleButton.onClick.RemoveAllListeners();
        if (femaleButton != null) femaleButton.onClick.RemoveAllListeners();
        if (rotateLeftButton != null) rotateLeftButton.onClick.RemoveAllListeners();
        if (rotateRightButton != null) rotateRightButton.onClick.RemoveAllListeners();
        if (resetRotationButton != null) resetRotationButton.onClick.RemoveAllListeners();
        if (createButton != null) createButton.onClick.RemoveAllListeners();
        if (cancelButton != null) cancelButton.onClick.RemoveAllListeners();
        if (closeButton != null) closeButton.onClick.RemoveAllListeners();
    }

    public void Open()
    {
        gameObject.SetActive(true);
        ResetForm();
    }

    public void ClosePopup()
    {
        gameObject.SetActive(false);
        OnPopupClosed?.Invoke();
    }

    private void ResetForm()
    {
        if (nicknameInputField != null)
        {
            nicknameInputField.text = string.Empty;
        }

        SelectGender(Gender.Male);
        UpdateNicknameValidation(string.Empty);

        if (previewStage != null)
        {
            previewStage.ResetRotation();
        }
    }

    public void SelectGender(Gender gender)
    {
        selectedGender = gender;

        if (previewStage != null)
        {
            previewStage.SetGender(gender);
        }

        // 버튼 하이라이트 및 UI 비주얼 갱신
        bool isMale = gender == Gender.Male;

        if (maleButtonHighlight != null)
        {
            maleButtonHighlight.gameObject.SetActive(isMale);
            maleButtonHighlight.color = activeMaleColor;
        }

        if (femaleButtonHighlight != null)
        {
            femaleButtonHighlight.gameObject.SetActive(!isMale);
            femaleButtonHighlight.color = activeFemaleColor;
        }

        if (maleButtonText != null)
        {
            maleButtonText.color = isMale ? activeMaleColor : normalTextColor;
            maleButtonText.fontStyle = isMale ? FontStyles.Bold : FontStyles.Normal;
        }

        if (femaleButtonText != null)
        {
            femaleButtonText.color = !isMale ? activeFemaleColor : normalTextColor;
            femaleButtonText.fontStyle = !isMale ? FontStyles.Bold : FontStyles.Normal;
        }

        if (previewGenderBadge != null)
        {
            previewGenderBadge.text = isMale ? "남성 (Male)" : "여성 (Female)";
            previewGenderBadge.color = isMale ? activeMaleColor : activeFemaleColor;
        }
    }

    private void OnNicknameValueChanged(string text)
    {
        UpdateNicknameValidation(text);

        if (previewNameBadge != null)
        {
            previewNameBadge.text = string.IsNullOrWhiteSpace(text) ? "이름 없음" : text;
        }
    }

    private bool UpdateNicknameValidation(string text)
    {
        int length = text != null ? text.Trim().Length : 0;

        if (charCountText != null)
        {
            charCountText.text = $"{length} / {maxNicknameLength}";
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            SetFeedback("닉네임을 입력해 주세요.", new Color(0.8f, 0.8f, 0.8f, 0.8f));
            SetCreateButtonInteractable(false);
            return false;
        }

        if (length < minNicknameLength)
        {
            SetFeedback($"닉네임은 최소 {minNicknameLength}자 이상이어야 합니다.", new Color(1f, 0.4f, 0.4f, 1f));
            SetCreateButtonInteractable(false);
            return false;
        }

        // 특수문자 검사 (선택적)
        SetFeedback("사용 가능한 닉네임입니다.", new Color(0.3f, 0.9f, 0.5f, 1f));
        SetCreateButtonInteractable(true);
        return true;
    }

    private void SetFeedback(string message, Color color)
    {
        if (feedbackText != null)
        {
            feedbackText.text = message;
            feedbackText.color = color;
        }
    }

    private void SetCreateButtonInteractable(bool interactable)
    {
        if (createButton != null)
        {
            createButton.interactable = interactable;
        }
    }

    private void UpdateStatsUI()
    {
        if (baseUserStats == null) return;

        if (strValueText != null) strValueText.text = baseUserStats.str.ToString();
        if (agiValueText != null) agiValueText.text = baseUserStats.agi.ToString();
        if (intValueText != null) intValueText.text = baseUserStats.intel.ToString();
        if (totalValueText != null) totalValueText.text = baseUserStats.GetTotal().ToString();
    }

    private void OnClickCreateButton()
    {
        string nickname = nicknameInputField != null ? nicknameInputField.text.Trim() : string.Empty;

        if (!UpdateNicknameValidation(nickname))
        {
            return;
        }

        // 유저 세이브 데이터 구성
        var userSaveData = new UserSaveData
        {
            userID = nickname,
            gender = selectedGender,
            userLevel = 1,
            userExp = 0f,
            userStats = new UserStats
            {
                str = baseUserStats.str,
                agi = baseUserStats.agi,
                intel = baseUserStats.intel
            }
        };

        var fullSaveModel = new SaveUserDataModel
        {
            user = userSaveData,
            dragon = new DragonSaveData
            {
                dragonID = "BabyDragon",
                dragonLevel = 1,
                dragonStats = new DragonStats { str = 5, mana = 10 }
            }
        };

        // 저장 실행
        if (SaveDataController.Instance != null)
        {
            bool saveSuccess = SaveDataController.Instance.Save(fullSaveModel);
            if (saveSuccess)
            {
                DebugLogController.GenerateLogMessage<UI_CharacterCreatePopup>($"캐릭터 생성 및 저장 성공: {nickname} ({selectedGender})");
            }
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.HasSaveData = true;
        }

        OnCharacterCreated?.Invoke(userSaveData);
        ClosePopup();
    }
    #endregion
}

