using UnityEngine;

[System.Serializable]
public class AbilityHitEvent
{
    [SerializeField] private string label = "Hit";
    [SerializeField, Range(0f, 1f)] private float startNormalizedTime = 0.35f;
    [SerializeField, Range(0f, 1f)] private float endNormalizedTime = 0.45f;
    [SerializeField] private float radius = 1.4f;
    [SerializeField] private float height = 1.8f;
    [SerializeField] private float centerHeight = 0.9f;
    [SerializeField] private float groggyDamage = 10f;
    [SerializeField] private float revivePower = 34f;
    [SerializeField] private Color previewColor = new Color(1f, 0.2f, 0f, 0.3f);

    public string Label => label;
    public float StartNormalizedTime => startNormalizedTime;
    public float EndNormalizedTime => Mathf.Max(startNormalizedTime, endNormalizedTime);
    public float Radius => Mathf.Max(0f, radius);
    public float Height => Mathf.Max(0f, height);
    public float CenterHeight => centerHeight;
    public float GroggyDamage => groggyDamage;
    public float RevivePower => revivePower;
    public Color PreviewColor => previewColor;
}

public enum AbilityType
{
    // 획득 즉시 효과를 적용하고 액티브 슬롯에는 들어가지 않는 능력.
    Passive,

    // 획득 후 액티브 슬롯에 등록되어 지정된 키로 사용하는 능력.
    Active
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
public class AbilityLevelData
{
    [SerializeField] private float damageMultiplier = 1f;
    [SerializeField] private float cooldownSeconds;
    [SerializeField] private float staminaCost;
    [SerializeField] private float maxHealthBonus;
    [SerializeField] private float maxStaminaBonus;
    [SerializeField] private float defenseRateBonus;
    [SerializeField] private float attackDamageBonusPercent;

    public float DamageMultiplier => damageMultiplier;
    public float CooldownSeconds => cooldownSeconds;
    public float StaminaCost => staminaCost;
    public float MaxHealthBonus => maxHealthBonus;
    public float MaxStaminaBonus => maxStaminaBonus;
    public float DefenseRateBonus => defenseRateBonus;
    public float AttackDamageBonusRate => attackDamageBonusPercent * 0.01f;

    public void SetActiveValues(float damage, float cooldown, float stamina)
    {
        damageMultiplier = damage;
        cooldownSeconds = cooldown;
        staminaCost = stamina;
    }
}

[CreateAssetMenu(menuName = "ServerSouls/Player Modules/Player Ability")]
public class PlayerAbilityModule : ScriptableObject
{
    [Header("Reward")]
    // DB 업로드 및 비트마스트 관리를 위한 고유 비트 번호 (0~63)
    [SerializeField] private int bitIndex;
    // 같은 능력인지 비교할 때 쓰는 고유 ID. 비워두면 에셋 이름을 ID처럼 사용한다.
    [SerializeField] private string abilityId;

    // UI에 보여줄 능력 이름. 비워두면 에셋 이름을 표시한다.
    [SerializeField] private string displayName;

    // UI에 보여줄 능력 설명.
    [TextArea]
    [SerializeField] private string description;

    // HUD나 보상 UI에서 표시할 능력 아이콘.
    [SerializeField] private Sprite icon;

    // 패시브인지 액티브인지 결정한다. 액티브만 키 슬롯에 등록된다.
    [SerializeField] private AbilityType abilityType = AbilityType.Passive;

    // 이 능력이 보상 후보로 등장할 수 있는 보스 단계 범위.
    [SerializeField, Range(1, 8)] private int minBossStage = 1;
    [SerializeField, Range(1, 8)] private int maxBossStage = 8;

    // Basic Skill 또는 개인 해금 상태가 반영된 최종 스킬 해금 여부.
    // Player reward pool 동기화 도구는 이 값을 보고 Player.prefab의 abilityPool에 포함할지 결정한다.
    [SerializeField] private bool unlockedSkill;

    // 모든 로그인 유저가 개인 해금 비트 없이도 보상 후보로 사용할 기본 스킬.
    // 서버 DB의 basic_skill 컬럼과 동기화된다.
    [SerializeField] private bool basicSkill;
    [SerializeField, Min(1)] private int maxLevel = 4;

    [Header("Level Settings")]
    [Tooltip("최대 레벨 수만큼 생성되며 인스펙터의 레벨 드롭다운으로 편집합니다.")]
    [SerializeField] private AbilityLevelData[] levelSettings = System.Array.Empty<AbilityLevelData>();

    [Header("Effect")]
    // 사용 또는 장착 시 회복할 체력량. 0이면 체력 회복 효과가 없다.
    [SerializeField] private float healthRestoreAmount;

