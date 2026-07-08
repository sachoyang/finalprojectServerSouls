using UnityEngine;

[CreateAssetMenu(menuName = "ServerSouls/Player Modules/Passive Ability")]
public sealed class PassiveAbilityModule : PlayerAbilityModule
{
    [Header("Level")]
    [SerializeField] private PassiveAbilityLevelData[] levelSettings = System.Array.Empty<PassiveAbilityLevelData>();

    [Header("Presentation")]
    [SerializeField] private AnimationClip animationClip;
    [SerializeField] private string animationStateName;
    [SerializeField] private string animationTrigger;
    [SerializeField, Min(0.01f)] private float animationSpeed = 1f;
    [SerializeField] private AnimatorStateFeatureMode rootMotionMode = AnimatorStateFeatureMode.Auto;
    [SerializeField] private AnimatorStateFeatureMode staminaRecoveryDelayMode = AnimatorStateFeatureMode.Auto;
    [SerializeField] private bool opensComboInput;
    [SerializeField, Range(0f, 1f)] private float comboInputOpenNormalizedTime = 0.72f;

    [Header("VFX")]
    [SerializeField] private GameObject effectPrefab;
    [SerializeField] private Vector3 effectLocalOffset;
    [SerializeField] private bool parentEffectToPlayer;

    [Header("Sound")]
    [SerializeField] private AudioClip soundClip;
    [SerializeField, Range(0f, 1f)] private float soundVolume = 1.0f;
    [SerializeField] private float soundDelay;

    public override AbilityType AbilityType => AbilityType.Passive;
    public override AnimationClip AnimationClip => animationClip;
    public override string AnimationStateName => animationStateName;
    public override string AnimationTrigger => animationTrigger;
    public override float AnimationSpeed => Mathf.Max(0.01f, animationSpeed);
    public override AnimatorStateFeatureMode RootMotionMode => rootMotionMode;
    public override AnimatorStateFeatureMode StaminaRecoveryDelayMode => staminaRecoveryDelayMode;
    public override bool OpensComboInput => opensComboInput;
    public override float ComboInputOpenNormalizedTime => comboInputOpenNormalizedTime;
    public override GameObject EffectPrefab => effectPrefab;
    public override Vector3 EffectLocalOffset => effectLocalOffset;
    public override bool ParentEffectToPlayer => parentEffectToPlayer;
    public override AudioClip SoundClip => soundClip;
    public override float SoundVolume => soundVolume;
    public override float SoundDelay => soundDelay;

    public override float GetMaxHealthBonus(int level)
    {
        EnsureLevelSettings();
        return level > 0 ? levelSettings[GetLevelIndex(level)].MaxHealthBonus : 0f;
    }

    public override float GetMaxStaminaBonus(int level)
    {
        EnsureLevelSettings();
        return level > 0 ? levelSettings[GetLevelIndex(level)].MaxStaminaBonus : 0f;
    }

    public override float GetDefenseRateBonus(int level)
    {
        EnsureLevelSettings();
        return level > 0 ? levelSettings[GetLevelIndex(level)].DefenseRateBonus : 0f;
    }

    public override float GetAttackDamageBonusRate(int level)
    {
        EnsureLevelSettings();
        return level > 0 ? levelSettings[GetLevelIndex(level)].AttackDamageBonusRate : 0f;
    }

    private void EnsureLevelSettings()
    {
        int maxLevel = MaxLevel;
        if (levelSettings == null)
            levelSettings = System.Array.Empty<PassiveAbilityLevelData>();

        if (levelSettings.Length != maxLevel)
            System.Array.Resize(ref levelSettings, maxLevel);

        for (int i = 0; i < levelSettings.Length; i++)
            levelSettings[i] ??= new PassiveAbilityLevelData();
    }
}
