using UnityEngine;
using UnityEngine.UI;

public class BossHUDView : MonoBehaviour
{
    [Header("Boss HP")]
    [SerializeField] private Image bossHpFillImage;

    public void SetHp(float currentHp, float maxHp)
    {
        float hpRatio = GetSafeRatio(currentHp, maxHp);

        if (bossHpFillImage != null)
            bossHpFillImage.fillAmount = hpRatio;
    }

    private float GetSafeRatio(float currentValue, float maxValue)
    {
        if (maxValue <= 0f)
            return 0f;

        return Mathf.Clamp01(currentValue / maxValue);
    }
}
