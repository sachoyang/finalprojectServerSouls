using System.Collections.Generic;
using System.Text;
using Fusion;
using UnityEngine;

[RequireComponent(typeof(NetworkCharacterController))]
public class NetworkPlayerController : NetworkBehaviour
{
    // Animator 파라미터 이름은 매 프레임 문자열로 찾지 않도록 해시로 캐싱한다.
    private static readonly int IsMoving = Animator.StringToHash("IsMoving");
    private static readonly int IsRunning = Animator.StringToHash("IsRunning");
    private static readonly int Attack = Animator.StringToHash("Attack");
    private static readonly int Attack2 = Animator.StringToHash("Attack2");
    private static readonly int Attack3 = Animator.StringToHash("Attack3");
    private static readonly int Attack4 = Animator.StringToHash("Attack4");
    private static readonly int Parry = Animator.StringToHash("Parry");
    private static readonly int Roll = Animator.StringToHash("Roll");
    private static readonly int Jump = Animator.StringToHash("Jump");
    private static readonly int Impact = Animator.StringToHash("Impact");
    private static readonly int Impact2 = Animator.StringToHash("Impact2");
    private static readonly int Death = Animator.StringToHash("Death");
    private static readonly int IsCrawling = Animator.StringToHash("IsCrawling");
    private static readonly int IsLockOn = Animator.StringToHash("IsLockOn");
    private static readonly int LockMoveX = Animator.StringToHash("LockMoveX");
    private static readonly int LockMoveY = Animator.StringToHash("LockMoveY");
    private static readonly int LockMoveSpeed = Animator.StringToHash("LockMoveSpeed");

    private const byte ActionAttack = 1;
    private const byte ActionParry = 2;
    private const byte ActionRoll = 3;
    private const byte ActionJump = 4;
    private const byte ActionImpact = 5;
    private const byte ActionParryImpact = 6;
    private const byte ActionDeath = 7;

    private const byte LockMoveIdle = 0;
    private const byte LockMoveForward = 1;
    private const byte LockMoveBack = 2;
    private const byte LockMoveLeft = 3;
    private const byte LockMoveRight = 4;
    private const byte LockMoveRunLeft = 5;
    private const byte LockMoveRunRight = 6;
    private const byte BasicAttackComboLastIndex = 2;

    private const string AlivePlayerTag = "Player";
    private const string DeadPlayerTag = "DeadPlayer";

    private static readonly int[] BasicAttackComboTriggers =
    {
        Attack2,
        Attack3,
        Attack4
    };

    [Header("References")]
    // 플레이어 모델 애니메이터와 로컬 플레이어가 바라볼 카메라.
    [SerializeField] private Animator animator;
    [SerializeField] private Camera viewCamera;

    [Header("Movement")]
    // 일반 이동, 달리기, 구르기, 다운 후 기어가기 속도.
    [SerializeField] private float walkSpeed = 2.6f;
    [SerializeField] private float runSpeed = 4.8f;
    [SerializeField] private float rollSpeed = 6.5f;
    [SerializeField] private float rotationSpeed = 720f;
    [SerializeField] private float rollDuration = 0.9f;
    [SerializeField] private float crawlSpeed = 0.9f;
    [SerializeField] private float shiftHoldThreshold = 0.25f;
    [SerializeField] private float movementAcceleration = 80f;
    [SerializeField] private float movementBraking = 30f;

    [Header("Action Locks")]
    // 액션 중 다른 입력을 잠깐 막는 시간과 점프 애니메이션 보정값.
    [SerializeField] private float jumpImpulse = 8f;
    [SerializeField] private float jumpAnimationLockDuration = 0.45f;

    [Header("Basic Attack Combo")]
    [SerializeField] private float comboGraceSeconds = 0.5f;

    [Header("Combat")]
    // 기본 공격 판정 구체의 위치와 크기. Gizmo도 이 값을 사용한다.
    [SerializeField] private float attackHitRadius = 1.4f;
    [SerializeField] private float attackHitDistance = 1.8f;
    [SerializeField] private float attackHitHeight = 1.1f;
    [SerializeField] private LayerMask attackTargetLayers = ~0;

    [Header("Lock On")]
    // 락온 가능한 보스 포인트 탐색 범위와 락온 중 회전 속도.
    [SerializeField] private LockOnTargetSelector lockOnTargetSelector;
    [SerializeField] private float lockOnSearchRadius = 80f;
    [SerializeField] private float lockOnRotationSpeed = 900f;

