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
    private static readonly int IdleState = Animator.StringToHash("idle1");
    private static readonly int WalkState = Animator.StringToHash("walk1");
    private static readonly int RunState = Animator.StringToHash("run1");
    private static readonly int Slash2State = Animator.StringToHash("slash2");
    private static readonly int Slash3State = Animator.StringToHash("slash3");
    private static readonly int Slash4State = Animator.StringToHash("slash4");

    private const byte ActionNone = 0;
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
    [SerializeField] private float comboInputBufferSeconds = 0.2f;

    [Header("Combat")]
    // 기본 공격 판정 구체의 위치와 크기. Gizmo도 이 값을 사용한다.
    [SerializeField] private float attackHitRadius = 1.4f;
    [SerializeField] private float attackHitDistance = 1.8f;
    [SerializeField] private float attackHitHeight = 1.1f;
    [SerializeField] private LayerMask attackTargetLayers = ~0;
    [SerializeField] private float basicAttackRevivePower = 34f;

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
    [Networked] private int LastActionId { get; set; }
    [Networked] private int LastConsumedActionId { get; set; }
    [Networked] private int ActionSequence { get; set; }
    [Networked] private NetworkBool BasicAttackComboUnlocked { get; set; }
    [Networked] private byte BasicAttackComboIndex { get; set; }
    [Networked] private float BasicAttackComboExpiresAt { get; set; }
    [Networked] private NetworkBool ActionAnimationLocked { get; set; }
    [Networked] private byte ActionLockType { get; set; }
    [Networked] private NetworkBool ComboInputWindowOpen { get; set; }

    private NetworkCharacterController _networkCharacterController;
    private PlayerStats _playerStats;
    private PlayerStatusController _statusController;
    private PlayerAbilityInventory _abilityInventory;
    private PlayerAbilityRewardController _abilityRewardController;
    private ChangeDetector _changeDetector;
    // 이동 입력이 없는 구르기에서도 마지막으로 움직이던 방향을 유지하기 위한 캐시.
    private Vector3 _lastMoveDirection = Vector3.forward;
    // 락온 대상 Transform은 로컬에만 들고, 네트워크에는 바라볼 위치와 상태만 보낸다.
    private Transform _lockOnTarget;
    private CameraManager _cameraManager;
    // NetworkCharacterController의 기본 회전 속도를 저장해 락온/구르기 처리 후 다시 기준값으로 돌린다.
    private float _networkControllerRotationSpeed;
    // 점프 직후 락온 블렌드 트리가 점프 모션을 덮어쓰지 않도록 잠깐 억제하는 시간.
    private float _suppressLockOnAnimatorUntil;
    // 공격 판정은 매번 새 배열을 만들지 않고 재사용해 GC 할당을 줄인다.
    private readonly Collider[] _attackHits = new Collider[16];
    // 한 번의 공격에 같은 보스의 여러 히트박스가 들어오면 가장 높은 배율 부위만 남긴다.
    private readonly Dictionary<NetworkBossCore, BossHitbox> _bestBossHitboxes = new Dictionary<NetworkBossCore, BossHitbox>();
    // 죽은 팀원이 여러 Collider로 겹쳐 맞아도 부활 게이지가 한 번만 오르게 막는다.
    private readonly HashSet<PlayerStats> _reviveHitPlayers = new HashSet<PlayerStats>();
    // 보상 획득 직후 네트워크 값이 오기 전에도 내 입력/디버그에서 콤보 해금 상태를 즉시 반영한다.
    private bool _localBasicAttackComboUnlocked;
    // Animator State 진입/종료는 클라이언트마다 먼저 감지될 수 있어 로컬 락을 별도로 둔다.
    private bool _localActionAnimationLocked;
    private byte _localActionLockType;
    private bool _localComboInputWindowOpen;
    // buffer는 아직 입력창이 열리기 전의 짧은 선입력, queue는 다음 공격으로 확정된 선입력이다.
    private bool _bufferedComboAttack;
    private int _bufferedComboActionId;
    private float _bufferedComboExpiresAt;
    private bool _queuedComboAttack;
    private int _queuedComboActionId;
    private int _lastLocalConsumedActionId;
    private bool _showPlayerDebug;

    public bool IsLockOnActive => IsLockOnNetworked;
    public Transform CurrentLockOnTarget => _lockOnTarget;
    public float AttackHitRadius => attackHitRadius;
    public Vector3 AttackHitLocalCenter => Vector3.up * attackHitHeight + Vector3.forward * attackHitDistance;
    public bool IsBasicAttackComboUnlocked => BasicAttackComboUnlocked || _localBasicAttackComboUnlocked;
    public bool IsActionAnimationLocked => ActionAnimationLocked || _localActionAnimationLocked;
    public bool IsDamageOrDeathActionActive =>
        (_playerStats != null && _playerStats.IsDead) ||
        (IsActionAnimationLocked && IsDamageOrDeathAction(LastAction));

    private void Awake()
    {
        // 같은 오브젝트에 붙은 필수 컴포넌트를 잡고, 없으면 컨트롤러를 비활성화한다.
        animator ??= GetComponent<Animator>();
        _networkCharacterController = GetComponent<NetworkCharacterController>();
        _playerStats = GetComponent<PlayerStats>();
        _statusController = GetComponent<PlayerStatusController>();
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
        ResetLocalActionState();
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

    private void ResetLocalActionState()
    {
        _localActionAnimationLocked = false;
        _localActionLockType = (byte)PlayerActionLockType.None;
        _localComboInputWindowOpen = false;
        ClearComboRequests();
        _lastLocalConsumedActionId = 0;
        if (animator != null)
        {
            ResetActionTriggers();
            animator.SetBool(IsMoving, false);
            animator.SetBool(IsRunning, false);
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
            ActionAnimationLocked = false;
            ActionLockType = (byte)PlayerActionLockType.None;
            ComboInputWindowOpen = false;
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
        bool rawAttackPressed = data.buttons.IsSet(NetworkInputData.MOUSEBUTTON0);
        bool rawParryPressed = data.buttons.IsSet(NetworkInputData.MOUSEBUTTON1);
        bool rawJumpPressed = data.buttons.IsSet(NetworkInputData.JUMP);
        bool hasActionInput = rawAttackPressed || rawParryPressed || rawJumpPressed;
        // Fusion 입력은 누르고 있는 동안 여러 틱에서 반복 전달될 수 있다.
        // actionId를 한 번만 소비해서 "한 번 누른 입력"이 공격/점프/패링을 중복 실행하지 않게 한다.
        bool canUseActionInput = hasActionInput && TryConsumeInputAction(data.actionId);
        bool jumpPressed = canUseActionInput && rawJumpPressed;
        bool attackPressed = canUseActionInput && !rawJumpPressed && rawAttackPressed;
        bool parryPressed = canUseActionInput && !rawJumpPressed && !rawAttackPressed && rawParryPressed;

        if (rawJumpPressed)
        {
            // 점프 입력은 공격 콤보보다 우선도가 높다.
            // 공격 선입력이 남아 있으면 점프 직후 공격이 예약 실행될 수 있으므로 즉시 비운다.
            ClearComboRequests();
        }

        // 매 틱마다 오래된 선입력을 정리하고, Animator가 입력 가능 구간을 열었으면 큐로 승격한다.
        // 이 순서를 먼저 처리해야 "이전 틱에 눌러둔 공격"이 현재 틱에서 자연스럽게 이어진다.
        PruneExpiredBufferedComboAttack(isActing);
        TryPromoteBufferedComboAttack(isActing);

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
            // 공격 중 입력이면 우선 "즉시 큐"를 시도한다.
            // 아직 Animator가 입력 가능 구간을 열지 않았지만 끝 0.2초 안이라면 buffer에 보관한다.
            if (!TryQueueBasicAttackCombo(isActing, data.actionId))
            {
                TryBufferBasicAttackCombo(isActing, data.actionId);
            }
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
            // 액션 입력은 서버 권한에서만 확정한다.
            // 비호스트 클라이언트는 입력만 보내고, 애니메이션은 ActionSequence 수신 후 재생한다.
            if (jumpPressed && _networkCharacterController.Grounded)
            {
                _networkCharacterController.Jump(false, jumpImpulse);
                StartAction(ActionJump, data.actionId);
                isActing = true;
                isBusy = true;
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
                StartAction(ActionParry, data.actionId);
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

        currentSpeed *= GetMoveSpeedMultiplier();

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
        ApplyMovement(moveDirection, crawlSpeed * GetMoveSpeedMultiplier(), Vector3.zero);
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
            // 선택된 대상이 없으면 네트워크 락온 상태도 꺼서 Animator가 일반 이동으로 돌아간다.
            IsLockOnNetworked = false;
            LockOnMoveNetworked = LockMoveIdle;
            return;
        }

        if (!_lockOnTarget.gameObject.activeInHierarchy)
        {
            // 보스나 락온 포인트가 비활성화되면 이전 Transform을 계속 바라보지 않도록 정리한다.
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
               CanStartBasicAttackFollowUp() &&
               Runner.SimulationTime <= BasicAttackComboExpiresAt;
    }

    private bool TryQueueBasicAttackCombo(bool isActing, int actionId)
    {
        // actionId가 0이면 입력 이벤트가 아니라 유지 입력에 가까우므로 선입력으로 저장하지 않는다.
        // 같은 클릭이 두 번 소비되는 것을 막기 위해 TryConsumeInputAction에서 받은 고유 id만 큐에 넣는다.
        if (actionId == 0 || !CanQueueBasicAttackCombo(isActing))
        {
            return false;
        }

        // queue가 잡히면 buffer는 더 이상 필요 없다.
        // 하나의 입력이 buffer와 queue 양쪽에 남아 있으면 다음 공격이 중복으로 나갈 수 있다.
        _queuedComboAttack = true;
        _queuedComboActionId = actionId;
        ClearBufferedComboAttack();
        return true;
    }

    private bool TryBufferBasicAttackCombo(bool isActing, int actionId)
    {
        // buffer는 "입력 가능 창은 아직 닫혀 있지만, 공격 종료 0.2초 전"에 눌린 입력을 짧게 보관한다.
        // Animator StateBehaviour가 OpenComboInputWindow를 호출하면 TryPromoteBufferedComboAttack에서 queue로 승격된다.
        if (!CanBufferBasicAttackCombo(isActing) || actionId == 0)
        {
            return false;
        }

        _bufferedComboAttack = true;
        _bufferedComboActionId = actionId;
        _bufferedComboExpiresAt = GetSimulationTime() + Mathf.Max(0.01f, comboInputBufferSeconds);
        return true;
    }

    private bool CanBufferBasicAttackCombo(bool isActing)
    {
        // 미해금 기본 공격도 slash2 반복 선입력이 필요하므로 콤보 해금 여부로 막지 않는다.
        // 단, 너무 이른 입력은 무시하고 현재 공격의 마지막 comboInputBufferSeconds 구간만 허용한다.
        return isActing &&
               LastAction == ActionAttack &&
               CanStartBasicAttackFollowUp() &&
               !_queuedComboAttack &&
               !IsComboInputWindowOpen() &&
               IsInBasicAttackComboBufferWindow();
    }

    private void TryPromoteBufferedComboAttack(bool isActing)
    {
        if (!_bufferedComboAttack)
        {
            return;
        }

        if (IsComboInputWindowOpen())
        {
            // Animator가 입력 창을 열었으면 buffer에 있던 클릭을 queue로 옮긴다.
            // 이 시점에도 조건이 안 맞으면 오래된 입력이므로 버린다.
            if (!TryQueueBasicAttackCombo(isActing, _bufferedComboActionId))
            {
                ClearBufferedComboAttack();
            }

            return;
        }

        if (!CanKeepBufferedBasicAttackCombo(isActing))
        {
            ClearBufferedComboAttack();
        }
    }

    private bool CanKeepBufferedBasicAttackCombo(bool isActing)
    {
        // 아직 입력 창이 열리지 않았더라도, 현재 공격이 계속 재생 중이면 buffer를 유지한다.
        // 피격/구르기/점프 등으로 LastAction이 바뀌면 더 이상 후속 기본공격으로 쓰면 안 된다.
        return isActing &&
               LastAction == ActionAttack &&
               CanStartBasicAttackFollowUp() &&
               !_queuedComboAttack;
    }

    private bool CanStartBasicAttackFollowUp()
    {
        // 콤보가 해금되지 않은 상태에서도 기본 공격 자체는 다음 기본 공격으로 선입력될 수 있어야 한다.
        // 해금 전에는 StartBasicAttack에서 항상 slash2로 고정되고, 해금 후에만 slash3/slash4 단계 제한을 적용한다.
        return !IsBasicAttackComboUnlocked || BasicAttackComboIndex < BasicAttackComboLastIndex;
    }

    private void PruneExpiredBufferedComboAttack(bool isActing)
    {
        if (!_bufferedComboAttack || GetSimulationTime() <= _bufferedComboExpiresAt)
        {
            return;
        }

        // 만료 시간이 지나도 현재 공격이 아직 유효하면 유지한다.
        // 서버 틱/Animator 업데이트 타이밍 차이로 OpenComboInputWindow가 한 틱 늦게 올 수 있기 때문이다.
        if (CanKeepBufferedBasicAttackCombo(isActing))
        {
            return;
        }

        ClearBufferedComboAttack();
    }

    private void ClearComboRequests()
    {
        // 공격 흐름이 끊기면 buffer와 queue를 모두 비운다.
        // 하나만 남기면 이전 클릭이 다음 액션 뒤에 늦게 실행될 수 있다.
        ClearBufferedComboAttack();
        _queuedComboAttack = false;
        _queuedComboActionId = 0;
    }

    private void ClearBufferedComboAttack()
    {
        _bufferedComboAttack = false;
        _bufferedComboActionId = 0;
        _bufferedComboExpiresAt = 0f;
    }

    private byte GetOpeningBasicAttackComboIndex()
    {
        // idle 상태에서 새 공격을 시작할 때, 직전 공격 종료 grace 안이면 다음 콤보 단계로 시작한다.
        // 콤보가 해금되지 않았거나 grace가 끝났으면 항상 첫 기본공격(slash2)부터 시작한다.
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

    private float GetSimulationTime()
    {
        // 네트워크 러너가 있으면 시뮬레이션 시간을 기준으로 사용해 호스트/클라이언트 판단을 맞춘다.
        // 에디터 단독 테스트처럼 Runner가 없는 경우만 Unity Time으로 대체한다.
        return Runner != null ? Runner.SimulationTime : Time.time;
    }

    private bool IsInBasicAttackComboBufferWindow()
    {
        if (animator == null)
        {
            return false;
        }

        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
        if (!IsCurrentBasicAttackState(stateInfo))
        {
            return false;
        }

        // 현재 공격 클립의 남은 시간이 설정값 이하일 때만 선입력 buffer를 허용한다.
        return GetRemainingStateSeconds(stateInfo) <= comboInputBufferSeconds;
    }

    private bool IsCurrentBasicAttackState(AnimatorStateInfo stateInfo)
    {
        return stateInfo.shortNameHash == Slash2State ||
               stateInfo.shortNameHash == Slash3State ||
               stateInfo.shortNameHash == Slash4State;
    }

    private static float GetRemainingStateSeconds(AnimatorStateInfo stateInfo)
    {
        // loop 상태는 normalizedTime이 계속 증가하므로 소수부만 사용한다.
        float normalizedTime = stateInfo.loop
            ? stateInfo.normalizedTime - Mathf.Floor(stateInfo.normalizedTime)
            : Mathf.Clamp01(stateInfo.normalizedTime);

        // 클립 길이가 비정상적으로 0이면 계산 안정성을 위해 아주 작은 값으로 보정한다.
        float stateLength = Mathf.Max(0.01f, stateInfo.length);
        return Mathf.Max(0f, (1f - normalizedTime) * stateLength);
    }

    private bool TrySpendBasicAttackStamina()
    {
        // PlayerStats가 없으면 테스트 오브젝트로 보고 공격을 허용한다.
        // 실제 플레이어에서는 PlayerStats가 스태미나 차감 성공 여부를 돌려준다.
        return _playerStats == null || _playerStats.TryUseStamina(_playerStats.AttackStaminaCost);
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
        TryPromoteBufferedComboAttack(true);
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
            if (BasicAttackComboIndex >= BasicAttackComboLastIndex)
            {
                BasicAttackComboExpiresAt = Runner != null ? Runner.SimulationTime : Time.time;
                ClearComboRequests();
                return;
            }

            BasicAttackComboExpiresAt = Runner != null
                ? Runner.SimulationTime + Mathf.Max(0f, comboGraceSeconds)
                : Time.time + Mathf.Max(0f, comboGraceSeconds);
            return;
        }

        ClearComboRequests();
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

        // EndActionAnimation의 grace 계산과 CanStartQueuedComboAttack의 만료 검사에서 쓰는 기준 시간이다.
        // 공격이 실제로 서버에서 확정된 순간을 기록해야 클라이언트별 프레임 차이에 덜 흔들린다.
        BasicAttackComboExpiresAt = Runner != null ? Runner.SimulationTime : Time.time;
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

        LastAction = becameDead
            ? ActionDeath
            : IsParryActive()
                ? ActionParryImpact
                : ActionImpact;
        LastActionId = 0;
        ActionSequence++;
        SetActionAnimationLocked(true, GetActionLockType(LastAction));
        SetComboInputWindowOpen(false);
        ClearComboRequests();
    }

    public void NotifyRevived()
    {
        _localActionAnimationLocked = false;
        _localActionLockType = (byte)PlayerActionLockType.None;
        _localComboInputWindowOpen = false;
        ClearComboRequests();
        _lastLocalConsumedActionId = 0;

        if (HasStateAuthority)
        {
            LastAction = ActionNone;
            LastActionId = 0;
            LastConsumedActionId = 0;
            BasicAttackComboIndex = 0;
            BasicAttackComboExpiresAt = Runner != null ? Runner.SimulationTime : Time.time;
            ActionAnimationLocked = false;
            ActionLockType = (byte)PlayerActionLockType.None;
            ComboInputWindowOpen = false;
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
                // 같은 보스 안에서는 머리/몸통처럼 여러 부위가 동시에 잡힐 수 있다.
                // 배율이 가장 높은 부위 하나만 남겨 한 공격이 같은 보스를 여러 번 때리지 않게 한다.
                _bestBossHitboxes[boss] = bossHitbox;
            }
        }

        foreach (BossHitbox bossHitbox in _bestBossHitboxes.Values)
        {
            // 최종적으로 보스별 대표 히트박스 하나에만 데미지를 전달한다.
            bossHitbox.OnHitByPlayer(damage, Object);
        }
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

    private float GetMoveSpeedMultiplier()
    {
        return _statusController != null ? _statusController.GetMoveSpeedMultiplier() : 1f;
    }

    private float GetOutgoingDamageMultiplier()
    {
        return _statusController != null ? _statusController.GetOutgoingDamageMultiplier() : 1f;
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
        animator.ResetTrigger(Impact);
        animator.ResetTrigger(Impact2);
        animator.ResetTrigger(Death);
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
        // 플레이 중 네트워크 액션/Animator 상태를 한 화면에서 보기 위한 임시 디버그 문자열이다.
        // 공격 중복, 선입력 큐, 액션락 문제를 빠르게 확인하는 데 사용한다.
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
        AppendNetworkActionDebug(builder);

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

    private void AppendNetworkActionDebug(StringBuilder builder)
    {
        builder.AppendLine("Network / Action");
        builder.AppendLine($"Authority : state={HasStateAuthority}, input={Object.HasInputAuthority}, forward={(Runner != null && Runner.IsForward)}");
        builder.AppendLine($"Action : {GetActionName(LastAction)} / id={LastActionId} / seq={ActionSequence}");
        builder.AppendLine($"Consumed : net={LastConsumedActionId}, local={_lastLocalConsumedActionId}");
        builder.AppendLine($"Lock : net={ActionAnimationLocked}({(PlayerActionLockType)ActionLockType}), local={_localActionAnimationLocked}({(PlayerActionLockType)_localActionLockType})");
        builder.AppendLine($"Combo : index={BasicAttackComboIndex}, window={ComboInputWindowOpen || _localComboInputWindowOpen}, queued={_queuedComboAttack}:{_queuedComboActionId}");

        if (animator == null)
        {
            builder.AppendLine("Animator : missing");
            return;
        }

        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
        builder.AppendLine($"Animator : {GetAnimatorStateName(stateInfo.shortNameHash)} / t={stateInfo.normalizedTime:0.00} / actionTag={stateInfo.IsTag("Action")}");
        builder.AppendLine($"Move Params : moving={animator.GetBool(IsMoving)}, running={animator.GetBool(IsRunning)}, lockOn={animator.GetBool(IsLockOn)}");
    }

    private static string GetActionName(byte actionType)
    {
        return actionType switch
        {
            ActionAttack => "Attack",
            ActionParry => "Parry",
            ActionRoll => "Roll",
            ActionJump => "Jump",
            ActionImpact => "Impact",
            ActionParryImpact => "ParryImpact",
            ActionDeath => "Death",
            _ => "None"
        };
    }

    private static bool IsDamageOrDeathAction(byte actionType)
    {
        // 피격/사망 반응은 스킬 시전보다 우선순위가 높은 연출 이벤트다.
        // 늦게 도착한 스킬 RPC가 이 애니메이션을 덮어쓰지 못하도록 구분한다.
        return actionType == ActionImpact ||
               actionType == ActionParryImpact ||
               actionType == ActionDeath;
    }

    private static string GetAnimatorStateName(int shortNameHash)
    {
        if (shortNameHash == IdleState) return "idle1";
        if (shortNameHash == WalkState) return "walk1";
        if (shortNameHash == RunState) return "run1";
        if (shortNameHash == Slash2State) return "slash2";
        if (shortNameHash == Slash3State) return "slash3";
        if (shortNameHash == Slash4State) return "slash4";
        return shortNameHash.ToString();
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