    // 사용 또는 장착 시 회복할 스태미나량. 0이면 스태미나 회복 효과가 없다.
    [SerializeField] private float staminaRestoreAmount;

    [SerializeField] private PlayerAbilitySpecialEffect specialEffect;

    [Header("Presentation")]
    // 이 능력에 대응되는 애니메이션 클립. 실제 재생은 Animator Trigger를 권장하고, 이 필드는 에셋에서 어떤 모션을 쓰는지 확인하는 용도다.
    [SerializeField] private AnimationClip animationClip;

    // Setup Tool에서 Animator State를 만들 때 사용할 상태 이름. 비워두면 animationClip 이름을 쓸 수 있다.
    // 예: "Great Sword Slide Attack"
    [SerializeField] private string animationStateName;

    // Animator Trigger 파라미터로 재생할 때 사용하는 트리거 이름.
    [SerializeField] private string animationTrigger;

    // Animator State의 Speed 값. 프리뷰와 Animator 동기화 도구가 같은 값을 사용한다.
    [SerializeField, Min(0.01f)] private float animationSpeed = 1f;

    [Header("Animation State Behaviour")]
    [Tooltip("Auto uses the animation clip's root curves. Override it when clip import settings make detection unreliable.")]
    [SerializeField] private AnimatorStateFeatureMode rootMotionMode = AnimatorStateFeatureMode.Auto;

    [Tooltip("Auto delays recovery when this ability consumes stamina.")]
    [SerializeField] private AnimatorStateFeatureMode staminaRecoveryDelayMode = AnimatorStateFeatureMode.Auto;

    [Tooltip("Allows a follow-up input while this animation state is playing.")]
    [SerializeField] private bool opensComboInput;

    [SerializeField, Range(0f, 1f)] private float comboInputOpenNormalizedTime = 0.72f;

    [Header("VFX")]
    // 능력 사용 시 생성할 파티클/이펙트 프리팹.
    [SerializeField] private GameObject effectPrefab;

    // 플레이어 기준 로컬 좌표로 이펙트를 생성할 위치 오프셋.
    [SerializeField] private Vector3 effectLocalOffset;

    // true면 생성된 이펙트를 플레이어 자식으로 붙인다.
    [SerializeField] private bool parentEffectToPlayer;

    [Header("Hitbox")]
    // 능력 사용 시 생성할 공격 판정 프리팹.
    [SerializeField] private GameObject hitboxPrefab;

    // 플레이어 기준 로컬 좌표로 히트박스를 생성할 위치 오프셋.
    [SerializeField] private Vector3 hitboxLocalOffset;

    [SerializeField] private float hitboxRevivePower = 34f;

    [SerializeField] private float hitboxDelay;

    [SerializeField] private float hitboxLifetime = 0.3f;

    [Header("Hit Events")]
    [SerializeField] private AbilityHitEvent[] hitEvents = System.Array.Empty<AbilityHitEvent>();

    [Header("Sound")]
    [Tooltip("능력 사용 시 재생할 사운드 클립 (에셋 DB의 Key로 연동됨)")]
    [SerializeField] private AudioClip soundClip;
    [Tooltip("사운드 재생 볼륨 (0.0 ~ 1.0)")]
    [SerializeField, Range(0f, 1f)] private float soundVolume = 1.0f;
    [Tooltip("능력 발동 후 사운드가 재생되기까지의 지연 시간(초)")]
    [SerializeField] private float soundDelay = 0.0f;

