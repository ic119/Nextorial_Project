using UnityEngine;

public class UI_LobbySceneView : MonoBehaviour
{
    #region Variable
    [SerializeField] private GameObject maskImage;
    #endregion

    #region LifeCycle
    private void Awake()
    {
        if (maskImage != null)
        {
            maskImage.SetActive(false);
        }
    }
    #endregion

    #region Method
    #endregion
}