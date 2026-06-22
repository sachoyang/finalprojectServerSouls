using System.Collections.Generic;
using System.Text;
using Fusion;
using UnityEngine;

public partial class NetworkPlayerController
{
    public void UnlockBasicAttackCombo()
    {
        _localBasicAttackComboUnlocked = true;

        if (Object == null)
        {
            return;
        }

        if (HasStateAuthority)
        {
            BasicAttackComboUnlocked = true;
            return;
        }

        if (Object.HasInputAuthority)
        {
            RPC_UnlockBasicAttackCombo();
        }
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_UnlockBasicAttackCombo()
    {
        BasicAttackComboUnlocked = true;
    }

    private bool CanQueueBasicAttackCombo(bool isActing)
    {
        // queue는 Animator가 콤보 입력 가능 창을 연 뒤에만 사용한다.
        // 여기 들어간 입력은 현재 공격이 끝나면 바로 다음 기본공격으로 실행된다.
        return isActing &&
               LastAction == ActionAttack &&
               CanStartBasicAttackFollowUp() &&
               !_queuedComboAttack &&
               IsComboInputWindowOpen();
    }

    private bool CanStartQueuedComboAttack(bool isActing)
    {
        // 실제 실행은 액션락이 풀린 뒤에만 허용한다.
        // 액션 중에 바로 StartBasicAttack을 호출하면 현재 공격 State를 덮어써서 모션/판정이 꼬일 수 있다.
        return !isActing &&
               _queuedComboAttack &&
               _queuedComboActionId != 0 &&
               LastAction == ActionAttack &&
               CanStartBasicAttackFollowUp();
    }

    private bool TryQueueBasicAttackCombo(bool isActing, int actionId)
    {
        // actionId가 0이면 입력 이벤트가 아니라 유지 입력에 가까우므로 선입력으로 저장하지 않는다.
        // 같은 클릭이 두 번 소비되는 것을 막기 위해 TryConsumeInputAction에서 받은 고유 id만 큐에 넣는다.
        if (actionId == 0 || !CanQueueBasicAttackCombo(isActing))
        {
            return false;
        }

        _queuedComboAttack = true;
        _queuedComboActionId = actionId;
        return true;
    }

    private bool CanStartBasicAttackFollowUp()
    {
        // 콤보가 해금되지 않은 상태에서도 기본 공격 자체는 다음 기본 공격으로 선입력될 수 있어야 한다.
        // 해금 전에는 StartBasicAttack에서 항상 slash2로 고정되고, 해금 후에만 slash3/slash4 단계 제한을 적용한다.
        return !IsBasicAttackComboUnlocked || BasicAttackComboIndex < BasicAttackComboLastIndex;
    }

    private void ClearComboRequests()
    {
        // 공격 흐름이 끊기면 이전 클릭이 다음 액션 뒤에 늦게 실행되지 않도록 queue를 비운다.
        _queuedComboAttack = false;
        _queuedComboActionId = 0;
    }

    private byte GetOpeningBasicAttackComboIndex()
    {
        // StateMachineBehaviour가 입력창을 연 동안 queue된 입력만 다음 콤보로 이어진다.
        // idle에서 새로 누른 공격은 항상 첫 기본공격으로 시작한다.
        return 0;
    }

    private byte GetNextBasicAttackComboIndex()
    {
        return (byte)Mathf.Min(BasicAttackComboIndex + 1, BasicAttackComboLastIndex);
    }

    private bool TrySpendBasicAttackStamina()
    {
        // PlayerStats가 없으면 테스트 오브젝트로 보고 공격을 허용한다.
        // 실제 플레이어에서는 PlayerStats가 스태미나 차감 성공 여부를 돌려준다.
        return _playerStats == null || _playerStats.TryUseActionStamina(_playerStats.AttackStaminaCost);
    }

    private bool TrySpendJumpStamina()
    {
        return _playerStats == null || _playerStats.TryUseActionStamina(_playerStats.JumpStaminaCost);
    }

    private bool TrySpendParryStamina()
    {
        return _playerStats == null || _playerStats.TryUseActionStamina(_playerStats.ParryStaminaCost);
    }

    private bool TryConsumeInputAction(int actionId)
    {
        // actionId는 입력 한 번을 구분하는 번호다.
        // 0은 유효한 새 입력이 아니므로 공격/점프/패링 실행에 쓰지 않는다.
        if (actionId == 0)
        {
            return false;
        }

        if (HasStateAuthority)
        {
            // 서버 권한에서는 Networked 값으로 마지막 소비 id를 저장해 재시뮬레이션 중복 실행을 막는다.
            if (LastConsumedActionId == actionId)
            {
                return false;
            }

            LastConsumedActionId = actionId;
            return true;
        }

        if (Object != null && Object.HasInputAuthority)
        {
            // 비호스트 입력권한 쪽에서도 로컬 중복 소비를 막아 같은 입력을 여러 번 처리하지 않게 한다.
            if (_lastLocalConsumedActionId == actionId)
            {
                return false;
            }

            _lastLocalConsumedActionId = actionId;
            return true;
        }

        return false;
    }

    public void BeginActionAnimation(PlayerActionLockType lockType)
    {
        // None은 락을 소유하지 않는 상태다.
        // 잘못 붙은 Behaviour 때문에 락이 켜진 뒤 풀리지 않는 상황을 막기 위해 무시한다.
        if (lockType == PlayerActionLockType.None)
        {
            return;
        }

        SetActionAnimationLocked(true, lockType);
        SetComboInputWindowOpen(false);
    }

    public void OpenComboInputWindow()
    {
        if (LastAction != ActionAttack || BasicAttackComboIndex >= BasicAttackComboLastIndex)
        {
            return;
        }

        SetComboInputWindowOpen(true);
    }

    public void EndActionAnimation(PlayerActionLockType lockType)
    {
        DelayStaminaRegenAfterAction(lockType);

        // 나가는 State가 현재 락을 소유한 타입일 때만 해제한다.
        // 예: 공격 중 피격되면 현재 타입은 Impact가 되므로, 늦게 호출된 Attack Exit는 락을 풀 수 없다.
        if (lockType == PlayerActionLockType.None || GetCurrentActionLockType() != lockType)
        {
            return;
        }

        SetActionAnimationLocked(false);
        SetComboInputWindowOpen(false);

        if (lockType == PlayerActionLockType.Roll)
        {
            RollDirection = Vector3.zero;
        }

        if (LastAction == ActionAttack)
        {
            if (BasicAttackComboIndex >= BasicAttackComboLastIndex)
            {
                ClearComboRequests();
            }

            return;
        }

        ClearComboRequests();
    }

    private void DelayStaminaRegenAfterAction(PlayerActionLockType lockType)
    {
        if (lockType == PlayerActionLockType.Parry && IsTransitioningToParryState())
        {
            return;
        }

        if (lockType == PlayerActionLockType.Attack ||
            lockType == PlayerActionLockType.Parry ||
            lockType == PlayerActionLockType.Jump ||
            lockType == PlayerActionLockType.Roll ||
            lockType == PlayerActionLockType.Skill)
        {
            _playerStats?.DelayStaminaRegen();
        }
    }

    private bool IsComboInputWindowOpen()
    {
        return ComboInputWindowOpen || _localComboInputWindowOpen;
    }

    private PlayerActionLockType GetCurrentActionLockType()
    {
        // StateMachineBehaviour가 현재 재생 중인 State에 맞춰 로컬 락을 갱신한다.
        // 전환 중에는 네트워크 값보다 로컬 Animator 상태가 더 최신일 수 있어 로컬 락을 우선 본다.
        byte lockType = _localActionAnimationLocked ? _localActionLockType : ActionLockType;
        return (PlayerActionLockType)lockType;
    }

    private void SetActionAnimationLocked(bool isLocked, PlayerActionLockType lockType = PlayerActionLockType.None)
    {
        // Animator State 진입/종료가 알려주는 현재 액션 락을 로컬과 네트워크 상태에 반영한다.
        // 게임 결과는 서버가 확정하지만, 각 클라이언트의 입력 차단은 현재 재생 중인 Animator 상태도 참고한다.
        // 이렇게 해야 애니메이션/입력 지연 때문에 공격, 패링, 스킬이 늦게 끼어드는 상황을 줄일 수 있다.
        _localActionAnimationLocked = isLocked;
        _localActionLockType = isLocked ? (byte)lockType : (byte)PlayerActionLockType.None;
        UpdateAnimatorRootMotionMode();

        if (Object != null && HasStateAuthority)
        {
            ActionAnimationLocked = isLocked;
            ActionLockType = _localActionLockType;
            UpdateAnimatorRootMotionMode();
        }
    }

    private void SetComboInputWindowOpen(bool isOpen)
    {
        _localComboInputWindowOpen = isOpen;

        if (Object != null && HasStateAuthority)
        {
            ComboInputWindowOpen = isOpen;
        }
    }

    private void StartBasicAttack(byte comboIndex, int actionId)
    {
        ClearComboRequests();
        // 비호스트 예측 틱에서는 콤보 요청만 정리하고, 실제 콤보 단계 확정은 서버를 기다린다.
        // 입력권한 클라이언트가 여기서 Animator를 직접 재생하면 서버 확정 Render와 겹쳐 두 번 공격처럼 보일 수 있다.
        if (!HasStateAuthority)
        {
            return;
        }

        // 콤보 해금 전에는 어떤 후속 입력이 들어와도 slash2만 반복한다.
        // 콤보 해금 후에는 queue가 넘긴 comboIndex를 slash2/slash3/slash4 단계로 사용한다.
        BasicAttackComboIndex = IsBasicAttackComboUnlocked
            ? (byte)Mathf.Clamp(comboIndex, 0, BasicAttackComboLastIndex)
            : (byte)0;

        StartAction(ActionAttack, actionId);
    }

    private void StartAction(byte actionType, int actionId = 0)
    {
        // Animator 트리거의 기준이 되는 액션 이벤트는 StateAuthority만 기록한다.
        if (!HasStateAuthority)
        {
            return;
        }

        // StateAuthority만 액션 이벤트를 확정한다.
        // ActionSequence가 증가하면 모든 클라이언트의 Render에서 같은 Animator 트리거가 한 번만 재생된다.
        // 동시에 액션 타입별 락을 걸어 다음 입력 틱에서 다른 액션이 끼어들지 못하게 한다.
        // 이 프로젝트는 현재 로컬 예측 애니메이션을 제거했으므로 ActionSequence가 유일한 액션 표현 이벤트다.
        LastAction = actionType;
        LastActionId = actionId;
        ActionSequence++;
        SetActionAnimationLocked(true, GetActionLockType(actionType));
        SetComboInputWindowOpen(false);

        if (actionType != ActionAttack)
        {
            // 공격이 아닌 액션은 기본공격 선입력을 이어받지 않는다.
            // 예를 들어 점프/패링/피격 직후에 이전 클릭이 남아서 공격으로 이어지는 것을 막는다.
            ClearComboRequests();
        }

        if (actionType == ActionAttack)
        {
            // 피격 판정은 서버 확정 시점에만 처리한다.
            // 클라이언트 Animator 재생 여부와 무관하게 같은 공격이 한 번만 데미지를 만든다.
            ApplyAttackDamage();
        }

    }

    public void NotifyDamageReaction(bool becameDead)
    {
        // PlayerStats가 데미지를 확정한 뒤 호출한다. 패링 중이면 Impact2, 사망이면 Death를 우선한다.
        // PlayerStats가 실제 피해 적용 후 호출한다.
        // 피격은 StartAction을 거치지 않으므로 여기서 즉시 Impact 타입 락을 걸어 패링/공격 입력이 끼어들지 못하게 한다.
        if (!HasStateAuthority)
        {
            return;
        }

        LastAction = becameDead ? ActionDeath : ActionImpact;
        LastActionId = 0;
        ActionSequence++;
        SetActionAnimationLocked(true, GetActionLockType(LastAction));
        SetComboInputWindowOpen(false);
        ClearComboRequests();
    }

    public void NotifyParryGuardBlocked()
    {
        if (!HasStateAuthority || !IsParryGuardActive)
        {
            return;
        }

        SetParryGuardActive(false);
        LastAction = ActionParryImpact;
        LastActionId = 0;
        ActionSequence++;
        SetActionAnimationLocked(true, PlayerActionLockType.Impact);
        SetComboInputWindowOpen(false);
        ClearComboRequests();
    }

    public void NotifyRevived()
    {
        _localActionAnimationLocked = false;
        _localActionLockType = (byte)PlayerActionLockType.None;
        _localComboInputWindowOpen = false;
        _localParryGuardActive = false;
        ClearComboRequests();
        _lastLocalConsumedActionId = 0;

        if (HasStateAuthority)
        {
            LastAction = ActionNone;
            LastActionId = 0;
            LastConsumedActionId = 0;
            BasicAttackComboIndex = 0;
            ActionAnimationLocked = false;
            ActionLockType = (byte)PlayerActionLockType.None;
            ComboInputWindowOpen = false;
            ParryGuardActive = false;
            ActionSequence++;
        }
    }

    public void IsInvincible()
    {
        // 애니메이션 이벤트에서 호출한다. 이 프레임부터 플레이어가 보스 데미지를 무시한다.
        _playerStats?.SetAnimationInvincible(true);
    }

    public void EndInvincible()
    {
        // 애니메이션 이벤트에서 호출한다. 이 프레임부터 다시 데미지를 받을 수 있다.
        _playerStats?.SetAnimationInvincible(false);
    }

    public void SetActionInvincible(bool isInvincible)
    {
        // StateMachineBehaviour에서 제어하는 순수 무적이다.
        // ParryGuard와 달리 피해를 막아도 Impact2 같은 반격 리액션은 발생시키지 않는다.
        _playerStats?.SetAnimationInvincible(isInvincible);
    }

    private bool IsParryActive()
    {
        return LastAction == ActionParry && (IsActionAnimationLocked || IsParryAnimatorStateActive());
    }

    public bool IsParryActionActive => IsParryActive();

    public bool IsParryGuardActive => ParryGuardActive || _localParryGuardActive;

    public void SetParryGuardActive(bool isActive)
    {
        _localParryGuardActive = isActive;
        _playerStats?.SetAnimationInvincible(isActive);

        if (Object != null && HasStateAuthority)
        {
            ParryGuardActive = isActive;
        }
    }

    public void EndParryGuardState()
    {
        // blocking1 -> blocking2 -> blocking3처럼 guard가 켜진 패링 State끼리 이어질 때,
        // 이전 State의 Exit가 다음 State의 guard를 꺼버리면 전환 중 공격이 뚫릴 수 있다.
        if (IsTransitioningToParryState())
        {
            return;
        }

        SetParryGuardActive(false);
    }

    private static PlayerActionLockType GetActionLockType(byte actionType)
    {
        // 네트워크로 동기화되는 byte 액션 값을 Animator StateBehaviour에서 사용하는 락 타입으로 변환한다.
        // Death는 별도 조작 복귀가 없는 상태라 None으로 두고, 피격류는 Impact 타입으로 묶는다.
        return actionType switch
        {
            ActionAttack => PlayerActionLockType.Attack,
            ActionParry => PlayerActionLockType.Parry,
            ActionRoll => PlayerActionLockType.Roll,
            ActionJump or ActionJumpForward => PlayerActionLockType.Jump,
            ActionImpact or ActionParryImpact => PlayerActionLockType.Impact,
            _ => PlayerActionLockType.None
        };
    }

    private void ApplyAttackDamage()
    {
        // 기본 공격 판정은 범위 안의 보스 히트박스 중 배율이 가장 높은 부위 하나만 적용한다.
        float damage = GetBasicAttackDamage();
        if (damage <= 0f)
        {
            return;
        }

        Vector3 hitCenter = transform.TransformPoint(AttackHitLocalCenter);
        int hitCount = Physics.OverlapSphereNonAlloc(
            hitCenter,
            attackHitRadius,
            _attackHits,
            attackTargetLayers,
            QueryTriggerInteraction.Collide);

        _bestBossHurtboxes.Clear(); // 🔥 이름 변경됨
        _reviveHitPlayers.Clear();
        
        for (int i = 0; i < hitCount; i++)
        {
            // 한 번의 OverlapSphere 결과 안에는 보스, 제단, 죽은 플레이어가 섞여 들어올 수 있다.
            // 대상 종류별로 서로 다른 처리 경로를 타기 때문에 위에서부터 우선순위를 나눠 검사한다.
            Collider hit = _attackHits[i];
            if (hit == null)
            {
                continue;
            }

            // 죽은 팀원을 공격하면 부활 게이지를 채운다.
            // 같은 플레이어의 여러 Collider가 맞아도 HashSet으로 한 번만 처리한다.
            PlayerStats hitPlayerStats = hit.GetComponentInParent<PlayerStats>();
            if (hitPlayerStats != null && hitPlayerStats != _playerStats && hitPlayerStats.IsDead)
            {
                if (_reviveHitPlayers.Add(hitPlayerStats))
                {
                    hitPlayerStats.RegisterReviveHit(Object, basicAttackRevivePower);
                }

                continue;
            }

            GimmickAltar altar = hit.GetComponentInParent<GimmickAltar>();
            if (altar != null)
            {
                // 제단은 보스 히트박스와 별도 대상이므로 즉시 데미지를 주고 다음 Collider로 넘어간다.
                altar.RPC_TakeDamage(damage);
                continue;
            }

            // 🔥 [수정 핵심] BossHitbox -> BossHurtbox 로 교체
            BossHurtbox bossHurtbox = hit.GetComponentInParent<BossHurtbox>();
            if (bossHurtbox == null)
            {
                continue;
            }

            NetworkBossCore boss = bossHurtbox.GetComponentInParent<NetworkBossCore>();
            if (boss == null)
            {
                continue;
            }

            if (!_bestBossHurtboxes.TryGetValue(boss, out BossHurtbox bestHurtbox) ||
                bossHurtbox.damageMultiplier > bestHurtbox.damageMultiplier)
            {
                // 같은 보스 안에서는 머리/몸통처럼 여러 부위가 동시에 잡힐 수 있다.
                // 배율이 가장 높은 부위 하나만 남겨 한 공격이 같은 보스를 여러 번 때리지 않게 한다.
                _bestBossHurtboxes[boss] = bossHurtbox;
            }
        }

        foreach (BossHurtbox bossHurtbox in _bestBossHurtboxes.Values)
        {
            // 신형 파라미터 규격(데미지, 그로기 10f, 공격자) 적용
            // 최종적으로 보스별 대표 히트박스 하나에만 데미지를 전달한다.
            bossHurtbox.OnHitByPlayer(damage, 10f, Object);

            // 피 이펙트 스폰
            SpawnBloodOnHit(bossHurtbox.GetComponent<Collider>());
        }
    }

    private void SpawnBloodOnHit(Collider hitCollider)
    {
        if (bloodEffectSpawner == null || hitCollider == null)
        {
            return;
        }

        Vector3 hitCenter = transform.TransformPoint(AttackHitLocalCenter);
        Vector3 hitPoint = hitCollider.ClosestPoint(hitCenter);

        Vector3 direction = hitPoint - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.001f)
        {
            direction = transform.forward;
        }

        bloodEffectSpawner.SpawnBlood(hitPoint, direction.normalized);
    }

