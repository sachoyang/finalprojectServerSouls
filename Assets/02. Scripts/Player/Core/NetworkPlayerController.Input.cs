using System.Collections.Generic;
using System.Text;
using Fusion;
using UnityEngine;

public partial class NetworkPlayerController
{
    private void Update()
    {
        // 로컬 플레이어만 디버그 UI 토글 입력을 읽는다.
        if (Object == null || !Object.HasInputAuthority)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.Slash))
        {
            _showPlayerDebug = !_showPlayerDebug;
        }
    }

    public override void FixedUpdateNetwork()
    {
        UpdateAnimatorRootMotionMode();
        RestoreDefaultGravityIfGrounded();

        if (HasControlLock(PlayerControlLockFlags.Movement))
        {
            StopForControlLock();
            return;
        }

        // HP가 0이 되면 전투 입력은 막고 느린 크롤링 이동만 허용한다.
        if (_playerStats != null && _playerStats.IsDead)
        {
            HandleCrawlingMovement();
            return;
        }

        if (!GetInput(out NetworkInputData data))
        {
            // 입력을 못 받는 틱에는 이동 상태를 정리해 보간 잔상을 줄인다.
            float speed = UpdateCurrentMoveSpeed(0f, false);
            ApplyMovement(Vector3.zero, speed, GetLockOnFacingDirection());
            UpdateMovementState(false, false, LockMoveIdle, GetNormalMoveBlendFromSpeed(speed, GetMoveSpeedMultiplier()));
            WasShiftHeld = false;
            ShiftHoldTime = 0f;
            return;
        }

        ProcessLockOnInput(data);

        // 입력 방향은 대각선 이동이 더 빨라지지 않도록 정규화한다.
        Vector3 desiredMove = data.direction;
        if (desiredMove.sqrMagnitude > 1f)
        {
            desiredMove.Normalize();
        }

        bool shiftHeld = data.buttons.IsSet(NetworkInputData.SHIFT);
        if (shiftHeld)
        {
            ShiftHoldTime = WasShiftHeld ? ShiftHoldTime + Runner.DeltaTime : 0f;
        }
        else if (!WasShiftHeld)
        {
            ShiftHoldTime = 0f;
        }

        bool shiftReleased = WasShiftHeld && !shiftHeld;
        bool isRolling = IsRollRootMotionActive();
        bool isActing = IsActionAnimationLocked;
        bool rawAttackPressed = data.buttons.IsSet(NetworkInputData.MOUSEBUTTON0);
        bool rawParryPressed = data.buttons.IsSet(NetworkInputData.MOUSEBUTTON1);
        bool rawJumpPressed = data.buttons.IsSet(NetworkInputData.JUMP);
        bool hasActionInput = rawAttackPressed || rawParryPressed || rawJumpPressed;
        // Fusion 입력은 누르고 있는 동안 여러 틱에서 반복 전달될 수 있다.
        // actionId를 한 번만 소비해서 "한 번 누른 입력"이 공격/점프/패링을 중복 실행하지 않게 한다.
        bool canUseActionInput = hasActionInput &&
                                 !HasControlLock(PlayerControlLockFlags.Action) &&
                                 TryConsumeInputAction(data.actionId);
        bool jumpPressed = canUseActionInput && rawJumpPressed;
        bool attackPressed = canUseActionInput && !rawJumpPressed && rawAttackPressed;
        bool parryPressed = canUseActionInput && !rawJumpPressed && !rawAttackPressed && rawParryPressed;

        if (rawJumpPressed)
        {
            // 점프 입력은 공격 콤보보다 우선도가 높다.
            // 공격 선입력이 남아 있으면 점프 직후 공격이 예약 실행될 수 있으므로 즉시 비운다.
            ClearComboRequests();
        }

        if (CanStartQueuedComboAttack(isActing))
        {
            // 이미 큐에 들어간 후속 공격은 현재 액션락이 풀린 첫 틱에 실행한다.
            // 스태미나는 실행 직전에 다시 검사해서, 대기 중 자원이 바뀐 경우를 반영한다.
            if (TrySpendBasicAttackStamina())
            {
                StartBasicAttack(GetNextBasicAttackComboIndex(), _queuedComboActionId);
                isActing = true;
            }
            else
            {
                ClearComboRequests();
            }
        }
        else if (attackPressed)
        {
            // 공격 중 입력은 Animator StateBehaviour가 연 입력 가능 구간에서만 다음 콤보로 queue된다.
            TryQueueBasicAttackCombo(isActing, data.actionId);
        }

        bool isJumpAction = isActing && IsJumpAction(LastAction);
        // 공격/패링은 제자리 고정, 점프/구르기는 자체 이동을 허용한다.
        bool actionBlocksMovement = isActing && !isJumpAction && !isRolling;
        bool isBusy = isRolling || isActing;

        if (desiredMove.sqrMagnitude > 0.001f)
        {
            _lastMoveDirection = desiredMove.normalized;
        }

        if (!isBusy)
        {
            // 액션 입력은 서버 권한에서만 확정한다.
            // 비호스트 클라이언트는 입력만 보내고, 애니메이션은 ActionSequence 수신 후 재생한다.
            if (jumpPressed && _networkCharacterController.Grounded)
            {
                if (TrySpendJumpStamina())
                {
                    bool useForwardJump = ShouldUseForwardJumpAnimation();
                    if (useForwardJump)
                    {
                        ForwardJumpDirection = desiredMove.sqrMagnitude > 0.001f
                            ? desiredMove.normalized
                            : Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
                    }
                    else
                    {
                        ForwardJumpDirection = Vector3.zero;
                    }

                    ApplyJumpPhysics(useForwardJump);
                    StartAction(useForwardJump ? ActionJumpForward : ActionJump, data.actionId);
                    isActing = true;
                    isBusy = true;
                }
            }
            else if (attackPressed)
            {
                // 기본 공격은 StateAuthority에서 최종 스태미나와 피격 판정을 처리한다.
                if (TrySpendBasicAttackStamina())
                {
                    StartBasicAttack(GetOpeningBasicAttackComboIndex(), data.actionId);
                    isActing = true;
                    isBusy = true;
                }
            }
            else if (parryPressed)
            {
                // 패링 중 피격되면 PlayerStats가 Impact2 액션을 요청한다.
                if (TrySpendParryStamina())
                {
                    StartAction(ActionParry, data.actionId);
                    isActing = true;
                    isBusy = true;
                }
            }
            else if (shiftReleased && ShiftHoldTime < shiftHoldThreshold)
            {
                // Shift를 짧게 뗐을 때 구르기, 오래 누르면 달리기로 처리한다.
                if (_playerStats == null || _playerStats.TryUseActionStamina(_playerStats.RollStaminaCost))
                {
                    StartRoll(desiredMove);
                    isRolling = true;
                    isBusy = true;
                }
            }
        }

        float runStaminaCost = _playerStats != null ? _playerStats.RunStaminaPerSecond * Runner.DeltaTime : 0f;
        bool shouldRun = desiredMove.sqrMagnitude > 0.001f &&
                         shiftHeld &&
                         ShiftHoldTime >= shiftHoldThreshold &&
                         !isRolling &&
                         !actionBlocksMovement &&
                         (_playerStats == null || _playerStats.HasStamina(runStaminaCost));

        float targetSpeed = 0f;
        Vector3 moveDirection = Vector3.zero;
        Vector3 facingDirection = IsLockOnNetworked ? GetLockOnFacingDirection() : Vector3.zero;

        if (!actionBlocksMovement && desiredMove.sqrMagnitude > 0.001f)
        {
            targetSpeed = shouldRun ? runSpeed : walkSpeed;
            moveDirection = desiredMove.normalized;
        }

        float moveSpeedMultiplier = GetMoveSpeedMultiplier();
        targetSpeed *= moveSpeedMultiplier;
        float currentSpeed = UpdateCurrentMoveSpeed(targetSpeed, false);

        if (actionBlocksMovement)
        {
            // 제자리 액션 중에는 이전 틱의 수평 속도가 남아 미끄러지지 않게 지운다.
            StopHorizontalVelocity();
            currentSpeed = UpdateCurrentMoveSpeed(0f, true);
        }

        if (IsForwardJumpRootMotionActive())
        {
            ApplyJumpRootMotion();
            UpdateMovementState(false, false, LockMoveIdle, 0f);
            WasShiftHeld = shiftHeld;
            return;
        }

        if (IsRollRootMotionActive())
        {
            ApplyRollRootMotion();
            UpdateMovementState(false, false, LockMoveIdle, 0f);
            WasShiftHeld = shiftHeld;
            return;
        }

        if (IsSkillRootMotionActive())
        {
            ApplySkillRootMotion();
            UpdateMovementState(false, false, LockMoveIdle, 0f);
            WasShiftHeld = shiftHeld;
            return;
        }

        ApplyMovement(moveDirection, currentSpeed, facingDirection);
        if (shouldRun && _playerStats != null)
        {
            _playerStats.TryUseStamina(runStaminaCost);
        }

        byte lockMove = IsLockOnNetworked && !isBusy ? GetLockOnMoveCode(moveDirection, shouldRun) : LockMoveIdle;
        UpdateMovementState(moveDirection.sqrMagnitude > 0.001f, shouldRun, lockMove, GetNormalMoveBlendFromSpeed(currentSpeed, moveSpeedMultiplier));
        WasShiftHeld = shiftHeld;

        if (!shiftHeld)
        {
            ShiftHoldTime = 0f;
        }
    }

    private void ProcessLockOnInput(NetworkInputData data)
    {
        // 락온 취소는 다른 락온 처리보다 우선한다.
        if (data.buttons.IsSet(NetworkInputData.LOCKON_CANCEL))
        {
            ClearLockOn();
            return;
        }

        if (data.buttons.IsSet(NetworkInputData.LOCKON))
        {
            SelectNextLockOnTarget();
        }

        if (_lockOnTarget == null)
        {
            // 선택된 대상이 없으면 네트워크 락온 상태도 꺼서 Animator가 일반 이동으로 돌아간다.
            IsLockOnNetworked = false;
            LockOnMoveNetworked = LockMoveIdle;
            return;
        }

        if (!lockOnTargetSelector.IsCurrentTargetValid(transform, _lockOnTarget))
        {
            // 대상 Root가 비활성/비대상화되거나, 플레이어가 부활해 더 이상 락온 대상이 아니면 정리한다.
            ClearLockOn();
            return;
        }

        // Transform 참조는 네트워크로 직접 공유하지 않고, 모든 클라이언트가 사용할 위치만 동기화한다.
        LockOnPointPosition = _lockOnTarget.position;
        IsLockOnNetworked = true;
    }

    private void SelectNextLockOnTarget()
    {
        // 가장 가까운 보스의 락온 포인트들을 순환 선택한다.
        if (lockOnTargetSelector == null)
        {
            ClearLockOn();
            return;
        }

        // 대상 검색/순환은 선택 전용 컴포넌트가 맡고, 컨트롤러는 전투 상태와 카메라 전달만 갱신한다.
        // viewCamera를 넘겨 화면 중심에 가장 가까운 후보를 우선 락온하도록 한다.
        _lockOnTarget = lockOnTargetSelector.SelectNextTarget(transform, _lockOnTarget, viewCamera);
        if (_lockOnTarget == null)
        {
            ClearLockOn();
            return;
        }

        LockOnPointPosition = _lockOnTarget.position;
        IsLockOnNetworked = true;

        if (Object.HasInputAuthority)
        {
            GetCameraManager()?.SetLockOnTarget(_lockOnTarget);
        }
    }

    private void ClearLockOn()
    {
        // 선택기 내부 순환 상태와 컨트롤러의 현재 타겟을 함께 비운다.
        _lockOnTarget = null;
        lockOnTargetSelector?.Clear();
        IsLockOnNetworked = false;
        LockOnMoveNetworked = LockMoveIdle;

        if (Object.HasInputAuthority)
        {
            // 카메라 락온은 내 화면에만 영향을 주므로 입력권한 클라이언트에서만 해제한다.
            GetCameraManager()?.ClearLockOnTarget();
        }
    }

}