    // 네트워크로 동기화되는 이동/액션 상태. Render에서 애니메이션으로 변환된다.
    [Networked] private bool IsMovingNetworked { get; set; }
    [Networked] private bool IsRunningNetworked { get; set; }
    [Networked] private bool IsLockOnNetworked { get; set; }
    [Networked] private byte LockOnMoveNetworked { get; set; }
    [Networked] private Vector3 LockOnPointPosition { get; set; }
    [Networked] private bool WasShiftHeld { get; set; }
    [Networked] private float ShiftHoldTime { get; set; }
    [Networked] private TickTimer RollTimer { get; set; }
    [Networked] private Vector3 RollDirection { get; set; }
    [Networked] private byte LastAction { get; set; }
    [Networked] private int ActionSequence { get; set; }
    [Networked] private NetworkBool BasicAttackComboUnlocked { get; set; }
    [Networked] private byte BasicAttackComboIndex { get; set; }
    [Networked] private float BasicAttackComboExpiresAt { get; set; }
    [Networked] private NetworkBool ActionAnimationLocked { get; set; }
    [Networked] private byte ActionLockType { get; set; }
    [Networked] private NetworkBool ComboInputWindowOpen { get; set; }

    private NetworkCharacterController _networkCharacterController;
    private PlayerStats _playerStats;
    private PlayerAbilityInventory _abilityInventory;
    private PlayerAbilityRewardController _abilityRewardController;
    private ChangeDetector _changeDetector;
    private Vector3 _lastMoveDirection = Vector3.forward;
    private Transform _lockOnTarget;
    private CameraManager _cameraManager;
    private float _networkControllerRotationSpeed;
    private float _suppressLockOnAnimatorUntil;
    private int _predictedActionSequence;
    private readonly Collider[] _attackHits = new Collider[16];
    private readonly Dictionary<NetworkBossCore, BossHitbox> _bestBossHitboxes = new Dictionary<NetworkBossCore, BossHitbox>();
    private bool _localBasicAttackComboUnlocked;
    private bool _localActionAnimationLocked;
    private byte _localActionLockType;
    private bool _localComboInputWindowOpen;
    private bool _queuedComboAttack;
    private bool _showPlayerDebug;

    public bool IsLockOnActive => IsLockOnNetworked;
    public Transform CurrentLockOnTarget => _lockOnTarget;
    public float AttackHitRadius => attackHitRadius;
    public Vector3 AttackHitLocalCenter => Vector3.up * attackHitHeight + Vector3.forward * attackHitDistance;
    public bool IsBasicAttackComboUnlocked => BasicAttackComboUnlocked || _localBasicAttackComboUnlocked;
    public bool IsActionAnimationLocked => ActionAnimationLocked || _localActionAnimationLocked;

    private void Awake()
    {
        // 같은 오브젝트에 붙은 필수 컴포넌트를 잡고, 없으면 컨트롤러를 비활성화한다.
        animator ??= GetComponent<Animator>();
        _networkCharacterController = GetComponent<NetworkCharacterController>();
        _playerStats = GetComponent<PlayerStats>();
        _abilityInventory = GetComponent<PlayerAbilityInventory>();
        _abilityRewardController = GetComponent<PlayerAbilityRewardController>();
        lockOnTargetSelector ??= GetComponent<LockOnTargetSelector>();
        if (lockOnTargetSelector == null)
        {
            lockOnTargetSelector = gameObject.AddComponent<LockOnTargetSelector>();
        }

        lockOnTargetSelector.SetSearchRadius(lockOnSearchRadius);
        viewCamera ??= Camera.main;
        _networkControllerRotationSpeed = _networkCharacterController != null ? _networkCharacterController.rotationSpeed : 0f;

        if (animator == null || _networkCharacterController == null)
        {
            enabled = false;
            return;
        }

        animator.applyRootMotion = false;
        _lastMoveDirection = transform.forward;
    }

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

    public override void Spawned()
    {
        _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);
        UpdatePlayerTag();
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