    private float GetBasicAttackDamage()
    {
        if (_playerStats == null)
        {
            return 0f;
        }

        float damage;
        if (!IsBasicAttackComboUnlocked)
        {
            damage = _playerStats.AttackPower;
        }
        else
        {
            damage = BasicAttackComboIndex switch
            {
                1 => _playerStats.SecondAttackDamage,
                2 => _playerStats.ThirdAttackDamage,
                _ => _playerStats.FirstAttackDamage
            };
        }

        return damage * GetOutgoingDamageMultiplier();
    }

    private float GetOutgoingDamageMultiplier()
    {
        return _statusController != null ? _statusController.GetOutgoingDamageMultiplier() : 1f;
    }

    private void TriggerAction(byte actionType)
    {
        // 네트워크 액션 코드를 실제 Animator 트리거로 변환한다.
        // 이 함수는 Render에서 ActionSequence 변경을 감지했을 때만 호출된다.
        // 따라서 입력권한/상태권한 모두 같은 서버 확정 이벤트를 보고 같은 표현을 재생한다.
        switch (actionType)
        {
            case ActionNone:
                animator.SetBool(IsCrawling, false);
                animator.CrossFade("idle1", 0.1f);
                break;
            case ActionAttack:
                if (!IsBasicAttackComboUnlocked)
                {
                    // 콤보 해금 전 기본 공격은 항상 slash2다.
                    // 같은 State를 반복 재생해야 하므로 Any State 자기 전이에 의존하지 않고 직접 처음부터 재생한다.
                    // 이 처리가 없으면 현재 slash2 재생 중 다시 Attack2가 들어왔을 때 self transition 설정에 따라 씹힐 수 있다.
                    animator.ResetTrigger(GetBasicAttackTrigger());
                    animator.CrossFade(GetBasicAttackStateHash(), 0.03f, 0, 0f);
                    break;
                }

                if (IsAnimatorInState(GetBasicAttackStateHash()))
                {
                    // 콤보 해금 후에는 slash2 -> slash3 -> slash4처럼 다른 State로 넘어가는 것이 정상이다.
                    // 이미 같은 State라면 같은 서버 이벤트를 중복 수신한 상황일 수 있어 트리거를 정리하고 무시한다.
                    animator.ResetTrigger(GetBasicAttackTrigger());
                    break;
                }

                // 콤보 해금 후에는 Animator Controller의 Any State trigger transition을 사용한다.
                // StateMachineBehaviour가 State 진입/종료와 입력 창 오픈을 관리한다.
                animator.SetTrigger(GetBasicAttackTrigger());
                break;
            case ActionParry:
                animator.SetTrigger(Parry);
                break;
            case ActionRoll:
                animator.SetTrigger(Roll);
                break;
            case ActionJump:
                animator.SetBool(IsLockOn, false);
                animator.SetTrigger(Jump);
                break;
            case ActionJumpForward:
                animator.SetBool(IsLockOn, false);
                animator.SetTrigger(Jump2);
                break;
            case ActionImpact:
                animator.SetTrigger(Impact);
                break;
            case ActionParryImpact:
                animator.SetTrigger(Impact2);
                break;
            case ActionDeath:
                animator.SetBool(IsCrawling, true);
                animator.SetTrigger(Death);
                break;
        }
    }

