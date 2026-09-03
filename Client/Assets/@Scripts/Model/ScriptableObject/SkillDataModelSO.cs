using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "SkillDataModelSO", menuName = "ScriptableObjectAssets/SkillDataModel")]
public class SkillDataModelSO : ScriptableObject
{
    public List<SkillData> skillDataList = new List<SkillData>();

    /// <summary>slot에 배정된 스킬 데이터를 찾는다. 없으면 null을 반환한다.</summary>
    public SkillData GetSkill(UI_GameSceneView.PlayerSkillSlot slot)
    {
        return skillDataList?.Find(s => s.slot == slot);
    }
}
