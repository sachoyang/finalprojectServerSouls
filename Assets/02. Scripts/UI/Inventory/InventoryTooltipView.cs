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

    public void Show(PlayerAbilityModule module, int level)
    {
        if (module == null)
        {
            Hide();
            return;
        }

        if (tooltipRoot != null)
            tooltipRoot.SetActive(true);

        if (nameText != null)
            nameText.text = $"{module.DisplayName}  Lv.{Mathf.Clamp(level, 1, module.MaxLevel)}";

        if (typeText != null)
            typeText.text = module.IsActive ? "Active" : "Passive";

        if (descriptionText != null)
            descriptionText.text = module.Description;

        if (detailText != null)
            detailText.text = BuildDetailText(module, level);

        if (levelUpText != null)
            levelUpText.text = BuildLevelUpText(module, level);
    }

    public void Hide()
    {
        if (tooltipRoot != null)
            tooltipRoot.SetActive(false);
        else
            gameObject.SetActive(false);
    }

    private string BuildDetailText(PlayerAbilityModule module, int level)
    {
        if (!module.IsActive)
        {
            return "Max Health: +" + module.GetMaxHealthBonus(level) + "\n" +
                   "Max Stamina: +" + module.GetMaxStaminaBonus(level) + "\n" +
                   "Defense: +" + module.GetDefenseRateBonus(level) + "\n" +
                   "Attack: +" + (module.GetAttackDamageBonusRate(level) * 100f) + "%";
        }

        return "Damage: x" + module.GetDamageMultiplier(level) + "\n" +
               "Stamina Cost: " + module.GetStaminaCost(level) + "\n" +
               "Cooldown: " + module.GetCooldownSeconds(level) + "s";
    }

    private string BuildLevelUpText(PlayerAbilityModule module, int level)
    {
        if (level >= module.MaxLevel)
            return "MAX LEVEL";

        int nextLevel = level + 1;
        if (module.IsActive)
        {
            return $"Next Lv.{nextLevel}  Damage x{module.GetDamageMultiplier(nextLevel)}, " +
                   $"Stamina {module.GetStaminaCost(nextLevel)}, Cooldown {module.GetCooldownSeconds(nextLevel)}s";
        }

        return $"Next Lv.{nextLevel}  HP +{module.GetMaxHealthBonus(nextLevel)}, " +
               $"Stamina +{module.GetMaxStaminaBonus(nextLevel)}, " +
               $"Defense +{module.GetDefenseRateBonus(nextLevel)}, " +
               $"Attack +{module.GetAttackDamageBonusRate(nextLevel) * 100f}%";
    }
}
