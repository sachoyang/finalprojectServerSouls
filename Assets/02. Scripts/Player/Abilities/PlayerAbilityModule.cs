using UnityEngine;

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

[CreateAssetMenu(menuName = "ServerSouls/Player Modules/Player Ability")]
public class PlayerAbilityModule : ScriptableObject
{
    [Header("Reward")]
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

    // Player reward pool 동기화 도구가 이 값을 보고 Player.prefab의 abilityPool에 포함할지 결정한다.
    // 테스트용 또는 미완성 모듈은 체크를 끄면 보스 보상 후보에서 제외된다.
    [SerializeField] private bool includeInRewardPool = true;

    [Header("Active Settings")]
    // 액티브 능력을 사용할 때 소모하는 스태미나. 0이면 무료다.
    [SerializeField] private float staminaCost;

    // 액티브 능력을 사용한 뒤 다시 사용할 수 있을 때까지의 시간.
    [SerializeField] private float cooldownSeconds;

    [Header("Effect")]
    // 사용 또는 장착 시 회복할 체력량. 0이면 체력 회복 효과가 없다.
    [SerializeField] private float healthRestoreAmount;

    // 사용 또는 장착 시 회복할 스태미나량. 0이면 스태미나 회복 효과가 없다.
    [SerializeField] private float staminaRestoreAmount;

    [SerializeField] private PlayerAbilitySpecialEffect specialEffect;

    [Header("Passive Stat Bonuses")]
    // 패시브 모듈 획득 시 최대 체력에 더할 값. 실제 적용 로직은 PlayerStats의 스탯 보너스 처리에서 사용한다.
    [SerializeField] private float maxHealthBonus;

    // 패시브 모듈 획득 시 최대 스태미나에 더할 값.
    [SerializeField] private float maxStaminaBonus;

    // 패시브 모듈 획득 시 방어율에 더할 값. 0.1이면 최종 피해를 10% 더 줄이는 용도로 사용한다.
    [SerializeField, Range(-1f, 1f)] private float defenseRateBonus;


    // 패시브 모듈 획득 시 기본 공격 데미지에 곱할 증가율. 20을 입력하면 계산에서는 0.2 배율로 사용한다.
    [SerializeField] private float attackDamageBonusPercent;

    [Header("Presentation")]
    // 이 능력에 대응되는 애니메이션 클립. 실제 재생은 Animator Trigger를 권장하고, 이 필드는 에셋에서 어떤 모션을 쓰는지 확인하는 용도다.
    [SerializeField] private AnimationClip animationClip;

    // Setup Tool에서 Animator State를 만들 때 사용할 상태 이름. 비워두면 animationClip 이름을 쓸 수 있다.
    // 예: "Great Sword Slide Attack"
    [SerializeField] private string animationStateName;

    // Animator Trigger 파라미터로 재생할 때 사용하는 트리거 이름.
    [SerializeField] private string animationTrigger;

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

    [SerializeField] private float hitboxDamage;

    [SerializeField] private float hitboxDelay;

    [SerializeField] private float hitboxLifetime = 0.3f;

    public string AbilityId => string.IsNullOrWhiteSpace(abilityId) ? name : abilityId;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    public string Description => description;
    public Sprite Icon => icon;
    public AbilityType AbilityType => abilityType;
    public bool IsActive => abilityType == AbilityType.Active;
    public float StaminaCost => staminaCost;
    public float CooldownSeconds => cooldownSeconds;
    public bool IncludeInRewardPool => includeInRewardPool;
    public float MaxHealthBonus => maxHealthBonus;
    public float MaxStaminaBonus => maxStaminaBonus;
    public float DefenseRateBonus => defenseRateBonus;
    public float AttackDamageBonusRate => attackDamageBonusPercent * 0.01f;
    public AnimationClip AnimationClip => animationClip;
    public string AnimationStateName => animationStateName;
    public string AnimationTrigger => animationTrigger;
    public float HealthRestoreAmount => healthRestoreAmount;
    public float StaminaRestoreAmount => staminaRestoreAmount;
    public PlayerAbilitySpecialEffect SpecialEffect => specialEffect;
    public GameObject EffectPrefab => effectPrefab;
    public Vector3 EffectLocalOffset => effectLocalOffset;
    public bool ParentEffectToPlayer => parentEffectToPlayer;
    public GameObject HitboxPrefab => hitboxPrefab;
    public Vector3 HitboxLocalOffset => hitboxLocalOffset;
    public float HitboxDamage => hitboxDamage;
    public float HitboxDelay => hitboxDelay;
    public float HitboxLifetime => hitboxLifetime;

    public bool CanAppearAtStage(int bossStage)
    {
        return bossStage >= minBossStage && bossStage <= maxBossStage;
    }
}
