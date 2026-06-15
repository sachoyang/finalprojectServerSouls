using System.Collections.Generic;
using System.Text;
using Fusion;
using UnityEngine;

public partial class NetworkPlayerController
{
    private void HandleCrawlingMovement()
    {
        // 사망 후에는 락온을 해제하고 일반 입력 방향으로만 천천히 기어간다.
        ClearLockOn();

        Vector3 desiredMove = Vector3.zero;
        if (GetInput(out NetworkInputData data))
        {
            desiredMove = data.direction;
            if (desiredMove.sqrMagnitude > 1f)
            {
                desiredMove.Normalize();
            }
        }

        Vector3 moveDirection = desiredMove.sqrMagnitude > 0.001f ? desiredMove.normalized : Vector3.zero;
        CurrentMoveSpeed = moveDirection.sqrMagnitude > 0.001f ? crawlSpeed * GetMoveSpeedMultiplier() : 0f;
        MoveSpeedBlendNetworked = 0f;
        ApplyMovement(moveDirection, crawlSpeed * GetMoveSpeedMultiplier(), Vector3.zero);
        UpdateMovementState(moveDirection.sqrMagnitude > 0.001f, false, LockMoveIdle, 0f);
        WasShiftHeld = false;
        ShiftHoldTime = 0f;
    }

    private CameraManager GetCameraManager()
    {
        // CameraManager는 씬에 미리 없을 수도 있어서 필요할 때 생성/조회한다.
        if (_cameraManager != null)
        {
            return _cameraManager;
        }

        _cameraManager = CameraManager.GetOrCreate();
        if (viewCamera != null)
        {
            // 등록된 게임플레이 카메라가 있어야 락온 타겟/컷신 카메라 전환이 같은 매니저를 통해 동작한다.
            _cameraManager.RegisterGameplayCamera(viewCamera, transform);
        }

        return _cameraManager;
    }

    private Vector3 GetLockOnFacingDirection()
    {
        if (!IsLockOnNetworked)
        {
            return Vector3.zero;
        }

        Vector3 direction = Vector3.ProjectOnPlane(LockOnPointPosition - transform.position, Vector3.up);
        return direction.sqrMagnitude > 0.001f ? direction.normalized : Vector3.zero;
    }

    private byte GetLockOnMoveCode(Vector3 moveDirection, bool isRunning)
    {
        if (moveDirection.sqrMagnitude <= 0.001f)
        {
            return LockMoveIdle;
        }

        Vector3 facing = GetLockOnFacingDirection();
        if (facing.sqrMagnitude <= 0.001f)
        {
            return LockMoveIdle;
        }

        Vector3 right = Vector3.Cross(Vector3.up, facing).normalized;
        float forwardDot = Vector3.Dot(moveDirection.normalized, facing);
        float rightDot = Vector3.Dot(moveDirection.normalized, right);

        // 락온 중 입력 방향을 보스 기준 전/후/좌/우 코드로 바꿔 2D 블렌드 트리에 넘긴다.
        // Dot 값이 더 큰 축을 우선해 대각선 입력도 하나의 주 이동 방향으로 정리한다.
        if (Mathf.Abs(rightDot) > Mathf.Abs(forwardDot))
        {
            if (rightDot < 0f)
            {
                return isRunning ? LockMoveRunLeft : LockMoveLeft;
            }

            return isRunning ? LockMoveRunRight : LockMoveRight;
        }

        if (forwardDot >= 0f)
        {
            return isRunning ? LockMoveRunForward : LockMoveForward;
        }

        return isRunning ? LockMoveRunBack : LockMoveBack;
    }

