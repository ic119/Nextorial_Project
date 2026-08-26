using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 로딩 및 작업 진행률을 나타내는 ProgressBar UI View.
/// </summary>
public class UI_ProgressBar : MonoBehaviour
{
    #region Variable
    [Header("UI Component")]
    [SerializeField] private Slider progressSlider;
    [SerializeField] private TextMeshProUGUI progressPercentageText;
    [SerializeField] private TextMeshProUGUI statusText;

    [Header("Setting")]
    [SerializeField] private bool usePercentageFormat = true;
    [SerializeField] private string defaultStatusText = "Loading...";
    #endregion

    #region LifeCycle
    private void Awake()
    {
        if (progressSlider != null)
        {
            progressSlider.interactable = false;
        }

        SetProgress(0f);
        if (statusText != null && !string.IsNullOrEmpty(defaultStatusText))
        {
            statusText.text = defaultStatusText;
        }
    }
    #endregion

    #region Method
    /// <summary>
    /// 진행률(0.0 ~ 1.0 또는 0 ~ 100)을 설정
    /// </summary>
    /// <param name="value">진행률 값 (0.0f ~ 1.0f 또는 0 ~ 100)</param>
    public void SetProgress(float value)
    {
        // 1.0 초과 시 0~100 범위로 간주하여 0~1 범위로 정규화
        float normalizedValue = value > 1.0f ? Mathf.Clamp01(value / 100.0f) : Mathf.Clamp01(value);

        if (progressSlider != null)
        {
            progressSlider.value = normalizedValue;
        }

        if (progressPercentageText != null)
        {
            int percentage = Mathf.RoundToInt(normalizedValue * 100f);
            progressPercentageText.text = usePercentageFormat ? $"{percentage}%" : $"{normalizedValue:F2}";
        }
    }

    /// <summary>
    /// 진행률과 함께 상태 메시지를 설정
    /// </summary>
    /// <param name="value">진행률 값 (0.0f ~ 1.0f)</param>
    /// <param name="message">상태 메시지</param>
    public void SetProgress(float value, string message)
    {
        SetProgress(value);
        SetStatusMessage(message);
    }

    /// <summary>
    /// 상태 메시지를 변경합니다.
    /// </summary>
    /// <param name="message">상태 메시지</param>
    public void SetStatusMessage(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }
    }
    #endregion
}