    public override void FixedUpdateNetwork()
    {
        // HP가 0이 되면 전투 입력은 막고 느린 크롤링 이동만 허용한다.
        if (_playerStats != null && _playerStats.IsDead)
        {
            HandleCrawlingMovement();
            return;
        }

        if (!GetInput(out NetworkInputData data))
        {
            // 입력을 못 받는 틱에는 이동 상태를 정리해 보간 잔상을 줄인다.
            ApplyMovement(Vector3.zero, walkSpeed, GetLockOnFacingDirection());
            UpdateMovementState(false, false, LockMoveIdle);
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
        bool isRolling = !RollTimer.ExpiredOrNotRunning(Runner);
        bool isActing = IsActionAnimationLocked;
        bool attackPressed = data.buttons.IsSet(NetworkInputData.MOUSEBUTTON0);
        bool jumpPressed = data.buttons.IsSet(NetworkInputData.JUMP);

        if (jumpPressed)
        {
            _queuedComboAttack = false;
        }

        if (CanStartQueuedComboAttack(isActing))
        {
            StartBasicAttack(GetNextBasicAttackComboIndex());
            isActing = true;
            _queuedComboAttack = false;
        }
        else if (attackPressed && CanQueueBasicAttackCombo(isActing))
        {
            _queuedComboAttack = true;
        }

        bool isJumpAction = isActing && LastAction == ActionJump;
        // 공격/패링은 제자리 고정, 점프/구르기는 자체 이동을 허용한다.
        bool actionBlocksMovement = isActing && !isJumpAction && !isRolling;
        bool isBusy = isRolling || isActing;

        if (desiredMove.sqrMagnitude > 0.001f)
        {
            _lastMoveDirection = desiredMove.normalized;
        }

        if (!isBusy)
        {
            // 점프는 로컬 체감을 위해 네트워크 상태 갱신과 동시에 예측 애니메이션을 재생한다.
            if (jumpPressed && _networkCharacterController.Grounded)
            {
                _networkCharacterController.Jump(false, jumpImpulse);
                StartAction(ActionJump);
                isActing = true;
                isBusy = true;
                TriggerPredictedAction(ActionJump);
            }
            else if (attackPressed)
            {
                // 기본 공격은 StateAuthority에서 최종 스태미나와 피격 판정을 처리한다.
                if (TrySpendBasicAttackStamina())
                {
                    StartBasicAttack(GetOpeningBasicAttackComboIndex());
                    isActing = true;
                    isBusy = true;
                }
            }
            else if (data.buttons.IsSet(NetworkInputData.MOUSEBUTTON1))
            {
                // 패링 중 피격되면 PlayerStats가 Impact2 액션을 요청한다.
                StartAction(ActionParry);
                isActing = true;
                isBusy = true;
            }
            else if (shiftReleased && ShiftHoldTime < shiftHoldThreshold)
            {
                // Shift를 짧게 뗐을 때 구르기, 오래 누르면 달리기로 처리한다.
                if (_playerStats == null || _playerStats.TryUseStamina(_playerStats.RollStaminaCost))
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

        float currentSpeed = walkSpeed;
        Vector3 moveDirection = Vector3.zero;
        Vector3 facingDirection = IsLockOnNetworked ? GetLockOnFacingDirection() : Vector3.zero;

        // 구르기는 입력 방향이 아니라 시작 순간 저장한 방향으로 끝까지 민다.
        if (isRolling)
        {
            currentSpeed = rollSpeed;
            moveDirection = RollDirection;
            facingDirection = Vector3.zero;
        }
        else if (!actionBlocksMovement && desiredMove.sqrMagnitude > 0.001f)
        {
            currentSpeed = shouldRun ? runSpeed : walkSpeed;
            moveDirection = desiredMove.normalized;
        }

        if (actionBlocksMovement)
        {
            // 제자리 액션 중에는 이전 틱의 수평 속도가 남아 미끄러지지 않게 지운다.
            StopHorizontalVelocity();
        }

        ApplyMovement(moveDirection, currentSpeed, facingDirection);
        if (shouldRun && _playerStats != null)
        {
            _playerStats.TryUseStamina(runStaminaCost);
        }

        byte lockMove = IsLockOnNetworked && !isBusy ? GetLockOnMoveCode(moveDirection, shouldRun) : LockMoveIdle;
        UpdateMovementState(moveDirection.sqrMagnitude > 0.001f, shouldRun, lockMove);
        WasShiftHeld = shiftHeld;

        if (!shiftHeld)
        {
            ShiftHoldTime = 0f;
        }
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
                // 로컬 예측으로 이미 재생한 점프는 중복 트리거를 생략한다.
                if (Object.HasInputAuthority && ShouldSkipAuthoritativeActionPresentation())
                {
                    continue;
                }

                animator.ResetTrigger(Attack);
                animator.ResetTrigger(Attack2);
                animator.ResetTrigger(Attack3);
                animator.ResetTrigger(Attack4);
                animator.ResetTrigger(Parry);
                animator.ResetTrigger(Roll);
                animator.ResetTrigger(Jump);
                animator.ResetTrigger(Impact);
                animator.ResetTrigger(Impact2);
                animator.ResetTrigger(Death);
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

        bool lockOnMovement = IsLockOnNetworked && !IsLockOnAnimatorSuppressed() && !IsInActionAnimation();
        // 락온 이동 블렌드 트리와 일반 이동 파라미터가 서로 섞이지 않게 분리한다.
        animator.SetBool(IsCrawling, _playerStats != null && _playerStats.IsDead);
        animator.SetBool(IsMoving, lockOnMovement ? false : IsMovingNetworked);
        animator.SetBool(IsRunning, lockOnMovement ? false : IsRunningNetworked);
        UpdateLockOnAnimatorParameters(lockOnMovement, LockOnMoveNetworked);
    }

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
        ApplyMovement(moveDirection, crawlSpeed, Vector3.zero);
        UpdateMovementState(moveDirection.sqrMagnitude > 0.001f, false, LockMoveIdle);
        WasShiftHeld = false;
        ShiftHoldTime = 0f;
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
            IsLockOnNetworked = false;
            LockOnMoveNetworked = LockMoveIdle;
            return;
        }

        if (!_lockOnTarget.gameObject.activeInHierarchy)
        {
            ClearLockOn();
            return;
        }

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

        // 대상 검색/순환은 선택 전용 컴포넌트가 맡고, 컨트롤러는 전투 상태만 갱신한다.
        _lockOnTarget = lockOnTargetSelector.SelectNextTarget(transform, _lockOnTarget);
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
        _lockOnTarget = null;
        lockOnTargetSelector?.Clear();
        IsLockOnNetworked = false;
        LockOnMoveNetworked = LockMoveIdle;

        if (Object.HasInputAuthority)
        {
            GetCameraManager()?.ClearLockOnTarget();
        }
    }

    private CameraManager GetCameraManager()
    {
        if (_cameraManager != null)
        {
            return _cameraManager;
        }

        _cameraManager = CameraManager.GetOrCreate();
        if (viewCamera != null)
        {
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

        if (Mathf.Abs(rightDot) > Mathf.Abs(forwardDot))
        {
            if (rightDot < 0f)
            {
                return isRunning ? LockMoveRunLeft : LockMoveLeft;
            }

            return isRunning ? LockMoveRunRight : LockMoveRight;
        }

        return forwardDot >= 0f ? LockMoveForward : LockMoveBack;
    }

    private void UpdateLockOnAnimatorParameters(bool lockOnMovement, byte lockMove)
    {
        // 락온 이동 코드를 2D 블렌드 트리 좌표로 넘긴다.
        Vector2 blend = lockOnMovement ? GetLockOnBlend(lockMove) : Vector2.zero;
        float speed = lockOnMovement && (lockMove == LockMoveRunLeft || lockMove == LockMoveRunRight) ? 2f : blend.magnitude;

        animator.SetBool(IsLockOn, lockOnMovement);
        animator.SetFloat(LockMoveX, blend.x);
        animator.SetFloat(LockMoveY, blend.y);
        animator.SetFloat(LockMoveSpeed, speed);
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
            _ => Vector2.zero
        };
    }

    private bool ShouldSkipAuthoritativeActionPresentation()
    {
        if (ActionSequence == _predictedActionSequence)
        {
            return true;
        }

        if (!_localActionAnimationLocked)
        {
            return false;
        }

        PlayerActionLockType localLockType = (PlayerActionLockType)_localActionLockType;
        PlayerActionLockType incomingLockType = GetActionLockType(LastAction);
        return localLockType == PlayerActionLockType.Jump &&
               incomingLockType == PlayerActionLockType.Attack;
    }

    private bool IsInActionAnimation()
    {
        // Action 태그가 붙은 상태는 락온 블렌드 트리가 덮어쓰지 않게 보호한다.
        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
        return stateInfo.IsTag("Action") && stateInfo.normalizedTime < 0.98f;
    }

    private bool IsLockOnAnimatorSuppressed()
    {
        return Time.time < _suppressLockOnAnimatorUntil;
    }

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
        return IsBasicAttackComboUnlocked &&
               isActing &&
               LastAction == ActionAttack &&
               BasicAttackComboIndex < BasicAttackComboLastIndex &&
               !_queuedComboAttack &&
               IsComboInputWindowOpen() &&
               TrySpendBasicAttackStamina();
    }

