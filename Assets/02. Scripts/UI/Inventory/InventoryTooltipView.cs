using UnityEngine;
using UnityEngine.UI;

public class InventoryTooltipView : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private GameObject tooltipRoot;

    [Header("UI")]
    [SerializeField] private Text nameText;
    [SerializeField] private Text typeText;
    [SerializeField] private Text descriptionText;
    [SerializeField] private Text detailText;
    [SerializeField] private Text levelUpText;

    public void Show(PlayerAbilityModule module)
    {
        if (module == null)
        {
            Hide();
            return;
        }

        if (tooltipRoot != null)
            tooltipRoot.SetActive(true);

        if (nameText != null)
            nameText.text = module.DisplayName;

        if (typeText != null)
            typeText.text = module.IsActive ? "Active" : "Passive";

        if (descriptionText != null)
            descriptionText.text = module.Description;

        if (detailText != null)
            detailText.text = BuildDetailText(module);

        if (levelUpText != null)
            levelUpText.text = BuildLevelUpText(module);
    }

    public void Hide()
    {
        if (tooltipRoot != null)
            tooltipRoot.SetActive(false);
        else
            gameObject.SetActive(false);
    }

    private string BuildDetailText(PlayerAbilityModule module)
    {
        if (!module.IsActive)
            return "";

        return "Stamina Cost: " + module.StaminaCost + "\n" +
               "Cooldown: " + module.CooldownSeconds + "s";
    }

    private string BuildLevelUpText(PlayerAbilityModule module)
    {
        return "";
    }
}