    public int BitIndex => bitIndex;
    public string AbilityId => string.IsNullOrWhiteSpace(abilityId) ? name : abilityId;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    public string Description => description;
    public Sprite Icon => icon;
    public AbilityType AbilityType => abilityType;
    public bool IsActive => abilityType == AbilityType.Active;
    public float StaminaCost => GetStaminaCost(1);
    public float CooldownSeconds => GetCooldownSeconds(1);
    public bool UnlockedSkill => unlockedSkill;
    public bool BasicSkill => basicSkill;
    public int MaxLevel => Mathf.Clamp(maxLevel, 1, byte.MaxValue);
    public float MaxHealthBonus => GetMaxHealthBonus(1);
    public float MaxStaminaBonus => GetMaxStaminaBonus(1);
    public float DefenseRateBonus => GetDefenseRateBonus(1);
    public float AttackDamageBonusRate => GetAttackDamageBonusRate(1);
    public AnimationClip AnimationClip => animationClip;
    public string AnimationStateName => animationStateName;
    public string AnimationTrigger => animationTrigger;
    public float AnimationSpeed => Mathf.Max(0.01f, animationSpeed);
    public bool UsesRootMotion =>
        rootMotionMode == AnimatorStateFeatureMode.Enabled ||
        (rootMotionMode == AnimatorStateFeatureMode.Auto &&
         animationClip != null &&
         animationClip.hasRootCurves);
    public bool DelaysStaminaRecovery =>
        staminaRecoveryDelayMode == AnimatorStateFeatureMode.Enabled ||
        (staminaRecoveryDelayMode == AnimatorStateFeatureMode.Auto && GetStaminaCost(1) > 0f);
    public bool OpensComboInput => opensComboInput;
    public float ComboInputOpenNormalizedTime => comboInputOpenNormalizedTime;
    public float HealthRestoreAmount => healthRestoreAmount;
    public float StaminaRestoreAmount => staminaRestoreAmount;
    public PlayerAbilitySpecialEffect SpecialEffect => specialEffect;
    public GameObject EffectPrefab => effectPrefab;
    public Vector3 EffectLocalOffset => effectLocalOffset;
    public bool ParentEffectToPlayer => parentEffectToPlayer;
    public GameObject HitboxPrefab => hitboxPrefab;
    public Vector3 HitboxLocalOffset => hitboxLocalOffset;
    public float HitboxDamage => GetDamageMultiplier(1);
    public float HitboxRevivePower => hitboxRevivePower;
    public float HitboxDelay => hitboxDelay;
    public float HitboxLifetime => hitboxLifetime;
    public AbilityHitEvent[] HitEvents => hitEvents;

    public AudioClip SoundClip => soundClip;
    public float SoundVolume => soundVolume;
    public float SoundDelay => soundDelay;
    public float GetDamageMultiplier(int level)
    {
        return GetLevelData(level).DamageMultiplier;
    }

    public float GetCooldownSeconds(int level)
    {
        return GetLevelData(level).CooldownSeconds;
    }

    public float GetStaminaCost(int level)
    {
        return GetLevelData(level).StaminaCost;
    }

    public float GetMaxHealthBonus(int level)
    {
        if (level <= 0)
            return 0f;

        return GetLevelData(level).MaxHealthBonus;
    }

    public float GetMaxStaminaBonus(int level)
    {
        if (level <= 0)
            return 0f;

        return GetLevelData(level).MaxStaminaBonus;
    }

    public float GetDefenseRateBonus(int level)
    {
        if (level <= 0)
            return 0f;

        return GetLevelData(level).DefenseRateBonus;
    }

    public float GetAttackDamageBonusRate(int level)
    {
        if (level <= 0)
            return 0f;

        return GetLevelData(level).AttackDamageBonusRate;
    }

    public bool CanAppearAtStage(int bossStage)
    {
        return bossStage >= minBossStage && bossStage <= maxBossStage;
    }

    // Basic Skill과 유저별 해금 비트를 합산한 런타임 보상 풀 결과를 반영한다.
    public void SetUnlockedSkill(bool value)
    {
        unlockedSkill = value;
    }

    private AbilityLevelData GetLevelData(int level)
    {
        EnsureLevelSettings();

        int index = Mathf.Clamp(level, 1, MaxLevel) - 1;
        return levelSettings[index];
    }

    private void EnsureLevelSettings()
    {
        if (levelSettings == null)
            levelSettings = System.Array.Empty<AbilityLevelData>();

        if (levelSettings.Length != MaxLevel)
            System.Array.Resize(ref levelSettings, MaxLevel);

        for (int i = 0; i < levelSettings.Length; i++)
            levelSettings[i] ??= new AbilityLevelData();
    }

    // ==========================================
    // 🔥 서버 데이터로 모듈을 조립하는 런타임 초기화 함수
    // ==========================================
    public void InitializeFromDB(AbilityDBData dbData)
    {
        this.name = dbData.ability_id; // 메모리 상의 에셋 이름

        this.bitIndex = dbData.bit_index;

        this.abilityId = dbData.ability_id;
        this.displayName = dbData.display_name;
        this.description = dbData.description;

        this.abilityType = (dbData.ability_type == "Active") ? AbilityType.Active : AbilityType.Passive;
        this.basicSkill = dbData.basic_skill != 0;

        EnsureLevelSettings();
        for (int i = 0; i < levelSettings.Length; i++)
        {
            levelSettings[i].SetActiveValues(
                dbData.damage_multiplier,
                dbData.cooldown_seconds,
                dbData.stamina_cost);
        }
        this.hitboxLifetime = dbData.duration; // 지속시간 매핑

        // 특수 효과 Enum 매핑
        if (System.Enum.TryParse(dbData.special_effect, out PlayerAbilitySpecialEffect effect))
        {
            this.specialEffect = effect;
        }
    }
}
