using UnityEngine;
using UnityEngine.Serialization;

[System.Serializable]
public class AbilityHitEvent
{
    [SerializeField] private string label = "Hit";
    [SerializeField, Range(0f, 1f)] private float startNormalizedTime = 0.35f;
    [SerializeField, Range(0f, 1f)] private float endNormalizedTime = 0.45f;
    [SerializeField] private float radius = 1.4f;
    [SerializeField] private float height = 1.8f;
    [SerializeField] private float centerHeight = 0.9f;
    [SerializeField] private float damageRate = 1f;
    [SerializeField] private float groggyDamage = 10f;
    [SerializeField] private float revivePower = 34f;
    [SerializeField] private Color previewColor = new Color(1f, 0.2f, 0f, 0.3f);

    public string Label => label;
    public float StartNormalizedTime => startNormalizedTime;
    public float EndNormalizedTime => Mathf.Max(startNormalizedTime, endNormalizedTime);
    public float Radius => Mathf.Max(0f, radius);
    public float Height => Mathf.Max(0f, height);
    public float CenterHeight => centerHeight;
    public float DamageRate => damageRate;
    public float GroggyDamage => groggyDamage;
    public float RevivePower => revivePower;
    public Color PreviewColor => previewColor;
}

public enum AbilityType
{
    Passive,
    Active,
    Utility
}

public enum PlayerAbilitySpecialEffect
{
    None,
    UnlockBasicAttackCombo
}

public enum AnimatorStateFeatureMode
{
    Auto,
    Enabled,
    Disabled
}

[System.Serializable]
public class ActiveAbilityLevelData
{
    [SerializeField] private float damageMultiplier = 1f;

    public float DamageMultiplier => damageMultiplier;
}

[System.Serializable]
public class PassiveAbilityLevelData
{
    [SerializeField] private float maxHealthBonus;
    [SerializeField] private float maxStaminaBonus;
    [Tooltip("현재 방어력에 합연산됩니다. 10 입력 시 10%, 100 입력 시 100%입니다.")]
    [FormerlySerializedAs("defenseRateBonus")]
    [SerializeField] private float defenseBonusPercent;
    [Tooltip("현재 공격력 증가율에 합연산됩니다. 10 입력 시 10%, 100 입력 시 100%입니다.")]
    [SerializeField] private float attackDamageBonusPercent;

    public float MaxHealthBonus => maxHealthBonus;
    public float MaxStaminaBonus => maxStaminaBonus;
    public float DefenseRateBonus => defenseBonusPercent * 0.01f;
    public float AttackDamageBonusRate => attackDamageBonusPercent * 0.01f;
}

[System.Serializable]
public class UtilityAbilityLevelData
{
    [SerializeField] private float healthRestoreAmount;
    [SerializeField] private float staminaRestoreAmount;

    public float HealthRestoreAmount => healthRestoreAmount;
    public float StaminaRestoreAmount => staminaRestoreAmount;
}

public abstract class PlayerAbilityModule : ScriptableObject
{
    [Header("Reward")]
    [SerializeField] private int bitIndex;
    [SerializeField] private string abilityId;
    [SerializeField] private string displayName;
    [TextArea]
    [SerializeField] private string description;
    [SerializeField] private Sprite icon;

    [FormerlySerializedAs("minBossStage")]
    [SerializeField, Min(1)] private int appearStage = 1;

    [SerializeField] private bool unlockedSkill;
    [SerializeField] private bool basicSkill;
    [SerializeField, Min(1)] private int maxLevel = 4;

