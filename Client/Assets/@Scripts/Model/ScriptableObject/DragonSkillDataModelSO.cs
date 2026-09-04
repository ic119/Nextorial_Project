using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "DragonSkillDataModelSO", menuName = "ScriptableObjectAssets/DragonSkillDataModel")]
public class DragonSkillDataModelSO : ScriptableObject
{
    public List<DragonSkillData> skillDataList = new List<DragonSkillData>();

    /// <summary>slot에 배정된 스킬 데이터를 찾는다. 없으면 null을 반환한다.</summary>
    public DragonSkillData GetSkill(DragonSkillSlot slot)
    {
        return skillDataList?.Find(s => s.slot == slot);
    }
}
