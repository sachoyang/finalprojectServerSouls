using System.Collections.Generic;
using System.Text;
using Fusion;
using UnityEngine;

public partial class NetworkPlayerController
{
    public override void Spawned()
    {
        _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);
        ResetLocalActionState();
        UpdatePlayerTag();
        PlayerRegistry.Register(this);
        _abilityInventory?.RestoreFromSessionData(Object.InputAuthority);

        // 카메라는 각 클라이언트의 내 플레이어만 따라가야 한다.
        if (!Object.HasInputAuthority)
        {
            return;
        }

        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            return;
        }

        viewCamera = mainCamera;
        ThirdPersonCameraController thirdPersonCamera = mainCamera.GetComponent<ThirdPersonCameraController>();
        if (thirdPersonCamera != null)
        {
            _cameraManager = CameraManager.GetOrCreate();
            _cameraManager.RegisterGameplayCamera(mainCamera, transform);
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        SetWeaponTrailActive(false, true);
        PlayerRegistry.Unregister(this);
    }

    private void ResetLocalActionState()
    {
        _localActionAnimationLocked = false;
        _localActionLockType = (byte)StateActionLockType.None;
        _localComboInputWindowOpen = false;
        _localParryGuardActive = false;
        _parryGuardStateDepth = 0;
        _invincibilityStateDepth = 0;
        _animatorStateRootMotionActive = false;
        SetWeaponTrailActive(false, true);
        ClearComboRequests();
        _lastLocalConsumedActionId = 0;
        if (animator != null)
        {
            ResetActionTriggers();
            animator.SetFloat(MoveSpeed, 0f);
            animator.SetBool(IsLockOn, false);
            animator.SetFloat(LockMoveX, 0f);
            animator.SetFloat(LockMoveY, 0f);
            animator.SetFloat(LockMoveSpeed, 0f);
        }

        if (HasStateAuthority)
        {
            LastAction = ActionNone;
            LastActionId = 0;
            LastConsumedActionId = 0;
            CurrentMoveSpeed = 0f;
            MoveSpeedBlendNetworked = 0f;
            RollDirection = Vector3.zero;
            ForwardJumpDirection = Vector3.zero;
            ParryGuardActive = false;
            if (_networkCharacterController != null)
            {
                _networkCharacterController.gravity = _networkControllerGravity;
            }
            ActionAnimationLocked = false;
            ActionLockType = (byte)StateActionLockType.None;
            ComboInputWindowOpen = false;
            ControlLockMask = 0;
        }
    }

    public bool HasControlLock(PlayerControlLockFlags flags)
    {
        int mask = ControlLockMask | _localControlLockMask;
        return ((PlayerControlLockFlags)mask & flags) != PlayerControlLockFlags.None;
    }

    public void SetControlLock(PlayerControlLockFlags flags, bool isLocked)
    {
        int flagMask = (int)flags;
        _localControlLockMask = isLocked
            ? _localControlLockMask | flagMask
            : _localControlLockMask & ~flagMask;

        if (Object == null)
        {
            return;
        }

        if (HasStateAuthority)
        {
            ApplyControlLockMask(flagMask, isLocked);
            return;
        }

        if (Object.HasInputAuthority)
        {
            RPC_SetControlLock(flagMask, isLocked);
        }
    }

    private void ApplyControlLockMask(int flagMask, bool isLocked)
    {
        ControlLockMask = isLocked
            ? ControlLockMask | flagMask
            : ControlLockMask & ~flagMask;
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_SetControlLock(int flagMask, NetworkBool isLocked)
    {
        ApplyControlLockMask(flagMask, isLocked);
    }

    public override void Render()
    {
        UpdatePlayerTag();

        // 네트워크 상태 변화는 Render에서 Animator 트리거로 변환한다.
        if (animator == null)
        {
            return;
        }

        foreach (string change in _changeDetector.DetectChanges(this))
        {
            if (change == nameof(ActionSequence))
            {
                // 서버가 확정한 일회성 액션 이벤트만 Animator 트리거로 변환한다.
                ResetActionTriggers();
                TriggerAction(LastAction);
            }
        }

        if (Object.HasInputAuthority)
        {
            // 락온 카메라 타겟도 로컬 플레이어의 카메라에만 반영한다.
            if (IsLockOnNetworked && _lockOnTarget != null)
            {
                GetCameraManager()?.SetLockOnTarget(_lockOnTarget);
            }
            else
            {
                GetCameraManager()?.ClearLockOnTarget();
            }
        }

        bool lockOnMovement = IsLockOnNetworked && !IsInActionAnimation();
        // 락온 이동 블렌드 트리와 일반 이동 파라미터가 서로 섞이지 않게 분리한다.
        animator.SetBool(IsCrawling, _playerStats != null && _playerStats.IsDead);
        float normalMoveBlend = lockOnMovement || !IsMovingNetworked
            ? 0f
            : Mathf.Clamp01(MoveSpeedBlendNetworked);
        SetAnimatorFloatDampedAndSnap(MoveSpeed, normalMoveBlend);
        UpdateLockOnAnimatorParameters(lockOnMovement, LockOnMoveNetworked);
    }

    private void UpdatePlayerTag()
    {
        // 사망 후 기어가는 상태에서는 보스가 Player 태그 대상으로 보지 않도록 태그를 바꾼다.
        bool isDead = _playerStats != null && _playerStats.IsDead;
        string targetTag = isDead ? DeadPlayerTag : AlivePlayerTag;

        if (!gameObject.CompareTag(targetTag))
        {
            gameObject.tag = targetTag;
        }
    }

}