    private void ResetActionTriggers()
    {
        // 새 액션 트리거를 넣기 전 이전 프레임에 남은 trigger를 모두 비운다.
        // Animator trigger는 한 번 설정되면 전이를 못 탔을 때 다음 전이에 남아 영향을 줄 수 있다.
        animator.ResetTrigger(Attack);
        animator.ResetTrigger(Attack2);
        animator.ResetTrigger(Attack3);
        animator.ResetTrigger(Attack4);
        animator.ResetTrigger(Parry);
        animator.ResetTrigger(Roll);
        animator.ResetTrigger(Jump);
        animator.ResetTrigger(Jump2);
        animator.ResetTrigger(Impact);
        animator.ResetTrigger(Impact2);
        animator.ResetTrigger(Death);
        ResetTurnTriggers();
    }

    private bool IsJumpAction(byte actionType)
    {
        return actionType == ActionJump || actionType == ActionJumpForward;
    }

    private bool ShouldUseForwardJumpAnimation()
    {
        float requiredRunSpeed = runSpeed * GetMoveSpeedMultiplier() * runJumpSpeedRatio;
        return CurrentMoveSpeed >= requiredRunSpeed;
    }

    private bool IsForwardJumpRootMotionActive()
    {
        return useForwardJumpRootMotion && LastAction == ActionJumpForward && IsActionAnimationLocked;
    }