    private void UpdateLockOnAnimatorParameters(bool lockOnMovement, byte lockMove)
    {
        // 락온 이동 코드를 2D 블렌드 트리 좌표로 넘긴다.
        Vector2 blend = lockOnMovement ? GetLockOnBlend(lockMove) : Vector2.zero;
        float speed = lockOnMovement ? blend.magnitude : 0f;

        animator.SetBool(IsLockOn, lockOnMovement);
        animator.SetFloat(LockMoveX, blend.x, 0.12f, Time.deltaTime);
        animator.SetFloat(LockMoveY, blend.y, 0.12f, Time.deltaTime);
        animator.SetFloat(LockMoveSpeed, speed, 0.12f, Time.deltaTime);
    }

    private static Vector2 GetLockOnBlend(byte lockMove)
    {
        return lockMove switch
        {
            LockMoveForward => new Vector2(0f, 1f),
            LockMoveBack => new Vector2(0f, -1f),
            LockMoveLeft => new Vector2(-1f, 0f),
            LockMoveRight => new Vector2(1f, 0f),
            LockMoveRunLeft => new Vector2(-2f, 0f),
            LockMoveRunRight => new Vector2(2f, 0f),
            LockMoveRunForward => new Vector2(0f, 2f),
            LockMoveRunBack => new Vector2(0f, -2f),
            _ => Vector2.zero
        };
    }

    private bool TryStartTurnAnimation(Vector3 moveDirection, Vector3 facingDirection, bool isRunning, bool isBusy, float currentSpeed, float moveSpeedMultiplier)
    {
        if (isBusy ||
            IsTurnAnimationActive() ||
            moveDirection.sqrMagnitude <= 0.001f ||
            !TurnAnimationCooldown.ExpiredOrNotRunning(Runner))
        {
            return false;
        }

        float requiredSpeed = (isRunning ? runSpeed : walkSpeed) * moveSpeedMultiplier * turnStartSpeedRatio;
        if (currentSpeed < requiredSpeed)
        {
            return false;
        }

        Vector3 targetDirection = IsLockOnNetworked ? facingDirection : moveDirection;
        if (targetDirection.sqrMagnitude <= 0.001f)
        {
            return false;
        }

        Vector3 currentForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        if (currentForward.sqrMagnitude <= 0.001f)
        {
            return false;
        }

        Vector3 flatTargetDirection = Vector3.ProjectOnPlane(targetDirection, Vector3.up).normalized;
        float turnDot = Vector3.Dot(currentForward.normalized, flatTargetDirection);
        if (turnDot > -0.72f)
        {
            return false;
        }

        bool fastTurn = isRunning && !IsLockOnNetworked;
        TurnAnimationFast = fastTurn;
        TurnTargetDirection = flatTargetDirection;
        TurnNeedsFinalRotation = true;
        UpdateTurnResumeState(moveDirection, isRunning);
        TurnAnimationSequence++;
        float turnDuration = fastTurn ? 0.52f : 0.72f;
        TurnAnimationTimer = TickTimer.CreateFromSeconds(Runner, turnDuration);
        TurnAnimationCooldown = TickTimer.CreateFromSeconds(Runner, turnDuration + turnAnimationCooldownPadding);
        CurrentMoveSpeed = 0f;
        MoveSpeedBlendNetworked = 0f;
        _turnUsedRootMotionRotation = false;
        ClearQueuedRootMotion();
        UpdateAnimatorRootMotionMode();
        return true;
    }

    private bool IsTurnAnimationActive()
    {
        return !TurnAnimationTimer.ExpiredOrNotRunning(Runner);
    }

    private void CompleteTurnAnimationIfNeeded()
    {
        if (!HasStateAuthority || !TurnNeedsFinalRotation || IsTurnAnimationActive())
        {
            return;
        }

        if (!_turnUsedRootMotionRotation && TurnTargetDirection.sqrMagnitude > 0.001f)
        {
            RotateTurnTowardsTarget(turnRootMotionFallbackRotationSpeed * Runner.DeltaTime);
        }

        ClearQueuedRootMotion();
        _turnUsedRootMotionRotation = false;
        TurnNeedsFinalRotation = false;
        TurnTargetDirection = Vector3.zero;
        CurrentMoveSpeed = TurnResumeCurrentSpeed;
        MoveSpeedBlendNetworked = TurnResumeMoveSpeedBlend;
        LockOnMoveNetworked = TurnResumeLockMove;
    }

