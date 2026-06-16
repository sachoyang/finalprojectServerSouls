using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PartyMemberSkillBarView : MonoBehaviour
{
    [Header("Skill Slots")]
    [SerializeField] private Image[] skillIconImages;

    public void SetSkills(IReadOnlyList<PartyMemberSkillUIData> skills)
    {
        Clear();

        if (skills == null || skillIconImages == null)
            return;

        int count = Mathf.Min(skills.Count, skillIconImages.Length);

        for (int i = 0; i < count; i++)
        {
            if (skillIconImages[i] == null)
                continue;

            if (skills[i].Icon == null)
                continue;

            skillIconImages[i].gameObject.SetActive(true);
            skillIconImages[i].sprite = skills[i].Icon;
            skillIconImages[i].color = Color.white;
        }
    }

    public void Clear()
    {
        if (skillIconImages == null)
            return;

        for (int i = 0; i < skillIconImages.Length; i++)
        {
            if (skillIconImages[i] == null)
                continue;

            skillIconImages[i].gameObject.SetActive(true);
            skillIconImages[i].sprite = null;
            skillIconImages[i].color = Color.clear;
        }
    }
}