using UnityEngine;

/// <summary>
/// Module오브젝트 & 하위 자식오브젝트 Don't Destroy 처리 
/// </summary>
public class ModuleDestroyController : MonoBehaviour
{
    #region LifeCycle
    private void Start()
    {
        DontDestroyOnLoad(gameObject);
    }
    #endregion
}