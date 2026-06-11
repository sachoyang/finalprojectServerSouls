using System.Collections.Generic;
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
public partial class NetworkPlayerController : NetworkBehaviour
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
    [Networked] private int ControlLockMask { get; set; }

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
    private int _localControlLockMask;
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


















































































}
