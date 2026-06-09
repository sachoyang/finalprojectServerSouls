using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BossHUDView : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Boss Info")]
    [SerializeField] private Text bossNameText;

    [Header("HP")]
    [SerializeField] private Image hpFillImage;

    [Header("State")]
    [SerializeField] private Text stateText;

    [Header("Status")]
    [SerializeField] private StatusIconBarView statusIconBarView;

    private void Awake()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    public void SetVisible(bool isVisible)
    {
        if (canvasGroup == null)
            return;

        canvasGroup.alpha = isVisible ? 1f : 0f;
        canvasGroup.interactable = isVisible;
        canvasGroup.blocksRaycasts = isVisible;
    }

    public void SetBossName(string bossName)
    {
        if (bossNameText == null)
            return;

        bossNameText.text = string.IsNullOrEmpty(bossName) ? string.Empty : bossName;
    }

    public void SetHp(float currentHp, float maxHp)
    {
        if (hpFillImage == null)
            return;

        float hpRate = maxHp > 0f ? currentHp / maxHp : 0f;
        hpFillImage.fillAmount = Mathf.Clamp01(hpRate);
    }

    public void SetStateText(string stateName)
    {
        if (stateText == null)
            return;

        stateText.text = string.IsNullOrEmpty(stateName) ? string.Empty : stateName;
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

    public void Clear()
    {
        SetBossName(string.Empty);
        SetHp(0f, 1f);
        SetStateText(string.Empty);
        ClearStatuses();
    }
}