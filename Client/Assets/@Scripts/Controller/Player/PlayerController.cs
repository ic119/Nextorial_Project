using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 키보드 좌우 화살표 입력을 받아 Rigidbody를 통해 캐릭터를 X축으로 이동시킨다.
/// 중력/충돌은 Rigidbody가 처리하므로, Y·Z는 건드리지 않고 X 속도만 제어한다.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    #region Variable
    [SerializeField] private float moveSpeed = 5f;

    private Rigidbody rb;
    #endregion

    #region Property
    /// <summary>현재 X축 이동 속력(부호 없음). 카메라 연출 등 외부에서 이동 여부 판단에 사용한다.</summary>
    public float CurrentSpeed => rb != null ? Mathf.Abs(rb.linearVelocity.x) : 0f;
    #endregion

    #region LifeCycle
    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void FixedUpdate()
    {
        float direction = GetHorizontalInput();

        Vector3 velocity = rb.linearVelocity;
        velocity.x = direction * moveSpeed;
        rb.linearVelocity = velocity;
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
    #endregion
}
