using Fusion;
using UnityEngine;

public enum AbilityType
{
    // 획득 즉시 효과를 적용하고 액티브 슬롯에는 들어가지 않는 능력.
    Passive,

    // 획득 후 액티브 슬롯에 등록되어 지정된 키로 사용하는 능력.
    Active
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

    // 패시브인지 액티브인지 결정한다. 액티브만 키 슬롯에 등록된다.
    [SerializeField] private AbilityType abilityType = AbilityType.Passive;

    // 이 능력이 보상 후보로 등장할 수 있는 보스 단계 범위.
    [SerializeField, Range(1, 8)] private int minBossStage = 1;
    [SerializeField, Range(1, 8)] private int maxBossStage = 8;

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

    [Header("Presentation")]
    // 이 능력에 대응되는 애니메이션 클립. 실제 재생은 Animator Trigger를 권장하고, 이 필드는 에셋에서 어떤 모션을 쓰는지 확인하는 용도다.
    [SerializeField] private AnimationClip animationClip;

    // Animator 상태 이름으로 직접 재생할 때 사용하는 상태명. animationClip이 비어있거나 상태 이름을 따로 쓰고 싶을 때 사용한다.
    // 예: "Great Sword Slide Attack"
    [SerializeField] private string animationStateName;

    // Animator Trigger 파라미터로 재생할 때 사용하는 트리거 이름.
    [SerializeField] private string animationTrigger;

    // 상태 이름으로 재생할 때 CrossFade에 사용할 전환 시간.
    [SerializeField] private float crossFadeDuration = 0.08f;

    // true면 상태 이름으로 직접 전환하고, false면 animationTrigger를 Animator에 전달한다.
    [SerializeField] private bool useStateName = true;

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

    // 보상을 선택했을 때 이 능력을 장착할 수 있는지 검사한다.
    public virtual bool CanEquip(PlayerAbilityContext context)
    {
        return context.Owner != null;
    }

    // 능력을 획득/장착하는 순간 호출된다. 패시브는 이 시점에 효과를 적용한다.
    public virtual void OnEquipped(PlayerAbilityContext context)
    {
        if (!IsActive)
        {
            PlayPresentation(context);
            ApplyEffect(context);
        }
    }

    public virtual void OnUnequipped(PlayerAbilityContext context)
    {
    }

    // 액티브 능력을 지금 사용할 수 있는지 검사한다.
    // 쿨타임과 스태미나 소모는 PlayerAbilityController에서 공통 처리한다.
    public virtual bool CanActivate(PlayerAbilityContext context)
    {
        return IsActive && context.Owner != null && (context.Stats == null || !context.Stats.IsDead);
    }

    // 액티브 능력을 실제로 사용한다.
    public virtual void Activate(PlayerAbilityContext context)
    {
        PlayPresentation(context);
        ApplyEffect(context);
    }

    // 에셋에 설정된 회복 수치를 실제 플레이어 스탯에 적용한다.
    private void ApplyEffect(PlayerAbilityContext context)
    {
        if (healthRestoreAmount > 0f)
        {
            context.Stats?.Heal(healthRestoreAmount);
        }

        if (staminaRestoreAmount > 0f)
        {
            context.Stats?.RestoreStamina(staminaRestoreAmount);
        }
    }

    // 애니메이션, 파티클, 히트박스처럼 눈에 보이는 연출/판정을 실행한다.
    private void PlayPresentation(PlayerAbilityContext context)
    {
        if (context.Owner == null)
        {
            return;
        }

        Animator animator = context.Owner.GetComponentInChildren<Animator>();
        if (animator != null)
        {
            string stateName = !string.IsNullOrWhiteSpace(animationStateName)
                ? animationStateName
                : animationClip != null
                    ? animationClip.name
                    : string.Empty;

            if (useStateName && !string.IsNullOrWhiteSpace(stateName))
            {
                animator.CrossFadeInFixedTime(stateName, Mathf.Max(0f, crossFadeDuration));
            }
            else if (!string.IsNullOrWhiteSpace(animationTrigger))
            {
                animator.ResetTrigger(animationTrigger);
                animator.SetTrigger(animationTrigger);
            }
        }

        SpawnPrefab(context, effectPrefab, effectLocalOffset, parentEffectToPlayer);
        SpawnPrefab(context, hitboxPrefab, hitboxLocalOffset, false);
    }

    // 네트워크 프리팹이면 Fusion Runner로 생성하고, 일반 프리팹이면 Unity Instantiate로 생성한다.
    private static void SpawnPrefab(PlayerAbilityContext context, GameObject prefab, Vector3 localOffset, bool parentToPlayer)
    {
        if (prefab == null || context.Transform == null)
        {
            return;
        }

        Vector3 position = context.Transform.TransformPoint(localOffset);
        Quaternion rotation = context.Transform.rotation;

        NetworkRunner runner = context.Runner;
        NetworkObject networkPrefab = prefab.GetComponent<NetworkObject>();
        if (runner != null && networkPrefab != null)
        {
            runner.Spawn(networkPrefab, position, rotation, null);
            return;
        }

        GameObject instance = Instantiate(prefab, position, rotation);
        if (parentToPlayer)
        {
            instance.transform.SetParent(context.Transform, true);
        }
    }
}
