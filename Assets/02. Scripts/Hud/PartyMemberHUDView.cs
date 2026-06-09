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

    public void SetData(PartyMemberUIData data)
    {
        // 파티원 슬롯은 네트워크 상태를 수정하지 않고, 전달받은 UI 데이터만 표시한다.
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

    private float GetSafeRatio(float currentValue, float maxValue)
    {
        if (maxValue <= 0f)
            return 0f;

        return Mathf.Clamp01(currentValue / maxValue);
    }
}
