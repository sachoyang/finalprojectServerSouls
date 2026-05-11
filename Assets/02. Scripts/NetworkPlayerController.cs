using System.Collections.Generic;
using Fusion;
using UnityEngine;

[RequireComponent(typeof(NetworkCharacterController))]
public class NetworkPlayerController : NetworkBehaviour
{
    private static readonly int IsMoving = Animator.StringToHash("IsMoving");
    private static readonly int IsRunning = Animator.StringToHash("IsRunning");
    private static readonly int Attack = Animator.StringToHash("Attack");
    private static readonly int Parry = Animator.StringToHash("Parry");
    private static readonly int Roll = Animator.StringToHash("Roll");
    private static readonly int Jump = Animator.StringToHash("Jump");

    private const byte ActionAttack = 1;
    private const byte ActionParry = 2;
    private const byte ActionRoll = 3;
    private const byte ActionJump = 4;

    private const byte LockMoveIdle = 0;
    private const byte LockMoveForward = 1;
    private const byte LockMoveBack = 2;
    private const byte LockMoveLeft = 3;
    private const byte LockMoveRight = 4;
    private const byte LockMoveRunLeft = 5;
    private const byte LockMoveRunRight = 6;

    private const string LockOnHeadTag = "LockOnHead";
    private const string LockOnBodyTag = "LockOnBody";
    private const string LockForwardState = "Great Sword Walk";
    private const string LockBackState = "Great Sword Walk2";
    private const string LockLeftState = "Great Sword Strafe";
    private const string LockRightState = "Great Sword Strafe2";
    private const string LockRunLeftState = "Great Sword Strafe3";
    private const string LockRunRightState = "Great Sword Strafe4";

    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private Camera viewCamera;

    [Header("Movement")]
    [SerializeField] private float walkSpeed = 2.6f;
    [SerializeField] private float runSpeed = 4.8f;
    [SerializeField] private float rollSpeed = 6.5f;
    [SerializeField] private float rotationSpeed = 720f;
    [SerializeField] private float rollDuration = 0.9f;
    [SerializeField] private float shiftHoldThreshold = 0.25f;
    [SerializeField] private float movementAcceleration = 80f;
    [SerializeField] private float movementBraking = 30f;

    [Header("Action Locks")]
    [SerializeField] private float attackLockDuration = 0.65f;
    [SerializeField] private float parryLockDuration = 0.5f;
    [SerializeField] private float jumpImpulse = 8f;

    [Header("Lock On")]
    [SerializeField] private float lockOnSearchRadius = 80f;
    [SerializeField] private float lockOnRotationSpeed = 900f;

    [Networked] private bool IsMovingNetworked { get; set; }
    [Networked] private bool IsRunningNetworked { get; set; }
    [Networked] private bool IsLockOnNetworked { get; set; }
    [Networked] private byte LockOnMoveNetworked { get; set; }
    [Networked] private Vector3 LockOnPointPosition { get; set; }
    [Networked] private bool WasShiftHeld { get; set; }
    [Networked] private float ShiftHoldTime { get; set; }
    [Networked] private TickTimer RollTimer { get; set; }
    [Networked] private TickTimer ActionTimer { get; set; }
    [Networked] private Vector3 RollDirection { get; set; }
    [Networked] private byte LastAction { get; set; }
    [Networked] private int ActionSequence { get; set; }

    private NetworkCharacterController _networkCharacterController;
    private PlayerStats _playerStats;
    private ChangeDetector _changeDetector;
    private Vector3 _lastMoveDirection = Vector3.forward;
    private Transform _lockOnTarget;
    private Transform _lockOnBossRoot;
    private int _lockOnIndex = -1;
    private ThirdPersonCameraController _thirdPersonCamera;
    private string _lastLockOnAnimation;
    private float _networkControllerRotationSpeed;

    public bool IsLockOnActive => IsLockOnNetworked;
    public Transform CurrentLockOnTarget => _lockOnTarget;

