using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class PlayerHUDView : MonoBehaviour
{
    [Header("HP")]
    [SerializeField] private Image hpFillImage;

    [Header("SP")]
    [SerializeField] private Image spFillImage;
    [SerializeField] private Color staminaWarningColor = new Color(1f, 0.24f, 0.16f, 1f);
    [SerializeField] private float staminaWarningFlashSeconds = 0.18f;

    [Header("Status")]
    [SerializeField] private StatusIconBarView statusIconBarView;

    private Coroutine staminaWarningRoutine;
    private Color defaultSpFillColor = Color.white;
    private bool hasDefaultSpFillColor;

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
        {
            CacheDefaultSpFillColor();
            spFillImage.fillAmount = spRatio;
        }
    }

    public void ShowStaminaUseFailed()
    {
        if (spFillImage == null || !gameObject.activeInHierarchy)
        {
            return;
        }

        CacheDefaultSpFillColor();
        if (staminaWarningRoutine != null)
        {
            StopCoroutine(staminaWarningRoutine);
        }

        staminaWarningRoutine = StartCoroutine(FlashStaminaWarning());
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

    private void CacheDefaultSpFillColor()
    {
        if (hasDefaultSpFillColor || spFillImage == null)
        {
            return;
        }

        defaultSpFillColor = spFillImage.color;
        hasDefaultSpFillColor = true;
    }

    private IEnumerator FlashStaminaWarning()
    {
        spFillImage.color = staminaWarningColor;
        yield return new WaitForSeconds(staminaWarningFlashSeconds);
        spFillImage.color = defaultSpFillColor;
        staminaWarningRoutine = null;
    }
}
