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

    public void SetSlot(PlayerAbilityModule module, KeyCode keyCode, float remainingCooldown)
    {
        if (module == null)
        {
            Clear();
            return;
        }

        if (iconImage != null)
        {
            iconImage.sprite = module.Icon;
            iconImage.enabled = module.Icon != null;
        }

        if (keyText != null)
            keyText.text = keyCode.ToString();

        SetCooldown(remainingCooldown, module.CooldownSeconds);
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