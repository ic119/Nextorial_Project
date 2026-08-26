using UnityEngine;
using UnityEngine.UI;

public class UI_LobbySceneView : MonoBehaviour
{
    #region Variable
    [SerializeField] private GameObject maskImage;

    [Header("Lobby Scene Buttons")]
    [SerializeField] private Button startButton;
    [SerializeField] private Button continueButton;
    [SerializeField] private Button exitButton;
    #endregion

    #region LifeCycle
private void Awake()
    {
        if (maskImage != null)
        {
            maskImage.SetActive(false);
        }

        if (continueButton != null)
        {
            bool hasSaveData = SaveDataController.Instance != null && SaveDataController.Instance.HasSaveData();
            continueButton.gameObject.SetActive(hasSaveData);
        }
    }
    #endregion

    #region Method
    #endregion
}