    private bool CanStartQueuedComboAttack(bool isActing)
    {
        return IsBasicAttackComboUnlocked &&
               !isActing &&
               _queuedComboAttack &&
               LastAction == ActionAttack &&
               Runner.SimulationTime <= BasicAttackComboExpiresAt;
    }

    private byte GetOpeningBasicAttackComboIndex()
    {
        if (!IsBasicAttackComboUnlocked ||
            LastAction != ActionAttack ||
            BasicAttackComboIndex >= BasicAttackComboLastIndex ||
            Runner.SimulationTime > BasicAttackComboExpiresAt)
        {
            return 0;
        }

        return GetNextBasicAttackComboIndex();
    }

    private byte GetNextBasicAttackComboIndex()
    {
        return (byte)Mathf.Min(BasicAttackComboIndex + 1, BasicAttackComboLastIndex);
    }

    private bool TrySpendBasicAttackStamina()
    {
        return _playerStats == null || _playerStats.TryUseStamina(_playerStats.AttackStaminaCost);
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
        if (LastAction != ActionAttack)
        {
            return;
        }

        SetComboInputWindowOpen(true);
    }

    public void EndActionAnimation(PlayerActionLockType lockType)
    {
        // 나가는 State가 현재 락을 소유한 타입일 때만 해제한다.
        // 예: 공격 중 피격되면 현재 타입은 Impact가 되므로, 늦게 호출된 Attack Exit는 락을 풀 수 없다.
        if (lockType == PlayerActionLockType.None || GetCurrentActionLockType() != lockType)
        {
            return;
        }

        SetActionAnimationLocked(false);
        SetComboInputWindowOpen(false);

        if (LastAction == ActionAttack)
        {
            BasicAttackComboExpiresAt = Runner != null
                ? Runner.SimulationTime + Mathf.Max(0f, comboGraceSeconds)
                : Time.time + Mathf.Max(0f, comboGraceSeconds);
            return;
        }

        _queuedComboAttack = false;
    }

