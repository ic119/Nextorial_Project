using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 키보드 입력을 한 곳에서 읽어 의미 단위 이벤트/값으로 재발행하는 중앙 입력 컨트롤러.
/// 이전에는 PlayerController가 Keyboard.current를 직접 폴링했는데, 앞으로 기능(스킬 등)이
/// 늘어날수록 여러 스크립트가 각자 키보드를 폴링하게 되어 키 재배치나 키 충돌 확인이 어려워진다.
/// 이 클래스가 유일한 폴링 지점이 되고, 각 기능(PlayerController 등)은 이벤트/프로퍼티만 구독한다.
/// </summary>
public class KeyboardInputController : SingletonObject<KeyboardInputController>
{
    #region Variable
    [Header("Key Bindings")]
    [SerializeField] private Key jumpKey = Key.Space;
    [SerializeField] private Key attackKey = Key.C;
    [SerializeField] private Key moveLeftKey = Key.LeftArrow;
    [SerializeField] private Key moveRightKey = Key.RightArrow;

    [Header("Skill Key Bindings (A/S/D/F)")]
    [SerializeField] private Key skillAKey = Key.A;
    [SerializeField] private Key skillSKey = Key.S;
    [SerializeField] private Key skillDKey = Key.D;
    [SerializeField] private Key skillFKey = Key.F;


    /// <summary>
    /// false로 설정하면 이 프레임부터 모든 입력을 무시한다. 팝업/컷씬 등에서 게임플레이 입력을
    /// 잠시 막아야 할 때 사용한다(기본값 true).
    /// </summary>
    public bool InputEnabled { get; set; } = true;

    /// <summary>
    /// 왼쪽(-1)/오른쪽(+1) 방향키를 합산한 부호 있는 이동 축. 매 프레임 눌림 상태를 그대로 반영하는
    /// 연속값이라 이벤트가 아닌 프로퍼티로 노출한다(폴링하는 쪽에서 매 프레임 읽으면 된다).
    /// </summary>
    public float MoveAxis { get; private set; }

    /// <summary>점프 키가 눌린 프레임에 발생한다.</summary>
    public event Action OnJumpPressed;

    /// <summary>공격 키가 눌린 프레임에 발생한다.</summary>
    public event Action OnAttackPressed;

    /// <summary>스킬 슬롯(A/S/D/F) 키가 눌린 프레임에 해당 슬롯과 함께 발생한다.</summary>
    public event Action<UI_GameSceneView.PlayerSkillSlot> OnSkillKeyPressed;

    #endregion

    #region LifeCycle
private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || !InputEnabled)
        {
            MoveAxis = 0f;
            return;
        }

        MoveAxis = ComputeMoveAxis(keyboard);

        if (keyboard[jumpKey].wasPressedThisFrame)
        {
            OnJumpPressed?.Invoke();
        }

        if (keyboard[attackKey].wasPressedThisFrame)
        {
            OnAttackPressed?.Invoke();
        }

        CheckSkillKey(keyboard, skillAKey, UI_GameSceneView.PlayerSkillSlot.A);
        CheckSkillKey(keyboard, skillSKey, UI_GameSceneView.PlayerSkillSlot.S);
        CheckSkillKey(keyboard, skillDKey, UI_GameSceneView.PlayerSkillSlot.D);
        CheckSkillKey(keyboard, skillFKey, UI_GameSceneView.PlayerSkillSlot.F);
    }
    #endregion

    #region Method
    private float ComputeMoveAxis(Keyboard keyboard)
    {
        float axis = 0f;

        if (keyboard[moveLeftKey].isPressed)
        {
            axis -= 1f;
        }

        if (keyboard[moveRightKey].isPressed)
        {
            axis += 1f;
        }

        return axis;
    }
    #endregion


/// <summary>_key가 이 프레임에 눌렸으면 OnSkillKeyPressed를 _slot과 함께 발생시킨다.</summary>
    private void CheckSkillKey(Keyboard keyboard, Key key, UI_GameSceneView.PlayerSkillSlot slot)
    {
        if (keyboard[key].wasPressedThisFrame)
        {
            OnSkillKeyPressed?.Invoke(slot);
        }
    }
}
