using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PartyMemberHUDView : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private GameObject rootObject;

    [Header("Name")]
    [SerializeField] private Text nicknameText;

    [Header("HP")]
    [SerializeField] private Image hpFillImage;

    [Header("SP")]
    [SerializeField] private Image spFillImage;

    [Header("Skill")]
    [SerializeField] private PartyMemberSkillBarView skillBarView;

    [Header("Status")]
    [SerializeField] private StatusIconBarView statusIconBarView;

    private void Awake()
    {
        ResolveReferences();
    }

    public void SetData(PartyMemberUIData data)
    {
        ResolveReferences();
        SetVisible(true);
        SetName(data.DisplayName);
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

    public void SetStatuses(IReadOnlyList<ActiveStatusUIInfo> statuses)
    {
        if (statusIconBarView != null)
            statusIconBarView.SetStatuses(statuses);
    }

    public void ClearStatuses()
    {
        if (statusIconBarView != null)
            statusIconBarView.Clear();
    }

    private void SetName(string displayName)
    {
        if (nicknameText != null)
            nicknameText.text = string.IsNullOrWhiteSpace(displayName) ? "-" : displayName;
    }

    private void ResolveReferences()
    {
        if (nicknameText != null)
            return;

        Text[] texts = GetComponentsInChildren<Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            Text candidate = texts[i];
            if (candidate == null)
                continue;

            string objectName = candidate.gameObject.name;
            if (objectName.Contains("Name") ||
                objectName.Contains("Nickname") ||
                objectName.Contains("Player") ||
                objectName.Contains("이름"))
            {
                nicknameText = candidate;
                return;
            }
        }
    }

    private float GetSafeRatio(float currentValue, float maxValue)
    {
        if (maxValue <= 0f)
            return 0f;

        return Mathf.Clamp01(currentValue / maxValue);
    }
}
