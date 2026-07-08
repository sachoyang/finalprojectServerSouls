using UnityEngine;

[CreateAssetMenu(menuName = "ServerSouls/Player Modules/Active Ability")]
public sealed class ActiveAbilityModule : PlayerAbilityModule
{
    [Header("Skill")]
    [SerializeField] private float staminaCost;
    [SerializeField] private float cooldownSeconds;

    [Header("Level")]
    [SerializeField] private ActiveAbilityLevelData[] levelSettings = System.Array.Empty<ActiveAbilityLevelData>();

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

    [Header("Hitbox")]
    [SerializeField] private GameObject hitboxPrefab;
    [SerializeField] private Vector3 hitboxLocalOffset;
    [SerializeField] private float hitboxRevivePower = 34f;
    [SerializeField] private float hitboxDelay;
    [SerializeField] private float hitboxLifetime = 0.3f;
    [SerializeField] private AbilityHitEvent[] hitEvents = System.Array.Empty<AbilityHitEvent>();

    public override AbilityType AbilityType => AbilityType.Active;
    public override bool UsesActiveSlot => true;
    public override float StaminaCost => staminaCost;
    public override float CooldownSeconds => cooldownSeconds;
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
    public override GameObject HitboxPrefab => hitboxPrefab;
    public override Vector3 HitboxLocalOffset => hitboxLocalOffset;
    public override float HitboxRevivePower => hitboxRevivePower;
    public override float HitboxDelay => hitboxDelay;
    public override float HitboxLifetime => hitboxLifetime;
    public override AbilityHitEvent[] HitEvents => hitEvents;
    public override AudioClip SoundClip => soundClip;
    public override float SoundVolume => soundVolume;
    public override float SoundDelay => soundDelay;

    public override float GetDamageMultiplier(int level)
    {
        EnsureLevelSettings();
        return levelSettings[GetLevelIndex(level)].DamageMultiplier;
    }

    public override void InitializeFromDB(AbilityDBData dbData)
    {
        base.InitializeFromDB(dbData);
        staminaCost = dbData.stamina_cost;
        cooldownSeconds = dbData.cooldown_seconds;

        // 레벨별 배율을 DB에서 재구성. 히트박스 수명 등 연출값은 DB에 없으므로 로컬 유지.
        int max = MaxLevel;
        var arr = new ActiveAbilityLevelData[max];
        for (int i = 0; i < max; i++) arr[i] = new ActiveAbilityLevelData();
        if (dbData.levels != null)
        {
            foreach (AbilityLevelDBData lv in dbData.levels)
            {
                int idx = lv.level - 1;
                if (idx >= 0 && idx < max) arr[idx].ApplyFromDB(lv);
            }
        }
        levelSettings = arr;
    }

    private void EnsureLevelSettings()
    {
        int maxLevel = MaxLevel;
        if (levelSettings == null)
            levelSettings = System.Array.Empty<ActiveAbilityLevelData>();

        if (levelSettings.Length != maxLevel)
            System.Array.Resize(ref levelSettings, maxLevel);

        for (int i = 0; i < levelSettings.Length; i++)
            levelSettings[i] ??= new ActiveAbilityLevelData();
    }
}