    private void UpdateTurnResumeState(Vector3 desiredMove, bool isRunning)
    {
        if (desiredMove.sqrMagnitude <= 0.001f)
        {
            TurnResumeCurrentSpeed = 0f;
            TurnResumeMoveSpeedBlend = 0f;
            TurnResumeLockMove = LockMoveIdle;
            return;
        }

        Vector3 moveDirection = desiredMove.normalized;
        float moveSpeedMultiplier = GetMoveSpeedMultiplier();
        TurnResumeCurrentSpeed = (isRunning ? runSpeed : walkSpeed) * moveSpeedMultiplier;
        TurnResumeMoveSpeedBlend = IsLockOnNetworked ? 0f : GetNormalMoveBlendFromSpeed(TurnResumeCurrentSpeed, moveSpeedMultiplier);
        TurnResumeLockMove = IsLockOnNetworked ? GetLockOnMoveCode(moveDirection, isRunning) : LockMoveIdle;
    }

    private bool IsInActionAnimation()
    {
        // Action 태그가 붙은 상태는 락온 블렌드 트리가 덮어쓰지 않게 보호한다.
        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
        return stateInfo.IsTag("Action") && stateInfo.normalizedTime < 0.98f;
    }

    private void ApplyJumpPhysics(bool forwardJump)
    {
        float height = Mathf.Max(0.01f, forwardJump ? forwardJumpHeight : jumpHeight);
        float airTime = Mathf.Max(0.1f, forwardJump ? forwardJumpAirTime : jumpAirTime);
        float gravityMagnitude = 8f * height / (airTime * airTime);
        float impulse = 4f * height / airTime;

        _networkCharacterController.gravity = -gravityMagnitude;
        _networkCharacterController.Jump(false, impulse);
    }

    private void RestoreDefaultGravityIfGrounded()
    {
        if (_networkCharacterController == null ||
            !_networkCharacterController.Grounded ||
            IsActionAnimationLocked)
        {
            return;
        }

        _networkCharacterController.gravity = _networkControllerGravity;
    }

    private bool IsLockOnAnimatorSuppressed()
    {
        return Time.time < _suppressLockOnAnimatorUntil;
    }

    private float GetMoveSpeedMultiplier()
    {
        return _statusController != null ? _statusController.GetMoveSpeedMultiplier() : 1f;
    }

    private float UpdateCurrentMoveSpeed(float targetSpeed, bool snap)
    {
        if (snap)
        {
            CurrentMoveSpeed = targetSpeed;
            return CurrentMoveSpeed;
        }

        float rate = targetSpeed > CurrentMoveSpeed ? moveSpeedAcceleration : moveSpeedDeceleration;
        CurrentMoveSpeed = Mathf.MoveTowards(CurrentMoveSpeed, targetSpeed, rate * Runner.DeltaTime);
        return CurrentMoveSpeed;
    }

    private float GetNormalMoveBlendFromSpeed(float currentSpeed, float moveSpeedMultiplier)
    {
        if (currentSpeed <= 0.001f)
        {
            return 0f;
        }

        float scaledWalkSpeed = Mathf.Max(0.001f, walkSpeed * moveSpeedMultiplier);
        float scaledRunSpeed = Mathf.Max(scaledWalkSpeed + 0.001f, runSpeed * moveSpeedMultiplier);
        if (currentSpeed <= scaledWalkSpeed)
        {
            return Mathf.Lerp(0f, 0.5f, currentSpeed / scaledWalkSpeed);
        }

        return Mathf.Lerp(0.5f, 1f, Mathf.InverseLerp(scaledWalkSpeed, scaledRunSpeed, currentSpeed));
    }

