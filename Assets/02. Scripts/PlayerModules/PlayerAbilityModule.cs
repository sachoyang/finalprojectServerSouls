using UnityEngine;

public abstract class PlayerAbilityModule : ScriptableObject
{
    [Header("Reward")]
    [SerializeField] private string abilityId;
    [SerializeField] private string displayName;
    [TextArea]
    [SerializeField] private string description;
    [SerializeField] private AbilityType abilityType = AbilityType.Passive;
    [SerializeField, Range(1, 8)] private int minBossStage = 1;
    [SerializeField, Range(1, 8)] private int maxBossStage = 8;

    [Header("Active Settings")]
    [SerializeField] private float staminaCost;
    [SerializeField] private float cooldownSeconds;

    public string AbilityId => string.IsNullOrWhiteSpace(abilityId) ? name : abilityId;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    public string Description => description;
    public AbilityType AbilityType => abilityType;
    public bool IsActive => abilityType == AbilityType.Active;
    public float StaminaCost => staminaCost;
    public float CooldownSeconds => cooldownSeconds;

    public bool CanAppearAtStage(int bossStage)
    {
        return bossStage >= minBossStage && bossStage <= maxBossStage;
    }

    public virtual bool CanEquip(PlayerAbilityContext context)
    {
        return context.Owner != null;
    }

    public virtual void OnEquipped(PlayerAbilityContext context)
    {
    }

    public virtual void OnUnequipped(PlayerAbilityContext context)
    {
    }

    public virtual bool CanActivate(PlayerAbilityContext context)
    {
        return IsActive && context.Owner != null && (context.Stats == null || !context.Stats.IsDead);
    }

    public virtual void Activate(PlayerAbilityContext context)
    {
    }
}
