using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PartyMemberHUDView : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private GameObject rootObject;

    [Header("HP")]
    [SerializeField] private Image hpFillImage;

    [Header("SP")]
    [SerializeField] private Image spFillImage;

    [Header("Skill")]
    [SerializeField] private PartyMemberSkillBarView skillBarView;

    public void SetData(PartyMemberUIData data)
    {
        SetVisible(true);
        SetStats(
            data.CurrentHealth,
            data.MaxHealth,
            data.CurrentStamina,
            data.MaxStamina);
    }

    public void SetVisible(bool isVisible)
    {
        if (rootObject != null)
            rootObject.SetActive(isVisible);
        else
            gameObject.SetActive(isVisible);
    }

    public void SetStats(float currentHp, float maxHp, float currentSp, float maxSp)
    {
        if (hpFillImage != null)
            hpFillImage.fillAmount = GetSafeRatio(currentHp, maxHp);

        if (spFillImage != null)
            spFillImage.fillAmount = GetSafeRatio(currentSp, maxSp);
    }

    public void SetSkills(IReadOnlyList<PartyMemberSkillUIData> skills)
    {
        if (skillBarView != null)
            skillBarView.SetSkills(skills);
    }

    public void ClearSkills()
    {
        if (skillBarView != null)
            skillBarView.Clear();
    }

    private float GetSafeRatio(float currentValue, float maxValue)
    {
        if (maxValue <= 0f)
            return 0f;

        return Mathf.Clamp01(currentValue / maxValue);
    }
}