    private void Awake()
    {
        animator ??= GetComponent<Animator>();
        _networkCharacterController = GetComponent<NetworkCharacterController>();
        _playerStats = GetComponent<PlayerStats>();
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

    public override void Spawned()
    {
        _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);

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
        _thirdPersonCamera = mainCamera.GetComponent<ThirdPersonCameraController>();
        if (_thirdPersonCamera != null)
        {
            _thirdPersonCamera.SetTarget(transform);
        }
        else
        {
            CameraFollow follow = mainCamera.GetComponent<CameraFollow>();
            if (follow == null)
            {
                follow = mainCamera.gameObject.AddComponent<CameraFollow>();
            }

            follow.target = transform;
        }

        CameraFollow existingFollow = mainCamera.GetComponent<CameraFollow>();
        if (existingFollow != null && _thirdPersonCamera != null)
        {
            existingFollow.enabled = false;
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (_playerStats != null && _playerStats.IsDead)
        {
            ClearLockOn();
            ApplyMovement(Vector3.zero, walkSpeed, Vector3.zero);
            UpdateMovementState(false, false, LockMoveIdle);
            WasShiftHeld = false;
            ShiftHoldTime = 0f;
            return;
        }

        if (!GetInput(out NetworkInputData data))
        {
            ApplyMovement(Vector3.zero, walkSpeed, GetLockOnFacingDirection());
            UpdateMovementState(false, false, LockMoveIdle);
            WasShiftHeld = false;
            ShiftHoldTime = 0f;
            return;
        }

        ProcessLockOnInput(data);

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
        bool isActing = !ActionTimer.ExpiredOrNotRunning(Runner);
        bool isBusy = isRolling || isActing;

        if (desiredMove.sqrMagnitude > 0.001f)
        {
            _lastMoveDirection = desiredMove.normalized;
        }

        if (!isBusy)
        {
            if (data.buttons.IsSet(NetworkInputData.JUMP) && _networkCharacterController.Grounded)
            {
                _networkCharacterController.Jump(false, jumpImpulse);
                LastAction = ActionJump;
                ActionSequence++;
            }
            else if (data.buttons.IsSet(NetworkInputData.MOUSEBUTTON0))
            {
                if (_playerStats == null || _playerStats.TryUseStamina(_playerStats.AttackStaminaCost))
                {
                    StartAction(ActionAttack, attackLockDuration);
                    isActing = true;
                    isBusy = true;
                }
            }
            else if (data.buttons.IsSet(NetworkInputData.MOUSEBUTTON1))
            {
                StartAction(ActionParry, parryLockDuration);
                isActing = true;
                isBusy = true;
            }
            else if (shiftReleased && ShiftHoldTime < shiftHoldThreshold)
            {
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
                         !isBusy &&
                         (_playerStats == null || _playerStats.HasStamina(runStaminaCost));

        float currentSpeed = walkSpeed;
        Vector3 moveDirection = Vector3.zero;
        Vector3 facingDirection = IsLockOnNetworked ? GetLockOnFacingDirection() : Vector3.zero;

        if (isRolling)
        {
            currentSpeed = rollSpeed;
            moveDirection = RollDirection;
            facingDirection = Vector3.zero;
        }
        else if (!isActing && desiredMove.sqrMagnitude > 0.001f)
        {
            currentSpeed = shouldRun ? runSpeed : walkSpeed;
            moveDirection = desiredMove.normalized;
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
        if (animator == null)
        {
            return;
        }

        foreach (string change in _changeDetector.DetectChanges(this))
        {
            if (change == nameof(ActionSequence))
            {
                animator.ResetTrigger(Attack);
                animator.ResetTrigger(Parry);
                animator.ResetTrigger(Roll);
                animator.ResetTrigger(Jump);
                TriggerAction(LastAction);
            }
        }

        if (Object.HasInputAuthority && _thirdPersonCamera != null)
        {
            if (IsLockOnNetworked && _lockOnTarget != null)
            {
                _thirdPersonCamera.SetLockOnTarget(_lockOnTarget);
            }
            else
            {
                _thirdPersonCamera.ClearLockOnTarget();
            }
        }

        bool lockOnMovement = IsLockOnNetworked && !IsInActionAnimation();
        animator.SetBool(IsMoving, lockOnMovement ? false : IsMovingNetworked);
        animator.SetBool(IsRunning, lockOnMovement ? false : IsRunningNetworked);

        if (lockOnMovement)
        {
            PlayLockOnAnimation(LockOnMoveNetworked);
        }
        else
        {
            _lastLockOnAnimation = null;
        }
    }

    private void ProcessLockOnInput(NetworkInputData data)
    {
        if (data.buttons.IsSet(NetworkInputData.LOCKON_CANCEL))
        {
            ClearLockOn();
            return;
        }

        if (data.buttons.IsSet(NetworkInputData.LOCKON))
        {
            CycleNearestBossLockOnPoint();
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

    private void CycleNearestBossLockOnPoint()
    {
        List<Transform> points = GetNearestBossLockOnPoints();
        if (points.Count == 0)
        {
            ClearLockOn();
            return;
        }

        Transform bossRoot = GetBossRoot(points[0]);
        if (_lockOnBossRoot != bossRoot)
        {
            _lockOnIndex = -1;
        }

        if (_lockOnTarget != null)
        {
            int currentIndex = points.IndexOf(_lockOnTarget);
            if (currentIndex >= 0)
            {
                _lockOnIndex = currentIndex;
            }
        }

        _lockOnBossRoot = bossRoot;
        _lockOnIndex = (_lockOnIndex + 1) % points.Count;
        _lockOnTarget = points[_lockOnIndex];
        LockOnPointPosition = _lockOnTarget.position;
        IsLockOnNetworked = true;

        if (Object.HasInputAuthority && _thirdPersonCamera != null)
        {
            _thirdPersonCamera.SetLockOnTarget(_lockOnTarget);
        }
    }

    private List<Transform> GetNearestBossLockOnPoints()
    {
        GameObject[] heads = GameObject.FindGameObjectsWithTag(LockOnHeadTag);
        GameObject[] bodies = GameObject.FindGameObjectsWithTag(LockOnBodyTag);
        var nearestPoints = new List<Transform>();
        Transform nearestRoot = null;
        float nearestDistance = float.MaxValue;

        EvaluateNearestRoot(heads, ref nearestRoot, ref nearestDistance);
        EvaluateNearestRoot(bodies, ref nearestRoot, ref nearestDistance);

        if (nearestRoot == null || nearestDistance > lockOnSearchRadius * lockOnSearchRadius)
        {
            return nearestPoints;
        }

        AddRootLockOnPoints(heads, nearestRoot, nearestPoints);
        AddRootLockOnPoints(bodies, nearestRoot, nearestPoints);
        nearestPoints.Sort(CompareLockOnPoint);
        return nearestPoints;
    }

    private void EvaluateNearestRoot(GameObject[] candidates, ref Transform nearestRoot, ref float nearestDistance)
    {
        foreach (GameObject candidate in candidates)
        {
            if (candidate == null || !candidate.activeInHierarchy)
            {
                continue;
            }

            float sqrDistance = (candidate.transform.position - transform.position).sqrMagnitude;
            if (sqrDistance >= nearestDistance)
            {
                continue;
            }

            nearestRoot = GetBossRoot(candidate.transform);
            nearestDistance = sqrDistance;
        }
    }

    private void AddRootLockOnPoints(GameObject[] candidates, Transform root, List<Transform> points)
    {
        foreach (GameObject candidate in candidates)
        {
            if (candidate == null || !candidate.activeInHierarchy)
            {
                continue;
            }

            if (GetBossRoot(candidate.transform) == root)
            {
                points.Add(candidate.transform);
            }
        }
    }

    private static Transform GetBossRoot(Transform point)
    {
        DragonBoss boss = point.GetComponentInParent<DragonBoss>();
        return boss != null ? boss.transform : point.root;
    }

    private static int CompareLockOnPoint(Transform left, Transform right)
    {
        int leftPriority = left.CompareTag(LockOnHeadTag) ? 0 : 1;
        int rightPriority = right.CompareTag(LockOnHeadTag) ? 0 : 1;
        int priorityCompare = leftPriority.CompareTo(rightPriority);
        return priorityCompare != 0 ? priorityCompare : string.CompareOrdinal(left.name, right.name);
    }

    private void ClearLockOn()
    {
        _lockOnTarget = null;
        _lockOnBossRoot = null;
        _lockOnIndex = -1;
        IsLockOnNetworked = false;
        LockOnMoveNetworked = LockMoveIdle;

        if (Object.HasInputAuthority && _thirdPersonCamera != null)
        {
            _thirdPersonCamera.ClearLockOnTarget();
        }
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

    private void PlayLockOnAnimation(byte lockMove)
    {
        string stateName = lockMove switch
        {
            LockMoveForward => LockForwardState,
            LockMoveBack => LockBackState,
            LockMoveLeft => LockLeftState,
            LockMoveRight => LockRightState,
            LockMoveRunLeft => LockRunLeftState,
            LockMoveRunRight => LockRunRightState,
            _ => "idle1"
        };

        if (_lastLockOnAnimation == stateName)
        {
            return;
        }

        animator.CrossFadeInFixedTime(stateName, 0.08f);
        _lastLockOnAnimation = stateName;
    }

    private bool IsInActionAnimation()
    {
        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
        return stateInfo.IsTag("Action") && stateInfo.normalizedTime < 0.98f;
    }

    private void StartAction(byte actionType, float lockDuration)
    {
        LastAction = actionType;
        ActionSequence++;
        ActionTimer = TickTimer.CreateFromSeconds(Runner, lockDuration);
    }

    private void StartRoll(Vector3 desiredMove)
    {
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

        StartAction(ActionRoll, rollDuration);
        RollTimer = TickTimer.CreateFromSeconds(Runner, rollDuration);
    }

    private void ApplyMovement(Vector3 moveDirection, float moveSpeed, Vector3 facingDirection)
    {
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

    private void RotateTowards(Vector3 direction, float rotateSpeed)
    {
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
        switch (actionType)
        {
            case ActionAttack:
                animator.SetTrigger(Attack);
                break;
            case ActionParry:
                animator.SetTrigger(Parry);
                break;
            case ActionRoll:
                animator.SetTrigger(Roll);
                break;
            case ActionJump:
                animator.SetTrigger(Jump);
                break;
        }
    }
}