    private bool IsComboInputWindowOpen()
    {
        return ComboInputWindowOpen || _localComboInputWindowOpen;
    }

    private PlayerActionLockType GetCurrentActionLockType()
    {
        // 로컬 예측 중에는 네트워크 값보다 로컬 락 타입이 먼저 갱신될 수 있다.
        // 입력 권한 클라이언트의 즉각적인 조작 차단을 위해 로컬 락을 우선해서 본다.
        byte lockType = _localActionAnimationLocked ? _localActionLockType : ActionLockType;
        return (PlayerActionLockType)lockType;
    }

    private void SetActionAnimationLocked(bool isLocked, PlayerActionLockType lockType = PlayerActionLockType.None)
    {
        // StateAuthority는 네트워크 값까지 갱신하고, 입력 권한 클라이언트는 로컬 값으로 즉시 반응한다.
        // 이렇게 해야 애니메이션/입력 지연 때문에 공격, 패링, 스킬이 늦게 끼어드는 상황을 줄일 수 있다.
        _localActionAnimationLocked = isLocked;
        _localActionLockType = isLocked ? (byte)lockType : (byte)PlayerActionLockType.None;

        if (Object != null && HasStateAuthority)
        {
            ActionAnimationLocked = isLocked;
            ActionLockType = _localActionLockType;
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

    private void StartBasicAttack(byte comboIndex)
    {
        _queuedComboAttack = false;
        BasicAttackComboIndex = IsBasicAttackComboUnlocked
            ? (byte)Mathf.Clamp(comboIndex, 0, BasicAttackComboLastIndex)
            : (byte)0;
        BasicAttackComboExpiresAt = Runner != null ? Runner.SimulationTime : Time.time;
        StartAction(ActionAttack);
    }

    private void StartAction(byte actionType)
    {
        // 액션 번호를 올리면 모든 클라이언트의 Render에서 같은 애니메이션 트리거를 받는다.
        // 액션 번호를 올리면 모든 클라이언트의 Render에서 같은 애니메이션 트리거를 받는다.
        // 동시에 액션 타입별 락을 걸어 다음 입력 틱에서 다른 액션이 끼어들지 못하게 한다.
        LastAction = actionType;
        ActionSequence++;
        SetActionAnimationLocked(true, GetActionLockType(actionType));
        SetComboInputWindowOpen(false);

        if (actionType != ActionAttack)
        {
            _queuedComboAttack = false;
        }

        if (actionType == ActionAttack && HasStateAuthority)
        {
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

        LastAction = becameDead
            ? ActionDeath
            : IsParryActive()
                ? ActionParryImpact
                : ActionImpact;
        ActionSequence++;
        SetActionAnimationLocked(true, GetActionLockType(LastAction));
        SetComboInputWindowOpen(false);
        _queuedComboAttack = false;
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

    private bool IsParryActive()
    {
        return LastAction == ActionParry && IsActionAnimationLocked;
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
            ActionJump => PlayerActionLockType.Jump,
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

        _bestBossHitboxes.Clear();
        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = _attackHits[i];
            if (hit == null)
            {
                continue;
            }

            BossHitbox bossHitbox = hit.GetComponentInParent<BossHitbox>();
            if (bossHitbox == null)
            {
                continue;
            }

            NetworkBossCore boss = bossHitbox.GetComponentInParent<NetworkBossCore>();
            if (boss == null)
            {
                continue;
            }

            if (!_bestBossHitboxes.TryGetValue(boss, out BossHitbox bestHitbox) ||
                bossHitbox.damageMultiplier > bestHitbox.damageMultiplier)
            {
                _bestBossHitboxes[boss] = bossHitbox;
            }
        }

        foreach (BossHitbox bossHitbox in _bestBossHitboxes.Values)
        {
            bossHitbox.OnHitByPlayer(damage, Object);
        }
    }

    private float GetBasicAttackDamage()
    {
        if (_playerStats == null)
        {
            return 0f;
        }

        if (!IsBasicAttackComboUnlocked)
        {
            return _playerStats.AttackPower;
        }

        return BasicAttackComboIndex switch
        {
            1 => _playerStats.SecondAttackDamage,
            2 => _playerStats.ThirdAttackDamage,
            _ => _playerStats.FirstAttackDamage
        };
    }

    private void StartRoll(Vector3 desiredMove)
    {
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

        // NetworkCharacterController also rotates toward moveDirection, but this keeps
        // the local rotation speed setting in this controller authoritative.
        RotateTowards(moveDirection, rotationSpeed);
    }

    private void StopHorizontalVelocity()
    {
        Vector3 velocity = _networkCharacterController.Velocity;
        velocity.x = 0f;
        velocity.z = 0f;
        _networkCharacterController.Velocity = velocity;
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

    private void UpdateMovementState(bool isMoving, bool isRunning, byte lockMove)
    {
        IsMovingNetworked = isMoving;
        IsRunningNetworked = isRunning;
        LockOnMoveNetworked = lockMove;
    }

    private void TriggerAction(byte actionType)
    {
        // 네트워크 액션 코드를 실제 Animator 트리거로 변환한다.
        switch (actionType)
        {
            case ActionAttack:
                animator.SetTrigger(GetBasicAttackTrigger());
                break;
            case ActionParry:
                animator.SetTrigger(Parry);
                break;
            case ActionRoll:
                animator.SetTrigger(Roll);
                break;
            case ActionJump:
                _suppressLockOnAnimatorUntil = Time.time + jumpAnimationLockDuration;
                animator.SetBool(IsLockOn, false);
                animator.SetTrigger(Jump);
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

    private void TriggerPredictedAction(byte actionType)
    {
        // 입력권한 클라이언트에서 즉시 보여줘야 하는 액션은 예측 재생한다.
        if (!Object.HasInputAuthority || animator == null)
        {
            return;
        }

        _predictedActionSequence = ActionSequence;
        animator.ResetTrigger(Attack);
        animator.ResetTrigger(Attack2);
        animator.ResetTrigger(Attack3);
        animator.ResetTrigger(Attack4);
        animator.ResetTrigger(Parry);
        animator.ResetTrigger(Roll);
        animator.ResetTrigger(Jump);
        animator.ResetTrigger(Impact);
        animator.ResetTrigger(Impact2);
        animator.ResetTrigger(Death);
        TriggerAction(actionType);
    }

    private int GetBasicAttackTrigger()
    {
        if (!IsBasicAttackComboUnlocked)
        {
            return Attack2;
        }

        return BasicAttackComboTriggers[Mathf.Clamp(BasicAttackComboIndex, 0, BasicAttackComboLastIndex)];
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

    private void OnGUI()
    {
        // 슬래시(/) 키로 켜는 간단한 로컬 플레이어 디버그 패널.
        if (!_showPlayerDebug || Object == null || !Object.HasInputAuthority)
        {
            return;
        }

        const float width = 430f;
        const float margin = 30f;
        string debugText = BuildPlayerDebugText();

        GUIStyle boxStyle = new GUIStyle(GUI.skin.box)
        {
            alignment = TextAnchor.UpperLeft,
            padding = new RectOffset(16, 16, 14, 14)
        };

        GUIStyle labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 22,
            normal = { textColor = Color.white },
            wordWrap = true
        };

        float textHeight = labelStyle.CalcHeight(new GUIContent(debugText), width - 32f);
        float height = Mathf.Min(Screen.height - margin * 2f, Mathf.Max(220f, textHeight + 32f));
        Rect panelRect = new Rect(Screen.width - width - margin, margin, width, height);

        GUI.Box(panelRect, string.Empty, boxStyle);
        GUI.Label(new Rect(panelRect.x + 16f, panelRect.y + 12f, panelRect.width - 32f, panelRect.height - 24f), debugText, labelStyle);
    }

    private string BuildPlayerDebugText()
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine("[Player Debug]");

        if (_playerStats == null)
        {
            builder.AppendLine("Stats : missing");
        }
        else
        {
            builder.AppendLine($"HP : {_playerStats.CurrentHealth:0} / {_playerStats.MaxHealth:0}");
            builder.AppendLine($"Stamina : {_playerStats.CurrentStamina:0} / {_playerStats.MaxStamina:0}");
            builder.AppendLine($"Defense : {_playerStats.DefenseRate * 100f:0}%");
        }

        builder.AppendLine();
        builder.AppendLine("Skills");
        AppendRewardOptions(builder);

        if (_abilityInventory == null || _abilityInventory.EquippedModules.Count == 0)
        {
            builder.AppendLine("- None");
            return builder.ToString();
        }

        AppendEquippedPassiveSkills(builder);
        AppendActiveSkillCooldowns(builder);
        return builder.ToString();
    }

    private void AppendRewardOptions(StringBuilder builder)
    {
        if (_abilityRewardController == null ||
            _abilityRewardController.PendingOptions == null ||
            _abilityRewardController.PendingOptions.Count == 0)
        {
            return;
        }

        builder.AppendLine();
        builder.AppendLine($"Reward Options / Boss Stage {_abilityRewardController.LastClearedBossStage}");
        for (int i = 0; i < _abilityRewardController.PendingOptions.Count; i++)
        {
            PlayerAbilityModule module = _abilityRewardController.PendingOptions[i];
            string optionName = module != null ? module.DisplayName : "Empty";
            builder.AppendLine($"- [F{6 + i}] {optionName}");
        }
    }

    private void AppendEquippedPassiveSkills(StringBuilder builder)
    {
        bool hasPassive = false;
        foreach (PlayerAbilityModule module in _abilityInventory.EquippedModules)
        {
            if (module == null || module.IsActive)
            {
                continue;
            }

            hasPassive = true;
            builder.AppendLine($"- {module.DisplayName} : Passive");
        }

        if (!hasPassive && _abilityInventory.ActiveSlots.Count == 0)
        {
            builder.AppendLine("- None");
        }
    }

    private void AppendActiveSkillCooldowns(StringBuilder builder)
    {
        float currentTime = Runner != null ? Runner.SimulationTime : Time.time;
        for (int i = 0; i < _abilityInventory.ActiveSlots.Count; i++)
        {
            PlayerAbilitySlot slot = _abilityInventory.ActiveSlots[i];
            PlayerAbilityModule module = slot?.Module;
            if (module == null)
            {
                continue;
            }

            float remaining = Mathf.Max(0f, slot.NextReadyTime - currentTime);
            string cooldownText = remaining > 0f ? $"{remaining:0.0}s" : "Ready";
            builder.AppendLine($"- [{slot.KeyCode}] {module.DisplayName} : {cooldownText}");
        }
    }
}
