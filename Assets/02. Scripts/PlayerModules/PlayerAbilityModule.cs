using UnityEngine;

// 모든 플레이어 능력 모듈의 기본 클래스.
// 새 능력을 만들 때는 이 클래스를 직접 수정하기보다,
// HealAbilityModule처럼 상속받은 새 ScriptableObject 클래스를 만들어 필요한 함수만 override하면 된다.
public abstract class PlayerAbilityModule : ScriptableObject
{
    [Header("Reward")]
    // 같은 능력인지 비교할 때 쓰는 고유 ID.
    // 비워 두면 ScriptableObject asset 이름을 ID처럼 사용한다.
    [SerializeField] private string abilityId;

    // UI에 보여줄 이름. 비워 두면 asset 이름을 표시한다.
    [SerializeField] private string displayName;

    // UI에 보여줄 설명 문구.
    [TextArea]
    [SerializeField] private string description;

    // 패시브인지 액티브인지 결정한다.
    // Active로 설정된 모듈만 키 슬롯에 등록된다.
    [SerializeField] private AbilityType abilityType = AbilityType.Passive;

    // 이 능력이 보상 후보로 등장할 수 있는 최소/최대 보스 단계.
    // 예: 3~5로 설정하면 3, 4, 5단계 보스를 잡았을 때만 랜덤 후보에 들어간다.
    [SerializeField, Range(1, 8)] private int minBossStage = 1;
    [SerializeField, Range(1, 8)] private int maxBossStage = 8;

    [Header("Active Settings")]
    // 액티브 능력을 사용할 때 소모할 스태미나.
    // 패시브 능력에서는 기본적으로 사용하지 않는다.
    [SerializeField] private float staminaCost;

    // 액티브 능력을 사용한 뒤 다시 사용할 수 있을 때까지의 시간.
    [SerializeField] private float cooldownSeconds;

    public string AbilityId => string.IsNullOrWhiteSpace(abilityId) ? name : abilityId;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    public string Description => description;
    public AbilityType AbilityType => abilityType;
    public bool IsActive => abilityType == AbilityType.Active;
    public float StaminaCost => staminaCost;
    public float CooldownSeconds => cooldownSeconds;

    // 보스 처치 보상 3개를 뽑을 때 현재 보스 단계에 맞는지 확인한다.
    public bool CanAppearAtStage(int bossStage)
    {
        return bossStage >= minBossStage && bossStage <= maxBossStage;
    }

    // 능력을 선택해서 장착할 수 있는지 검사한다.
    // 특정 조건이 필요한 능력은 상속 클래스에서 override해서 추가 조건을 넣으면 된다.
    public virtual bool CanEquip(PlayerAbilityContext context)
    {
        return context.Owner != null;
    }

    // 능력을 선택해서 장착하는 순간 호출된다.
    // 패시브 효과, 스탯 증가, 초기 버프 적용은 보통 이 함수에 구현한다.
    public virtual void OnEquipped(PlayerAbilityContext context)
    {
    }

    // 능력을 제거하거나 교체하는 시스템을 만들 때 사용할 해제 함수.
    // 현재 기본 구조에서는 아직 호출하지 않지만, 나중에 모듈 제거 기능을 붙일 수 있게 열어 둔다.
    public virtual void OnUnequipped(PlayerAbilityContext context)
    {
    }

    // 액티브 능력을 지금 사용할 수 있는지 검사한다.
    // 기본 조건은 "액티브 능력이고, 플레이어가 존재하며, 죽지 않았을 것"이다.
    // 쿨다운과 스태미나 검사는 PlayerAbilityController에서 공통으로 처리한다.
    public virtual bool CanActivate(PlayerAbilityContext context)
    {
        return IsActive && context.Owner != null && (context.Stats == null || !context.Stats.IsDead);
    }

    // 액티브 능력의 실제 효과가 실행되는 함수.
    // 회복, 투사체 발사, 범위 공격, 버프 시작 같은 내용을 상속 클래스에서 구현한다.
    public virtual void Activate(PlayerAbilityContext context)
    {
    }
}
