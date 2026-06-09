using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PlayerHUDView : MonoBehaviour
{
    [Header("HP")]
    [SerializeField] private Image hpFillImage;

    [Header("SP")]
    [SerializeField] private Image spFillImage;

    [Header("Status")]
    [SerializeField] private StatusIconBarView statusIconBarView;

    public void SetHp(float currentHp, float maxHp)
    {
        float hpRatio = GetSafeRatio(currentHp, maxHp);

        if (hpFillImage != null)
            hpFillImage.fillAmount = hpRatio;
    }

    public void SetSp(float currentSp, float maxSp)
    {
        float spRatio = GetSafeRatio(currentSp, maxSp);

        if (spFillImage != null)
            spFillImage.fillAmount = spRatio;
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

    private float GetSafeRatio(float currentValue, float maxValue)
    {
        if (maxValue <= 0f)
            return 0f;

        return Mathf.Clamp01(currentValue / maxValue);
    }
}