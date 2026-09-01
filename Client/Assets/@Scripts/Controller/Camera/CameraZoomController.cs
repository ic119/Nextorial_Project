using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// 추적 대상(PlayerController)의 이동 속도에 따라 CinemachineCamera의 FOV를 부드럽게 조정한다.
/// 정지 시 기본 화각, 이동 시 살짝 줌아웃해 진행 방향 시야를 넓혀준다.
/// </summary>
[RequireComponent(typeof(CinemachineCamera))]
public class CameraZoomController : MonoBehaviour
{
    #region Variable
    [Header("Zoom Settings")]
    [SerializeField] private float idleFieldOfView = 60f;
    [SerializeField] private float movingFieldOfView = 68f;
    [SerializeField] private float zoomSmoothTime = 0.4f;
    [SerializeField] private float moveSpeedThreshold = 0.1f;

    private CinemachineCamera cineCamera;
    private PlayerController target;
    private float fovVelocity;
    #endregion

    #region LifeCycle
    private void Awake()
    {
        cineCamera = GetComponent<CinemachineCamera>();
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        bool isMoving = target.CurrentSpeed > moveSpeedThreshold;
        float targetFieldOfView = isMoving ? movingFieldOfView : idleFieldOfView;

        LensSettings lens = cineCamera.Lens;
        lens.FieldOfView = Mathf.SmoothDamp(lens.FieldOfView, targetFieldOfView, ref fovVelocity, zoomSmoothTime);
        cineCamera.Lens = lens;
    }
    #endregion

    #region Method
    /// <summary>추적할 캐릭터를 지정한다. Follow 대상 지정과 이동 속도 조회를 함께 처리한다.</summary>
    public void SetTarget(PlayerController playerController)
    {
        target = playerController;
        cineCamera.Follow = playerController != null ? playerController.transform : null;
    }
    #endregion
}
