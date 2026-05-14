using Fusion;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerStats : NetworkBehaviour
{
    [Header("Vitals")]
    // 플레이어 최대 체력. Spawned에서 CurrentHealth 초기값으로 사용된다.
    [SerializeField] private float maxHealth = 10000f;
    // 플레이어 최대 기력. 공격, 구르기, 달리기, 스킬 사용 가능 여부의 기준이 된다.
    [SerializeField] private float maxStamina = 1000f;
    // 기력 회복 지연 시간이 끝난 뒤 초당 회복되는 기력량.
    [SerializeField] private float staminaRegenPerSecond = 100f;
    // 기력을 사용한 직후 바로 회복되지 않도록 막는 시간.
    [SerializeField] private float staminaRegenDelay = 0.8f;
    // 연속 충돌로 한 공격이 여러 번 들어가는 것을 막기 위한 짧은 피격 무적 시간.
    [SerializeField] private float hitInvincibleDuration = 0.25f;

    [Header("Combat")]
    // 받는 데미지를 줄이는 비율. 0.5면 최종 피해가 50%만 적용된다.
    [SerializeField, Range(0f, 1f)] private float defenseRate = 0.5f;
    // 평타 1타 데미지. 기존 AttackPower 접근자는 우선 1타 데미지를 대표값으로 반환한다.
    [SerializeField] private float firstAttackDamage = 7000f;
    // 평타 2타 데미지. 콤보 시스템이 연결되면 두 번째 공격 판정에 사용한다.
    [SerializeField] private float secondAttackDamage = 9000f;
    // 평타 3타 데미지. 콤보 시스템이 연결되면 세 번째 공격 판정에 사용한다.
    [SerializeField] private float thirdAttackDamage = 11000f;
    // 현재 기본 공격은 기획표에 별도 코스트가 없어 0으로 둔다.
    [SerializeField] private float attackStaminaCost = 0f;
    // Shift 짧은 탭 구르기 1회에 소모되는 기력.
    [SerializeField] private float rollStaminaCost = 200f;
    // Shift 길게 누르기 달리기 중 초당 소모되는 기력.
    [SerializeField] private float runStaminaPerSecond = 40f;

    [Header("Skill Costs")]
    // 회전베기 스킬 기력 코스트.
    [SerializeField] private float spinSlashStaminaCost = 400f;
    // 점프베기 스킬 기력 코스트.
    [SerializeField] private float jumpSlashStaminaCost = 400f;
    // 파워업 스킬 기력 코스트.
    [SerializeField] private float powerUpStaminaCost = 500f;
    // 슬라이드 베기 스킬 기력 코스트.
    [SerializeField] private float slideSlashStaminaCost = 400f;

    // 현재 체력. 네트워크 상태로 동기화되어 모든 클라이언트가 같은 값을 본다.
    [Networked] public float CurrentHealth { get; private set; }
    // 현재 기력. 달리기/구르기/스킬 코스트 적용 결과가 네트워크로 공유된다.
    [Networked] public float CurrentStamina { get; private set; }
    // 사망 여부. 컨트롤러는 이 값을 보고 입력과 이동을 막는다.
    [Networked] public bool IsDead { get; private set; }
    // 기력 사용 직후 회복 시작까지의 대기 시간을 네트워크 틱 기준으로 관리한다.
    [Networked] private TickTimer StaminaRegenDelayTimer { get; set; }
    // 보스 공격 히트박스가 한 번 스쳐도 여러 Collider에서 중복 피해가 들어가는 것을 줄인다.
    [Networked] private TickTimer HitInvincibleTimer { get; set; }

    // 외부 스크립트가 수치를 읽을 때 인스펙터 값을 직접 수정하지 못하도록 읽기 전용으로 공개한다.
    public float MaxHealth => maxHealth;
    public float MaxStamina => maxStamina;
    public float DefenseRate => defenseRate;
    public float AttackPower => firstAttackDamage;
    public float FirstAttackDamage => firstAttackDamage;
    public float SecondAttackDamage => secondAttackDamage;
    public float ThirdAttackDamage => thirdAttackDamage;
    public float AttackStaminaCost => attackStaminaCost;
    public float RollStaminaCost => rollStaminaCost;
    public float RunStaminaPerSecond => runStaminaPerSecond;
    public float SpinSlashStaminaCost => spinSlashStaminaCost;
    public float JumpSlashStaminaCost => jumpSlashStaminaCost;
    public float PowerUpStaminaCost => powerUpStaminaCost;
    public float SlideSlashStaminaCost => slideSlashStaminaCost;

    public override void Spawned()
    {
        // 체력/기력 같은 판정 값은 상태 권한을 가진 쪽에서만 초기화한다.
        // 프록시 클라이언트는 네트워크로 동기화된 값을 받기만 한다.
        if (!HasStateAuthority)
        {
            return;
        }

        CurrentHealth = maxHealth;
        CurrentStamina = maxStamina;
        IsDead = false;
    }

    public override void FixedUpdateNetwork()
    {
        // 상태 권한이 없거나 죽은 상태라면 기력 회복 계산을 하지 않는다.
        if (!HasStateAuthority || IsDead)
        {
            return;
        }

        // 최근에 기력을 썼다면 회복 지연 타이머가 끝날 때까지 기다린다.
        if (!StaminaRegenDelayTimer.ExpiredOrNotRunning(Runner))
        {
            return;
        }

        // Fusion 네트워크 틱 시간 기준으로 기력을 회복해 클라이언트 간 결과를 맞춘다.
        CurrentStamina = Mathf.Min(maxStamina, CurrentStamina + staminaRegenPerSecond * Runner.DeltaTime);
    }

    public bool HasStamina(float amount)
    {
        // 액션을 시작할 수 있는지 확인할 때 사용하는 단순 조회 함수.
        return CurrentStamina >= amount;
    }

    public bool TryUseStamina(float amount)
    {
        // 0 이하 코스트는 항상 성공 처리한다. 기본 공격처럼 무료 액션에 사용된다.
        if (amount <= 0f)
        {
            return true;
        }

        // 입력 권한 클라이언트에서도 즉시 액션 가능 여부를 예측할 수 있도록 현재 동기화 값을 기준으로 확인한다.
        // 실제 차감은 상태 권한이 있는 쪽에서만 수행된다.
        if (!HasStateAuthority)
        {
            return CurrentStamina >= amount;
        }

        // 죽었거나 기력이 부족하면 액션을 시작하지 못한다.
        if (IsDead || CurrentStamina < amount)
        {
            return false;
        }

        // 기력을 차감하고 회복 지연 타이머를 새로 시작한다.
        CurrentStamina = Mathf.Max(0f, CurrentStamina - amount);
        StaminaRegenDelayTimer = TickTimer.CreateFromSeconds(Runner, staminaRegenDelay);
        return true;
    }

    public void TakeDamage(float damage)
    {
        // 보스 히트박스나 다른 공격 판정에서 호출할 플레이어 피격 진입점.
        if (damage <= 0f)
        {
            return;
        }

        // 상태 권한이 있으면 바로 처리하고, 권한이 없는 클라이언트에서 호출되면 RPC로 상태 권한에 요청한다.
        if (HasStateAuthority)
        {
            ApplyDamage(damage);
        }
        else
        {
            RPC_RequestTakeDamage(damage);
        }
    }

    public void ApplyBossDamage(float damage)
    {
        // 보스 쪽에서 의미가 더 명확한 이름으로 호출할 수 있도록 남겨둔 래퍼 함수.
        TakeDamage(damage);
    }

    public void Heal(float amount)
    {
        // 회복은 상태 권한에서만 처리한다. 죽은 상태에서는 임의로 체력을 되살리지 않는다.
        if (amount <= 0f || !HasStateAuthority || IsDead)
        {
            return;
        }

        CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + amount);
    }

    public void RestoreStamina(float amount)
    {
        // 포션/버프 등 즉시 기력 회복용 함수. 현재는 외부 연결을 위한 기본 입구만 준비한다.
        if (amount <= 0f || !HasStateAuthority || IsDead)
        {
            return;
        }

        CurrentStamina = Mathf.Min(maxStamina, CurrentStamina + amount);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestTakeDamage(float damage)
    {
        // 클라이언트에서 감지한 피격 요청을 상태 권한이 최종 판정한다.
        ApplyDamage(damage);
    }

    private void ApplyDamage(float damage)
    {
        // 이미 죽었거나 짧은 피격 무적 중이면 데미지를 무시한다.
        if (IsDead || !HitInvincibleTimer.ExpiredOrNotRunning(Runner))
        {
            return;
        }

        // 방어율을 적용한 최종 피해만 체력에서 차감한다.
        float finalDamage = damage * (1f - Mathf.Clamp01(defenseRate));
        CurrentHealth = Mathf.Max(0f, CurrentHealth - finalDamage);
        HitInvincibleTimer = TickTimer.CreateFromSeconds(Runner, hitInvincibleDuration);

        Debug.Log($"[Player Damaged] damage: {finalDamage}, hp: {CurrentHealth}/{maxHealth}");

        if (CurrentHealth <= 0f)
        {
            // 사망 상태는 NetworkPlayerController에서 입력/이동 제한에 사용된다.
            IsDead = true;
            Debug.Log("[Player Dead]");
        }
    }
}
