public enum AddressableKey
{
    None,
    UI_ProgressBar,
    UI_LobbyScene,
    UI_GameScene,
    BasicCharacter,
    BasicDragon,
    Tile001,
    Tile002,
    TileFloor,
    TileRockFloor,
    TileStairs,
    SlashNormal,
    WheelWindNormal,
    SkillDataModelSO,
    DragonSkillDataModelSO
}

/// <summary>
/// 드래곤 스킬 슬롯(Q/W/E/R). PlayerSkillSlot(A/S/D/F)과 동일한 방식으로,
/// 리스트 순서가 아니라 이 값으로 슬롯을 찾는다.
/// </summary>
public enum DragonSkillSlot
{
    Q = 0,
    W = 1,
    E = 2,
    R = 3
}

public enum DragonType
{
    Aggressive,  // 공격형
    Guardian     // 방어형
}

public enum DragonElement
{
    Ice,      // 얼음속성
    Fire,     // 불속성
    Lightning // 번개속성
}

public enum PlayerMoveState
{
    IsIdle,
    IsMove,
    IsDash,
    IsJump
}

public enum DragonMoveState
{
    IsIdle,
    IsMove,
    IsDash,
    IsJump
}

/// <summary>
/// 무기 오브젝트 이름 접두사와 매핑되는 무기 분류.
/// OH(One-Handed) = 한손무기류, TH(Two-Handed) = 두손무기류, Shield = 방패류, Wand = 원드류, Spear = 창류.
/// </summary>
public enum WeaponType
{
    NoWeapon,
    OneHanded,
    TwoHanded,
    Shield,
    Wand,
    Spear
}
