using System;
using UnityEngine;

[Serializable]
public class SkillData
{
    /// <summary>
    /// 이 스킬이 배정된 플레이어 스킬 슬롯(A/S/D/F). 목록의 순서가 아니라 이 값으로 슬롯을 찾으므로,
    /// Inspector에서 리스트 순서를 바꿔도 슬롯 매핑이 깨지지 않는다.
    /// </summary>
    public UI_GameSceneView.PlayerSkillSlot slot;

    public string skillName;

    [Tooltip("스킬 재사용 대기시간(초).")]
    public float cooldown;

    [Tooltip("스킬 한 번 적중 시 입히는 데미지.")]
    public int damage;
}