    private bool IsRollRootMotionActive()
    {
        return LastAction == ActionRoll && IsActionAnimationLocked;
    }

    private bool IsSkillRootMotionActive()
    {
        return GetCurrentActionLockType() == PlayerActionLockType.Skill &&
               IsSkillRootMotionAnimatorStateActive();
    }

    private bool IsParryAnimatorStateActive()
    {
        return IsAnimatorInState(Blocking1State) ||
               IsAnimatorInState(Blocking2State) ||
               IsAnimatorInState(Blocking3State);
    }

    private bool IsTransitioningToParryState()
    {
        if (animator == null || !animator.IsInTransition(0))
        {
            return false;
        }

        AnimatorStateInfo nextState = animator.GetNextAnimatorStateInfo(0);
        return IsParryState(nextState.shortNameHash);
    }

    private static bool IsParryState(int stateHash)
    {
        return stateHash == Blocking1State ||
               stateHash == Blocking2State ||
               stateHash == Blocking3State;
    }

    private bool IsSkillRootMotionAnimatorStateActive()
    {
        if (animator == null)
        {
            return false;
        }

        AnimatorStateInfo currentState = animator.GetCurrentAnimatorStateInfo(0);
        if (IsRootMotionSkillState(currentState.shortNameHash))
        {
            return true;
        }

        if (!animator.IsInTransition(0))
        {
            return false;
        }

        AnimatorStateInfo nextState = animator.GetNextAnimatorStateInfo(0);
        return IsRootMotionSkillState(nextState.shortNameHash);
    }

