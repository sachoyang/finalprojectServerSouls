using UnityEngine;
using UnityEngine.UI;

public class SkillSlotHUDView : MonoBehaviour
{
    [Header("Icon")]
    [SerializeField] private Image iconImage;

    [Header("Cooldown")]
    [SerializeField] private Image cooldownOverlayImage;
    [SerializeField] private Text cooldownText;

    [Header("Key")]
    [SerializeField] private Text keyText;

    public void SetData(SkillSlotUIData data)
    {
        // SkillSlotUIData는 이미 표시용으로 가공된 값이므로 이 뷰는 화면 갱신만 담당한다.
        if (data.IsEmpty)
        {
            Clear();
            return;
        }

        if (iconImage != null)
        {
            iconImage.sprite = data.Icon;
            iconImage.enabled = data.Icon != null;
        }

        if (keyText != null)
            keyText.text = data.KeyCode != KeyCode.None ? data.KeyCode.ToString() : "";

        SetCooldown(data.CooldownRemaining, data.CooldownDuration);
    }

    public void Clear()
    {
        if (iconImage != null)
        {
            iconImage.sprite = null;
            iconImage.enabled = false;
        }

        if (cooldownOverlayImage != null)
        {
            cooldownOverlayImage.fillAmount = 0f;
            cooldownOverlayImage.gameObject.SetActive(false);
        }

        if (cooldownText != null)
        {
            cooldownText.text = "";
            cooldownText.gameObject.SetActive(false);
        }

        if (keyText != null)
            keyText.text = "";
    }

    private void SetCooldown(float remainingCooldown, float cooldownDuration)
    {
        bool isCooldown = remainingCooldown > 0f && cooldownDuration > 0f;

        if (cooldownOverlayImage != null)
        {
            cooldownOverlayImage.gameObject.SetActive(isCooldown);

            if (isCooldown)
            {
                float cooldownRatio = Mathf.Clamp01(remainingCooldown / cooldownDuration);
                cooldownOverlayImage.fillAmount = cooldownRatio;
            }
        }

        if (cooldownText != null)
        {
            cooldownText.gameObject.SetActive(isCooldown);

            if (isCooldown)
                cooldownText.text = Mathf.CeilToInt(remainingCooldown).ToString();
        }
    }
}
