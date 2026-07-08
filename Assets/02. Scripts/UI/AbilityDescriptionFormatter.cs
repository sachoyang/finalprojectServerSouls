using System.Globalization;
using UnityEngine;

public static class AbilityDescriptionFormatter
{
    public static string Format(PlayerAbilityModule module, int level)
    {
        if (module == null)
            return string.Empty;

        int resolvedLevel = Mathf.Clamp(level, 1, module.MaxLevel);
        string resolved = module.Description ?? string.Empty;

        resolved = ReplaceToken(resolved, "level", resolvedLevel);
        resolved = ReplaceToken(resolved, "maxLevel", module.MaxLevel);
        resolved = ReplaceToken(resolved, "skillMultiplier", module.GetDamageMultiplier(resolvedLevel));
        resolved = ReplaceToken(resolved, "damage", module.GetDamageMultiplier(resolvedLevel));
        resolved = ReplaceToken(resolved, "staminaCost", module.StaminaCost);
        resolved = ReplaceToken(resolved, "cooldown", module.CooldownSeconds);

        resolved = ReplaceToken(resolved, "maxHealthIncrease", module.GetMaxHealthBonus(resolvedLevel));
        resolved = ReplaceToken(resolved, "health", module.GetMaxHealthBonus(resolvedLevel));
        resolved = ReplaceToken(resolved, "maxStaminaIncrease", module.GetMaxStaminaBonus(resolvedLevel));
        resolved = ReplaceToken(resolved, "stamina", module.GetMaxStaminaBonus(resolvedLevel));
        resolved = ReplaceToken(resolved, "defenseIncrease", module.GetDefenseRateBonus(resolvedLevel) * 100f);
        resolved = ReplaceToken(resolved, "defense", module.GetDefenseRateBonus(resolvedLevel) * 100f);
        resolved = ReplaceToken(resolved, "attackIncrease", module.GetAttackDamageBonusRate(resolvedLevel) * 100f);
        resolved = ReplaceToken(resolved, "attack", module.GetAttackDamageBonusRate(resolvedLevel) * 100f);

        resolved = ReplaceToken(resolved, "healthRestore", module.GetHealthRestoreAmount(resolvedLevel));
        resolved = ReplaceToken(resolved, "heal", module.GetHealthRestoreAmount(resolvedLevel));
        resolved = ReplaceToken(resolved, "staminaRestore", module.GetStaminaRestoreAmount(resolvedLevel));

        AbilityHitEvent[] hitEvents = module.HitEvents;
        resolved = ReplaceToken(resolved, "hitCount", hitEvents != null ? hitEvents.Length : 0);
        float skillMultiplier = module.GetDamageMultiplier(resolvedLevel);
        if (hitEvents != null)
        {
            for (int i = 0; i < hitEvents.Length; i++)
            {
                float hitMultiplier = hitEvents[i] != null
                    ? hitEvents[i].DamageRate * skillMultiplier
                    : 0f;
                resolved = ReplaceToken(resolved, $"hit{i + 1}", hitMultiplier);
            }
        }

        return resolved;
    }

    private static string ReplaceToken(string source, string token, float value)
    {
        return source.Replace(
            $"{{{token}}}",
            value.ToString("0.##", CultureInfo.InvariantCulture));
    }
}
