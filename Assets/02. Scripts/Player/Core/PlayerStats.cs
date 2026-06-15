using Fusion;
using System;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerStats : NetworkBehaviour
{
    public bool IsSpawnedReady { get; private set; }

    public readonly struct SessionSnapshot
    {
        public readonly float CurrentHealth;
        public readonly float CurrentStamina;
        public readonly bool IsDead;
        public readonly float BonusMaxHealth;
        public readonly float BonusMaxStamina;
        public readonly float BonusDefenseRate;
        public readonly float BonusAttackDamageRate;
        public readonly int DeathCount;

        public SessionSnapshot(
            float currentHealth,
            float currentStamina,
            bool isDead,
            float bonusMaxHealth,
            float bonusMaxStamina,
            float bonusDefenseRate,
            float bonusAttackDamageRate,
            int deathCount)
        {
            CurrentHealth = currentHealth;
            CurrentStamina = currentStamina;
            IsDead = isDead;
            BonusMaxHealth = bonusMaxHealth;
            BonusMaxStamina = bonusMaxStamina;
            BonusDefenseRate = bonusDefenseRate;
            BonusAttackDamageRate = bonusAttackDamageRate;
            DeathCount = deathCount;
        }
    }

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

    [Header("Revive")]
    [SerializeField] private float baseReviveGaugePerSegment = 100f;
    [SerializeField] private float reviveGaugeIncreasePerDeathAfterMaxSegments = 100f;
    [SerializeField] private int maxReviveSegments = 3;
    [SerializeField] private float reviveProgressDecayDelay = 2f;
    [SerializeField] private float reviveProgressDecayPerSecond = 30f;

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
    // 구르기/패링 애니메이션 이벤트가 켜는 수동 무적 상태.
    [Networked] public bool IsAnimationInvincible { get; private set; }
    // 기력 사용 직후 회복 시작까지의 대기 시간을 네트워크 틱 기준으로 관리한다.
    [Networked] private TickTimer StaminaRegenDelayTimer { get; set; }
    // 보스 공격 히트박스가 한 번 스쳐도 여러 Collider에서 중복 피해가 들어가는 것을 줄인다.
    [Networked] private TickTimer HitInvincibleTimer { get; set; }
    // 패시브 능력으로 얻은 스탯 보너스. 기본 스탯과 분리해 두면 어떤 값이 원래 수치이고 어떤 값이 보상 수치인지 추적하기 쉽다.
    [Networked] private float BonusMaxHealth { get; set; }
    [Networked] private float BonusMaxStamina { get; set; }
    [Networked] private float BonusDefenseRate { get; set; }
    [Networked] private float BonusAttackDamageRate { get; set; }
    [Networked] public int DeathCount { get; private set; }
    [Networked] public int ReviveSegmentCount { get; private set; }
    [Networked] public float ReviveGaugePerSegment { get; private set; }
    [Networked] public float ReviveProgress { get; private set; }
    [Networked] private TickTimer ReviveDecayDelayTimer { get; set; }

    // 외부 스크립트가 수치를 읽을 때 인스펙터 값을 직접 수정하지 못하도록 읽기 전용으로 공개한다.
    public float MaxHealth => Mathf.Max(1f, maxHealth + BonusMaxHealth);
    public float MaxStamina => Mathf.Max(0f, maxStamina + BonusMaxStamina);
    public float DefenseRate => Mathf.Clamp01(defenseRate + BonusDefenseRate);
    public float AttackPower => FirstAttackDamage;
    public float FirstAttackDamage => ApplyAttackBonus(firstAttackDamage);
    public float SecondAttackDamage => ApplyAttackBonus(secondAttackDamage);
    public float ThirdAttackDamage => ApplyAttackBonus(thirdAttackDamage);
    public float AttackStaminaCost => attackStaminaCost;
    public float RollStaminaCost => rollStaminaCost;
    public float RunStaminaPerSecond => runStaminaPerSecond;
    public float SpinSlashStaminaCost => spinSlashStaminaCost;
    public float JumpSlashStaminaCost => jumpSlashStaminaCost;
    public float PowerUpStaminaCost => powerUpStaminaCost;
    public float SlideSlashStaminaCost => slideSlashStaminaCost;
    public float ReviveRequiredGauge => Mathf.Max(1f, ReviveSegmentCount * ReviveGaugePerSegment);
    public float ReviveNormalizedProgress => IsDead ? Mathf.Clamp01(1f - ReviveProgress / ReviveRequiredGauge) : 0f;
    public float ReviveRemainingNormalized => IsDead ? Mathf.Clamp01(ReviveProgress / ReviveRequiredGauge) : 0f;
    public event Action<PlayerStats> ReviveStateChanged;

    private ChangeDetector _changeDetector;
    private PlayerStatusController _statusController;

    public SessionSnapshot CreateSessionSnapshot()
    {
        return new SessionSnapshot(
            CurrentHealth,
            CurrentStamina,
            IsDead,
            BonusMaxHealth,
            BonusMaxStamina,
            BonusDefenseRate,
            BonusAttackDamageRate,
            DeathCount);
    }

    public PlayerHUDData GetHUDData()
    {
        // UI가 PlayerStats 내부 계산식을 직접 알 필요 없도록 표시용 값만 묶어서 반환한다.
        return new PlayerHUDData(
            CurrentHealth,
            MaxHealth,
            CurrentStamina,
            MaxStamina,
            IsDead);
    }

    public void RestoreSessionSnapshot(SessionSnapshot snapshot)
    {
        if (!HasStateAuthority)
        {
            return;
        }

        BonusMaxHealth = snapshot.BonusMaxHealth;
        BonusMaxStamina = snapshot.BonusMaxStamina;
        BonusDefenseRate = snapshot.BonusDefenseRate;
        BonusAttackDamageRate = snapshot.BonusAttackDamageRate;
        DeathCount = Mathf.Max(0, snapshot.DeathCount);

        CurrentHealth = Mathf.Clamp(snapshot.CurrentHealth, 0f, MaxHealth);
        CurrentStamina = Mathf.Clamp(snapshot.CurrentStamina, 0f, MaxStamina);
        IsDead = snapshot.IsDead || CurrentHealth <= 0f;
        IsAnimationInvincible = false;
        StaminaRegenDelayTimer = default;
        HitInvincibleTimer = default;
        if (IsDead)
        {
            BeginReviveState(false);
        }
        else
        {
            ClearReviveProgress();
        }
    }

    public override void Spawned()
    {
        IsSpawnedReady = true;

        _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);
        _statusController = GetComponent<PlayerStatusController>();

        // 체력/기력 같은 판정 값은 상태 권한을 가진 쪽에서만 초기화한다.
        // 프록시 클라이언트는 네트워크로 동기화된 값을 받기만 한다.
        if (!HasStateAuthority)
        {
            return;
        }

        BonusMaxHealth = 0f;
        BonusMaxStamina = 0f;
        BonusDefenseRate = 0f;
        BonusAttackDamageRate = 0f;
        DeathCount = 0;
        CurrentHealth = MaxHealth;
        CurrentStamina = MaxStamina;
        IsDead = false;
        IsAnimationInvincible = false;
        ClearReviveProgress();
    }

    public override void Render()
    {
        if (_changeDetector == null)
        {
            return;
        }

        foreach (string change in _changeDetector.DetectChanges(this))
        {
            if (change == nameof(IsDead) ||
                change == nameof(DeathCount) ||
                change == nameof(ReviveSegmentCount) ||
                change == nameof(ReviveGaugePerSegment) ||
                change == nameof(ReviveProgress))
            {
                ReviveStateChanged?.Invoke(this);
                break;
            }
        }
    }

    public override void FixedUpdateNetwork()
    {
        // 상태 권한이 없거나 죽은 상태라면 기력 회복 계산을 하지 않는다.
        if (!HasStateAuthority)
        {
            return;
        }

        if (IsDead)
        {
            UpdateReviveDecay();
            return;
        }

        // 최근에 기력을 썼다면 회복 지연 타이머가 끝날 때까지 기다린다.
        if (!StaminaRegenDelayTimer.ExpiredOrNotRunning(Runner))
        {
            return;
        }

        // Fusion 네트워크 틱 시간 기준으로 기력을 회복해 클라이언트 간 결과를 맞춘다.
        CurrentStamina = Mathf.Min(MaxStamina, CurrentStamina + staminaRegenPerSecond * Runner.DeltaTime);
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

    public void SetAnimationInvincible(bool isInvincible)
    {
        // 애니메이션 이벤트는 입력권한 클라이언트에서 먼저 들어올 수 있으므로
        // 실제 판정 권한을 가진 StateAuthority로 전달한다.
        if (HasStateAuthority)
        {
            ApplyAnimationInvincible(isInvincible);
        }
        else
        {
            RPC_SetAnimationInvincible(isInvincible);
        }
    }

    public void Heal(float amount)
    {
        // 회복은 상태 권한에서만 처리한다. 죽은 상태에서는 임의로 체력을 되살리지 않는다.
        if (amount <= 0f || !HasStateAuthority || IsDead)
        {
            return;
        }

        CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + amount);
    }

    // ==========================================
    // 스킬 해금과 무관하게 작동하는 외부 기믹용 강제 힐 함수
    // ==========================================
    public void ForceHeal(float amount)
    {
        // 권한이 없거나, 이미 죽었거나, 회복량이 0 이하면 무시
        if (amount <= 0f || !HasStateAuthority || IsDead) return;

        // 최대 체력을 넘지 않는 선에서 체력 증가
        CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + amount);
    }

    public void RestoreStamina(float amount)
    {
        // 포션/버프 등 즉시 기력 회복용 함수. 현재는 외부 연결을 위한 기본 입구만 준비한다.
        if (amount <= 0f || !HasStateAuthority || IsDead)
        {
            return;
        }

        CurrentStamina = Mathf.Min(MaxStamina, CurrentStamina + amount);
    }

    public void RegisterReviveHit(NetworkObject helper = null, float revivePower = 0f)
    {
        if (HasStateAuthority)
        {
            ApplyReviveHit(helper, revivePower);
            return;
        }

        RPC_RegisterReviveHit(helper, revivePower);
    }

    // 패시브 능력 모듈에 들어있는 스탯 보너스를 누적한다.
    // 실제 네트워크 스탯 값은 StateAuthority에서만 바꿔야 모든 클라이언트가 같은 결과를 받는다.
    public void ApplyPassiveStatBonus(PlayerAbilityModule module)
    {
        if (module == null || module.IsActive || !HasStateAuthority)
        {
            return;
        }

        BonusMaxHealth += module.MaxHealthBonus;
        BonusMaxStamina += module.MaxStaminaBonus;
        BonusDefenseRate += module.DefenseRateBonus;
        BonusAttackDamageRate += module.AttackDamageBonusRate;

        // 최대치가 늘어나는 보상은 획득 즉시 체감되도록 현재 값도 함께 올린다.
        if (module.MaxHealthBonus > 0f && !IsDead)
        {
            CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + module.MaxHealthBonus);
        }

        if (module.MaxStaminaBonus > 0f && !IsDead)
        {
            CurrentStamina = Mathf.Min(MaxStamina, CurrentStamina + module.MaxStaminaBonus);
        }
    }

    // 장비 해제나 테스트 리셋처럼 패시브 보너스를 되돌릴 일이 생겼을 때 사용할 수 있는 함수다.
    public void RemovePassiveStatBonus(PlayerAbilityModule module)
    {
        if (module == null || module.IsActive || !HasStateAuthority)
        {
            return;
        }

        BonusMaxHealth -= module.MaxHealthBonus;
        BonusMaxStamina -= module.MaxStaminaBonus;
        BonusDefenseRate -= module.DefenseRateBonus;
        BonusAttackDamageRate -= module.AttackDamageBonusRate;

        CurrentHealth = Mathf.Min(CurrentHealth, MaxHealth);
        CurrentStamina = Mathf.Min(CurrentStamina, MaxStamina);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestTakeDamage(float damage)
    {
        // 클라이언트에서 감지한 피격 요청을 상태 권한이 최종 판정한다.
        ApplyDamage(damage);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_SetAnimationInvincible(bool isInvincible)
    {
        ApplyAnimationInvincible(isInvincible);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RegisterReviveHit(NetworkObject helper, float revivePower)
    {
        ApplyReviveHit(helper, revivePower);
    }

    private void ApplyDamage(float damage)
    {
        NetworkPlayerController controller = GetComponent<NetworkPlayerController>();
        if (!IsDead && IsAnimationInvincible && controller != null && controller.IsParryGuardActive)
        {
            Debug.Log("[Player Damage Blocked] parry guard");
            controller.NotifyParryGuardBlocked();
            return;
        }

        // 이미 죽었거나 짧은 피격 무적 중이면 데미지를 무시한다.
        if (IsDead || IsAnimationInvincible || !HitInvincibleTimer.ExpiredOrNotRunning(Runner))
        {
            Debug.Log($"[Player Damage Ignored] dead:{IsDead}, animationInvincible:{IsAnimationInvincible}, hitInvincible:{!HitInvincibleTimer.ExpiredOrNotRunning(Runner)}");
            return;
        }

        // 방어율을 적용한 최종 피해만 체력에서 차감한다.
        float finalDamage = damage * (1f - DefenseRate);
        if (_statusController != null)
        {
            finalDamage *= _statusController.GetIncomingDamageMultiplier();
        }

        CurrentHealth = Mathf.Max(0f, CurrentHealth - finalDamage);
        HitInvincibleTimer = TickTimer.CreateFromSeconds(Runner, hitInvincibleDuration);
        bool becameDead = CurrentHealth <= 0f;

        Debug.Log($"[Player Damaged] damage: {finalDamage}, hp: {CurrentHealth}/{MaxHealth}");

        if (becameDead)
        {
            // 사망 상태는 NetworkPlayerController에서 입력/이동 제한에 사용된다.
            IsDead = true;
            IsAnimationInvincible = false;
            BeginReviveState();
            Debug.Log("[Player Dead]");
        }

        controller?.NotifyDamageReaction(becameDead);
    }

    private void ApplyAnimationInvincible(bool isInvincible)
    {
        IsAnimationInvincible = !IsDead && isInvincible;
    }

    private void BeginReviveState(bool incrementDeath = true)
    {
        if (incrementDeath)
        {
            DeathCount++;
        }
        else if (DeathCount <= 0)
        {
            DeathCount = 1;
        }

        ReviveSegmentCount = Mathf.Clamp(DeathCount, 1, Mathf.Max(1, maxReviveSegments));
        int extraDeaths = Mathf.Max(0, DeathCount - maxReviveSegments);
        ReviveGaugePerSegment = Mathf.Max(1f, baseReviveGaugePerSegment + reviveGaugeIncreasePerDeathAfterMaxSegments * extraDeaths);
        ReviveProgress = ReviveRequiredGauge;
        ReviveDecayDelayTimer = default;
        ReviveStateChanged?.Invoke(this);
    }

    private void ClearReviveProgress()
    {
        ReviveSegmentCount = 0;
        ReviveGaugePerSegment = Mathf.Max(1f, baseReviveGaugePerSegment);
        ReviveProgress = 0f;
        ReviveDecayDelayTimer = default;
        ReviveStateChanged?.Invoke(this);
    }

    private void ApplyReviveHit(NetworkObject helper, float revivePower)
    {
        if (!IsDead)
        {
            return;
        }

        if (helper != null && Object != null && helper == Object)
        {
            return;
        }

        if (revivePower <= 0f)
        {
            return;
        }

        ReviveProgress = Mathf.Max(0f, ReviveProgress - revivePower);
        ReviveDecayDelayTimer = TickTimer.CreateFromSeconds(Runner, reviveProgressDecayDelay);

        if (ReviveProgress <= 0f)
        {
            ReviveFully();
            return;
        }

        ReviveStateChanged?.Invoke(this);
    }

    private void UpdateReviveDecay()
    {
        if (ReviveProgress >= ReviveRequiredGauge || !ReviveDecayDelayTimer.ExpiredOrNotRunning(Runner))
        {
            return;
        }

        ReviveProgress = Mathf.Min(ReviveRequiredGauge, ReviveProgress + reviveProgressDecayPerSecond * Runner.DeltaTime);
        ReviveStateChanged?.Invoke(this);
    }

    private void ReviveFully()
    {
        IsDead = false;
        IsAnimationInvincible = false;
        CurrentHealth = MaxHealth;
        CurrentStamina = MaxStamina;
        StaminaRegenDelayTimer = default;
        HitInvincibleTimer = default;
        ClearReviveProgress();
        GetComponent<NetworkPlayerController>()?.NotifyRevived();
        Debug.Log("[Player Revived]");
    }

    private float ApplyAttackBonus(float baseDamage)
    {
        return Mathf.Max(0f, baseDamage * (1f + BonusAttackDamageRate));
    }
}