    private void StartRoll(Vector3 desiredMove)
    {
        // 구르기도 이동성 액션이므로 방향과 지속 시간을 서버 권한에서 확정한다.
        if (!HasStateAuthority)
        {
            return;
        }

        // 입력이 없으면 마지막 이동 방향, 그것도 없으면 현재 바라보는 방향으로 구른다.
        Vector3 rollDirection = desiredMove.sqrMagnitude > 0.001f ? desiredMove.normalized : _lastMoveDirection;
        if (rollDirection.sqrMagnitude < 0.001f)
        {
            rollDirection = transform.forward;
        }

        RollDirection = Vector3.ProjectOnPlane(rollDirection, Vector3.up).normalized;
        if (RollDirection.sqrMagnitude < 0.001f)
        {
            RollDirection = transform.forward;
        }

        StartAction(ActionRoll);
        RollTimer = TickTimer.CreateFromSeconds(Runner, rollDuration);
    }

    private void ApplyMovement(Vector3 moveDirection, float moveSpeed, Vector3 facingDirection)
    {
        // NetworkCharacterController의 속도 값을 액션별로 바꾼 뒤 이동을 적용한다.
        _networkCharacterController.maxSpeed = moveSpeed;
        _networkCharacterController.acceleration = movementAcceleration;
        _networkCharacterController.braking = movementBraking;

        if (facingDirection.sqrMagnitude > 0.001f)
        {
            _networkCharacterController.rotationSpeed = 0f;
            _networkCharacterController.Move(moveDirection);
            RotateTowards(facingDirection, lockOnRotationSpeed);
            return;
        }

        _networkCharacterController.rotationSpeed = _networkControllerRotationSpeed;
        _networkCharacterController.Move(moveDirection);

        // NetworkCharacterController도 이동 방향으로 회전하지만, 최종 회전 속도는 이 컨트롤러 설정을 기준으로 맞춘다.
        // 이동/락온/구르기마다 회전 속도를 다르게 줄 수 있게 직접 보정한다.
        RotateTowards(moveDirection, rotationSpeed);
    }

    private void StopHorizontalVelocity()
    {
        Vector3 velocity = _networkCharacterController.Velocity;
        velocity.x = 0f;
        velocity.z = 0f;
        _networkCharacterController.Velocity = velocity;
    }

    private void StopForControlLock()
    {
        // 이동 잠금 중에는 입력이 들어와도 서버 시뮬레이션에서 이동과 달리기 상태를 즉시 멈춘다.
        ClearComboRequests();
        StopHorizontalVelocity();
        CurrentMoveSpeed = 0f;
        MoveSpeedBlendNetworked = 0f;
        TurnNeedsFinalRotation = false;
        TurnResumeCurrentSpeed = 0f;
        TurnResumeMoveSpeedBlend = 0f;
        TurnResumeLockMove = LockMoveIdle;
        if (!IsParryActive())
        {
            ParryGuardActive = false;
            _localParryGuardActive = false;
        }
        _turnUsedRootMotionRotation = false;
        ClearQueuedRootMotion();
        _networkCharacterController.gravity = _networkControllerGravity;
        ApplyMovement(Vector3.zero, 0f, Vector3.zero);
        UpdateMovementState(false, false, LockMoveIdle, 0f);
        WasShiftHeld = false;
        ShiftHoldTime = 0f;
    }

    private void RotateTowards(Vector3 direction, float rotateSpeed)
    {
        // 이동 또는 락온 방향으로 부드럽게 회전한다.
        if (direction.sqrMagnitude <= 0.001f)
        {
            return;
        }

        _lastMoveDirection = direction.normalized;
        Quaternion targetRotation = Quaternion.LookRotation(_lastMoveDirection, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            targetRotation,
            rotateSpeed * Runner.DeltaTime);
    }

    private void UpdateMovementState(bool isMoving, bool isRunning, byte lockMove, float moveSpeedBlend)
    {
        IsMovingNetworked = isMoving;
        IsRunningNetworked = isRunning;
        LockOnMoveNetworked = lockMove;
        MoveSpeedBlendNetworked = moveSpeedBlend;
    }

}