    private static bool IsRootMotionSkillState(int stateHash)
    {
        return stateHash == SlideAttackState ||
               stateHash == HighSpinAttackState ||
               stateHash == JumpAttackState ||
               stateHash == GreatSwordSlideAttackState ||
               stateHash == GreatSwordHighSpinAttackState ||
               stateHash == GreatSwordJumpAttackState;
    }

    private int GetBasicAttackTrigger()
    {
        // 콤보 해금 전에는 항상 첫 기본공격 트리거만 사용한다.
        if (!IsBasicAttackComboUnlocked)
        {
            return Attack2;
        }

        // 해금 후에는 서버가 확정한 콤보 인덱스를 Animator trigger로 변환한다.
        return BasicAttackComboTriggers[Mathf.Clamp(BasicAttackComboIndex, 0, BasicAttackComboLastIndex)];
    }

    private int GetBasicAttackStateHash()
    {
        // 현재 재생 중인지 비교할 Animator State hash도 트리거 선택과 같은 규칙을 사용한다.
        if (!IsBasicAttackComboUnlocked)
        {
            return Slash2State;
        }

        return Mathf.Clamp(BasicAttackComboIndex, 0, BasicAttackComboLastIndex) switch
        {
            1 => Slash3State,
            2 => Slash4State,
            _ => Slash2State
        };
    }

    private bool IsAnimatorInState(int stateHash)
    {
        if (animator == null)
        {
            return false;
        }

        // 현재 State가 목표 State이고 거의 끝난 상태가 아니면, 같은 트리거를 다시 넣어 중복 전이를 만들지 않는다.
        AnimatorStateInfo currentState = animator.GetCurrentAnimatorStateInfo(0);
        if (currentState.shortNameHash == stateHash && currentState.normalizedTime < 0.98f)
        {
            return true;
        }

        if (!animator.IsInTransition(0))
        {
            return false;
        }

        AnimatorStateInfo nextState = animator.GetNextAnimatorStateInfo(0);
        // 전이 중이라면 다음 State까지 확인해 같은 액션이 겹쳐 들어가는 것을 막는다.
        return nextState.shortNameHash == stateHash;
    }

    private static bool IsDamageOrDeathAction(byte actionType)
    {
        // 피격/사망 반응은 스킬 시전보다 우선순위가 높은 연출 이벤트다.
        // 늦게 도착한 스킬 RPC가 이 애니메이션을 덮어쓰지 못하도록 구분한다.
        return actionType == ActionImpact ||
               actionType == ActionParryImpact ||
               actionType == ActionDeath;
    }

}