    public int BitIndex => bitIndex;
    public string AbilityId => string.IsNullOrWhiteSpace(abilityId) ? name : abilityId;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    public string Description => description;
    public Sprite Icon => icon;
    public abstract AbilityType AbilityType { get; }
    public bool IsActive => AbilityType == AbilityType.Active;
    public bool IsPassive => AbilityType == AbilityType.Passive;
    public bool IsUtility => AbilityType == AbilityType.Utility;
    public virtual bool UsesActiveSlot => IsActive;
    public virtual float StaminaCost => 0f;
    public virtual float CooldownSeconds => 0f;
    public bool UnlockedSkill => unlockedSkill;
    public bool BasicSkill => basicSkill;
    public int MaxLevel => Mathf.Clamp(maxLevel, 1, byte.MaxValue);
    public float MaxHealthBonus => GetMaxHealthBonus(1);
    public float MaxStaminaBonus => GetMaxStaminaBonus(1);
    public float DefenseRateBonus => GetDefenseRateBonus(1);
    public float AttackDamageBonusRate => GetAttackDamageBonusRate(1);
    public virtual AnimationClip AnimationClip => null;
    public virtual string AnimationStateName => string.Empty;
    public virtual string AnimationTrigger => string.Empty;
    public virtual float AnimationSpeed => 1f;
    public virtual AnimatorStateFeatureMode RootMotionMode => AnimatorStateFeatureMode.Auto;
    public virtual AnimatorStateFeatureMode StaminaRecoveryDelayMode => AnimatorStateFeatureMode.Auto;
    public virtual bool UsesRootMotion =>
        RootMotionMode == AnimatorStateFeatureMode.Enabled ||
        (RootMotionMode == AnimatorStateFeatureMode.Auto &&
         AnimationClip != null &&
         AnimationClip.hasRootCurves);
    public virtual bool DelaysStaminaRecovery =>
        StaminaRecoveryDelayMode == AnimatorStateFeatureMode.Enabled ||
        (StaminaRecoveryDelayMode == AnimatorStateFeatureMode.Auto && StaminaCost > 0f);
    public virtual bool OpensComboInput => false;
    public virtual float ComboInputOpenNormalizedTime => 0f;
    public float HealthRestoreAmount => GetHealthRestoreAmount(1);
    public float StaminaRestoreAmount => GetStaminaRestoreAmount(1);
    public virtual PlayerAbilitySpecialEffect SpecialEffect => PlayerAbilitySpecialEffect.None;
    public virtual GameObject EffectPrefab => null;
    public virtual Vector3 EffectLocalOffset => Vector3.zero;
    public virtual bool ParentEffectToPlayer => false;
    public virtual GameObject HitboxPrefab => null;
    public virtual Vector3 HitboxLocalOffset => Vector3.zero;
    public virtual float HitboxRevivePower => 0f;
    public virtual float HitboxDelay => 0f;
    public virtual float HitboxLifetime => 0f;
    public virtual AbilityHitEvent[] HitEvents => System.Array.Empty<AbilityHitEvent>();
    public virtual AudioClip SoundClip => null;
    public virtual float SoundVolume => 1f;
    public virtual float SoundDelay => 0f;

    public virtual float GetDamageMultiplier(int level) => 1f;
    public virtual float GetMaxHealthBonus(int level) => 0f;
    public virtual float GetMaxStaminaBonus(int level) => 0f;
    public virtual float GetDefenseRateBonus(int level) => 0f;
    public virtual float GetAttackDamageBonusRate(int level) => 0f;
    public virtual float GetHealthRestoreAmount(int level) => 0f;
    public virtual float GetStaminaRestoreAmount(int level) => 0f;

    public bool CanAppearAtStage(int bossStage)
    {
        return bossStage >= appearStage;
    }

    public void SetUnlockedSkill(bool value)
    {
        unlockedSkill = value;
    }

    public virtual void InitializeFromDB(AbilityDBData dbData)
    {
        name = dbData.ability_id;

        bitIndex = dbData.bit_index;
        abilityId = dbData.ability_id;
        displayName = dbData.display_name;
        description = dbData.description;
        basicSkill = dbData.basic_skill != 0;
    }

    protected int GetLevelIndex(int level)
    {
        return Mathf.Clamp(level, 1, MaxLevel) - 1;
    }
}
