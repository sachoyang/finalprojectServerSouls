using UnityEngine;

public readonly struct PartyMemberSkillUIData
{
    public readonly string SkillName;
    public readonly Sprite Icon;
    public readonly bool IsActive;

    public PartyMemberSkillUIData(string skillName, Sprite icon, bool isActive)
    {
        SkillName = skillName;
        Icon = icon;
        IsActive = isActive;
    }
}