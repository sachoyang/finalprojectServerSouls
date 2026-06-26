using System.Text;
using Fusion;
using UnityEngine;

[System.Flags]
public enum PlayerControlLockFlags
{
    None = 0,
    Movement = 1 << 0,
    Action = 1 << 1,
    Skill = 1 << 2,
    Camera = 1 << 3,
    Interaction = 1 << 4,
    All = Movement | Action | Skill | Camera | Interaction
}

[RequireComponent(typeof(NetworkCharacterController))]
public partial class NetworkPlayerController :
    NetworkBehaviour,
    IActionLockStateReceiver,
    IComboInputStateReceiver,
    IParryGuardStateReceiver,
    IInvincibilityStateReceiver,
    IRootMotionStateReceiver,
    IStaminaRecoveryStateReceiver
{
    // Animator 파라미터 이름은 매 프레임 문자열로 찾지 않도록 해시로 캐싱한다.
    private static readonly int MoveSpeed = Animator.StringToHash("MoveSpeed");
    private static readonly int Attack = Animator.StringToHash("Attack");
    private static readonly int Attack2 = Animator.StringToHash("Attack2");
    private static readonly int Attack3 = Animator.StringToHash("Attack3");
    private static readonly int Attack4 = Animator.StringToHash("Attack4");
    private static readonly int Parry = Animator.StringToHash("Parry");
    private static readonly int Roll = Animator.StringToHash("Roll");
    private static readonly int Jump = Animator.StringToHash("Jump");
    private static readonly int Jump2 = Animator.StringToHash("Jump2");
    private static readonly int Impact = Animator.StringToHash("Impact");
    private static readonly int Impact2 = Animator.StringToHash("Impact2");
    private static readonly int Death = Animator.StringToHash("Death");
    private static readonly int IsCrawling = Animator.StringToHash("IsCrawling");
    private static readonly int IsLockOn = Animator.StringToHash("IsLockOn");
    private static readonly int LockMoveX = Animator.StringToHash("LockMoveX");
    private static readonly int LockMoveY = Animator.StringToHash("LockMoveY");
    private static readonly int LockMoveSpeed = Animator.StringToHash("LockMoveSpeed");
    private static readonly int IdleState = Animator.StringToHash("idle1");
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
    private const byte ActionJumpForward = 10;

    private const byte LockMoveIdle = 0;
    private const byte LockMoveForward = 1;
    private const byte LockMoveBack = 2;
    private const byte LockMoveLeft = 3;
    private const byte LockMoveRight = 4;
    private const byte LockMoveRunLeft = 5;
    private const byte LockMoveRunRight = 6;
    private const byte LockMoveRunForward = 7;
    private const byte LockMoveRunBack = 8;
    private const string AlivePlayerTag = "Player";
    private const string DeadPlayerTag = "DeadPlayer";
    private static readonly int[] BasicAttackComboTriggers =
    {
        Attack2,
        Attack3,
        Attack4
    };
    private static byte BasicAttackComboLastIndex =>
        (byte)Mathf.Max(0, BasicAttackComboTriggers.Length - 1);

    [Header("References")]
    // 플레이어 모델 애니메이터와 로컬 플레이어가 바라볼 카메라.
    [SerializeField] private Animator animator;
    [SerializeField] private Camera viewCamera;

    [Header("Movement")]
    // 일반 이동, 달리기, 구르기, 다운 후 기어가기 속도.
    [SerializeField] private float walkSpeed = 2.6f;
    [SerializeField] private float runSpeed = 4.8f;
    [SerializeField] private float rotationSpeed = 720f;
    [SerializeField] private float crawlSpeed = 0.9f;
    [SerializeField] private float shiftHoldThreshold = 0.25f;
    [SerializeField] private float movementAcceleration = 80f;
    [SerializeField] private float movementBraking = 30f;
    [SerializeField] private float moveSpeedAcceleration = 7f;
    [SerializeField] private float moveSpeedDeceleration = 10f;
    [SerializeField] private float moveStartSpeed = 1.2f;
    [SerializeField] private float moveStopSpeed = 0.18f;
    [SerializeField, Range(0f, 0.5f)] private float minimumMoveAnimationBlend = 0.5f;

    [Header("Step Up")]
    // CharacterController가 이 높이 이하의 턱을 자동으로 올라간 뒤 만든 가짜 상승 속도를 제거한다.
    // Player.prefab의 CharacterController Step Offset(현재 0.3m)보다 약간 크게 두는 것이 안전하다.
    [SerializeField, Min(0f)] private float maximumStepUpHeight = 0.35f;
    // 바닥이나 계단의 미세한 높이 변화까지 판정할 수 있도록 사용하는 최소 상승량이다.
    [SerializeField, Min(0f)] private float minimumStepUpHeight = 0.01f;

    [Header("Action Locks")]
    [SerializeField, Range(0.5f, 1f)]
    private float activeAnimatorStateEndThreshold = 0.98f;

    // 점프 높이와 전체 체공 시간을 기준으로 초기 속도와 중력을 계산해 애니메이션 타이밍에 맞춘다.
    [SerializeField] private float jumpHeight = 1.15f;
    [SerializeField] private float jumpAirTime = 0.68f;
    [SerializeField] private float forwardJumpHeight = 1.15f;
    [SerializeField] private float forwardJumpAirTime = 0.68f;
    [SerializeField, Range(0.5f, 1f)] private float runJumpSpeedRatio = 0.75f;
    [SerializeField] private bool useForwardJumpRootMotion = false;

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
    [Networked] private Vector3 RollDirection { get; set; }
    [Networked] private byte LastAction { get; set; }
    [Networked] private int LastActionId { get; set; }
    [Networked] private int LastConsumedActionId { get; set; }
    [Networked] private int ActionSequence { get; set; }
    [Networked] private NetworkBool BasicAttackComboUnlocked { get; set; }
    [Networked] private byte BasicAttackComboIndex { get; set; }
    [Networked] private NetworkBool ActionAnimationLocked { get; set; }
    [Networked] private byte ActionLockType { get; set; }
    [Networked] private NetworkBool ComboInputWindowOpen { get; set; }
    [Networked] private NetworkBool ParryGuardActive { get; set; }
    [Networked] private int ControlLockMask { get; set; }
    [Networked] private float CurrentMoveSpeed { get; set; }
    [Networked] private float MoveSpeedBlendNetworked { get; set; }
    [Networked] private Vector3 ForwardJumpDirection { get; set; }

    private NetworkCharacterController _networkCharacterController;
    private CharacterController _characterController;
    private PlayerStats _playerStats;
    private PlayerStatusController _statusController;
    private PlayerAbilityInventory _abilityInventory;
    private CombatSystem _combatSystem;
    private ChangeDetector _changeDetector;
    // 이동 입력이 없는 구르기에서도 마지막으로 움직이던 방향을 유지하기 위한 캐시.
    private Vector3 _lastMoveDirection = Vector3.forward;
    // 락온 대상 Transform은 로컬에만 들고, 네트워크에는 바라볼 위치와 상태만 보낸다.
    private Transform _lockOnTarget;
    private CameraManager _cameraManager;
    // NetworkCharacterController의 기본 회전 속도를 저장해 락온/구르기 처리 후 다시 기준값으로 돌린다.
    private float _networkControllerRotationSpeed;
    private float _networkControllerGravity;
    private Vector3 _queuedRootMotionDeltaPosition;
    private Quaternion _queuedRootMotionDeltaRotation = Quaternion.identity;
    private bool _hasQueuedRootMotion;
    // 보상 획득 직후 네트워크 값이 오기 전에도 내 입력/디버그에서 콤보 해금 상태를 즉시 반영한다.
    private bool _localBasicAttackComboUnlocked;
    // Animator State 진입/종료는 클라이언트마다 먼저 감지될 수 있어 로컬 락을 별도로 둔다.
    private bool _localActionAnimationLocked;
    private byte _localActionLockType;
    private bool _localComboInputWindowOpen;
    private bool _localParryGuardActive;
    private int _parryGuardStateDepth;
    private int _invincibilityStateDepth;
    private bool _animatorStateRootMotionActive;
    private int _localControlLockMask;
    // queue는 Animator StateBehaviour가 연 입력 가능 구간에서 다음 공격으로 확정된 선입력이다.
    private bool _queuedComboAttack;
    private int _queuedComboActionId;
    private int _lastLocalConsumedActionId;
    private bool _showPlayerDebug;

    public bool IsLockOnActive => IsLockOnNetworked;
    public Transform CurrentLockOnTarget => _lockOnTarget;
    public float AttackHitRadius => _combatSystem != null ? _combatSystem.BasicAttackHitRadius : 0f;
    public Vector3 AttackHitLocalCenter => _combatSystem != null ? _combatSystem.BasicAttackHitLocalCenter : Vector3.zero;
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
        _characterController = GetComponent<CharacterController>();
        _playerStats = GetComponent<PlayerStats>();
        _statusController = GetComponent<PlayerStatusController>();
        _abilityInventory = GetComponent<PlayerAbilityInventory>();
        _combatSystem = FindFirstObjectByType<CombatSystem>();
        lockOnTargetSelector ??= GetComponent<LockOnTargetSelector>();
        if (lockOnTargetSelector == null)
        {
            lockOnTargetSelector = gameObject.AddComponent<LockOnTargetSelector>();
        }
        lockOnTargetSelector.SetSearchRadius(lockOnSearchRadius);
        viewCamera ??= Camera.main;
        _networkControllerRotationSpeed = _networkCharacterController != null ? _networkCharacterController.rotationSpeed : 0f;
        _networkControllerGravity = _networkCharacterController != null ? _networkCharacterController.gravity : -20f;

        if (animator == null || _networkCharacterController == null)
        {
            enabled = false;
            return;
        }

        animator.applyRootMotion = false;
        _lastMoveDirection = transform.forward;
    }


















































































}
