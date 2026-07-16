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

    [Header("Groggy")]
    [SerializeField] private Image groggyFillYellow;
    [SerializeField] private Image groggyFillPurple;
    [SerializeField] private Text groggyTimeText;

    [Header("Groggy Effect")]
    [SerializeField] private Image glassBreakEffect;
    [SerializeField] private float glassBreakFadeDuration = 0.6f;
    [SerializeField, Range(0f, 1f)] private float glassBreakStartAlpha = 0.3f;

    [Header("State")]
    [SerializeField] private Text stateText;

    [Header("Status")]
    [SerializeField] private StatusIconBarView statusIconBarView;

    private bool wasGroggy;
    private float glassBreakTimer;

    private void Awake()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        SetGlassBreakAlpha(0f);

        if (glassBreakEffect != null)
            glassBreakEffect.gameObject.SetActive(false);

        SetGroggyNormalMode();
    }

    private void Update()
    {
        UpdateGlassBreakEffect();
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

    public void SetGroggy(float currentGroggy, float maxGroggy, bool isGroggy, float remainingTime, float duration)
    {
        if (isGroggy)
        {
            SetGroggyActiveMode(remainingTime, duration);

            if (!wasGroggy)
                PlayGlassBreakEffect();
        }
        else
        {
            SetGroggyNormalMode();

            float ratio = maxGroggy > 0f
                ? Mathf.Clamp01(currentGroggy / maxGroggy)
                : 0f;

            // 노란 바 = '그로기 저항' 게이지. 그로기가 쌓일수록 줄어들고, 0이 되면 그로기 발동.
            if (groggyFillYellow != null)
                groggyFillYellow.fillAmount = 1f - ratio;
        }

        wasGroggy = isGroggy;
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
        SetGroggy(0f, 1f, false, 0f, 1f);
        SetStateText(string.Empty);
        ClearStatuses();
    }

    private void SetGroggyNormalMode()
    {
        if (groggyFillYellow != null)
            groggyFillYellow.gameObject.SetActive(true);

        if (groggyFillPurple != null)
            groggyFillPurple.gameObject.SetActive(false);

        if (groggyTimeText != null)
            groggyTimeText.gameObject.SetActive(false);
    }

    private void SetGroggyActiveMode(float remainingTime, float duration)
    {
        if (groggyFillYellow != null)
            groggyFillYellow.gameObject.SetActive(false);

        if (groggyFillPurple != null)
        {
            groggyFillPurple.gameObject.SetActive(true);

            float ratio = duration > 0f
                ? Mathf.Clamp01(remainingTime / duration)
                : 0f;

            groggyFillPurple.fillAmount = ratio;
        }

        if (groggyTimeText != null)
        {
            groggyTimeText.gameObject.SetActive(true);
            groggyTimeText.text = Mathf.Max(0f, remainingTime).ToString("0.00");
        }
    }

    private void PlayGlassBreakEffect()
    {
        if (glassBreakEffect == null)
            return;

        glassBreakTimer = glassBreakFadeDuration;
        glassBreakEffect.gameObject.SetActive(true);
        SetGlassBreakAlpha(glassBreakStartAlpha);
    }

    private void UpdateGlassBreakEffect()
    {
        if (glassBreakEffect == null || glassBreakTimer <= 0f)
            return;

        glassBreakTimer -= Time.deltaTime;

        float alpha = glassBreakFadeDuration > 0f
            ? glassBreakStartAlpha * Mathf.Clamp01(glassBreakTimer / glassBreakFadeDuration)
            : 0f;

        SetGlassBreakAlpha(alpha);

        if (glassBreakTimer <= 0f)
        {
            SetGlassBreakAlpha(0f);
            glassBreakEffect.gameObject.SetActive(false);
        }
    }

    private void SetGlassBreakAlpha(float alpha)
    {
        if (glassBreakEffect == null)
            return;

        Color color = glassBreakEffect.color;
        color.a = alpha;
        glassBreakEffect.color = color;
    }
}