using Fusion;
using UnityEngine;

[RequireComponent(typeof(NetworkCharacterController))]
public class NetworkPlayerController : Player
{
    private static readonly int IsMoving = Animator.StringToHash("IsMoving");
    private static readonly int IsRunning = Animator.StringToHash("IsRunning");
    private static readonly int Attack = Animator.StringToHash("Attack");
    private static readonly int Parry = Animator.StringToHash("Parry");
    private static readonly int Roll = Animator.StringToHash("Roll");

    private const byte ActionAttack = 1;
    private const byte ActionParry = 2;
    private const byte ActionRoll = 3;

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

    [Networked] private bool IsMovingNetworked { get; set; }
    [Networked] private bool IsRunningNetworked { get; set; }
    [Networked] private bool WasShiftHeld { get; set; }
    [Networked] private float ShiftHoldTime { get; set; }
    [Networked] private TickTimer RollTimer { get; set; }
    [Networked] private TickTimer ActionTimer { get; set; }
    [Networked] private Vector3 RollDirection { get; set; }
    [Networked] private byte LastAction { get; set; }
    [Networked] private int ActionSequence { get; set; }

    private NetworkCharacterController _networkCharacterController;
    private ChangeDetector _changeDetector;
    private Vector3 _lastMoveDirection = Vector3.forward;

    private void Awake()
    {
        animator ??= GetComponent<Animator>();
        _networkCharacterController = GetComponent<NetworkCharacterController>();
        viewCamera ??= Camera.main;

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

        if (Object.HasInputAuthority)
        {
            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                viewCamera = mainCamera;
                ThirdPersonCameraController thirdPersonCamera = mainCamera.GetComponent<ThirdPersonCameraController>();
                if (thirdPersonCamera != null)
                {
                    thirdPersonCamera.SetTarget(transform);
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
                if (existingFollow != null && thirdPersonCamera != null)
                {
                    existingFollow.enabled = false;
                }
            }
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!GetInput(out NetworkInputData data))
        {
            UpdateMovementState(false, false);
            WasShiftHeld = false;
            ShiftHoldTime = 0f;
            return;
        }

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
            if (data.buttons.IsSet(NetworkInputData.MOUSEBUTTON0))
            {
                StartAction(ActionAttack, attackLockDuration);
                isActing = true;
                isBusy = true;
            }
            else if (data.buttons.IsSet(NetworkInputData.MOUSEBUTTON1))
            {
                StartAction(ActionParry, parryLockDuration);
                isActing = true;
                isBusy = true;
            }
            else if (shiftReleased && ShiftHoldTime < shiftHoldThreshold)
            {
                StartRoll(desiredMove);
                isRolling = true;
                isBusy = true;
            }
        }

        bool shouldRun = desiredMove.sqrMagnitude > 0.001f &&
                         shiftHeld &&
                         ShiftHoldTime >= shiftHoldThreshold &&
                         !isBusy;

        float currentSpeed = walkSpeed;
        Vector3 moveDirection = Vector3.zero;

        if (isRolling)
        {
            currentSpeed = rollSpeed;
            moveDirection = RollDirection;
        }
        else if (!isActing && desiredMove.sqrMagnitude > 0.001f)
        {
            currentSpeed = shouldRun ? runSpeed : walkSpeed;
            moveDirection = desiredMove.normalized;
        }

        ApplyMovement(moveDirection, currentSpeed);
        UpdateMovementState(moveDirection.sqrMagnitude > 0.001f, shouldRun);
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
                TriggerAction(LastAction);
            }
        }

        animator.SetBool(IsMoving, IsMovingNetworked);
        animator.SetBool(IsRunning, IsRunningNetworked);
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

    private void ApplyMovement(Vector3 moveDirection, float moveSpeed)
    {
        _networkCharacterController.maxSpeed = moveSpeed;
        _networkCharacterController.acceleration = movementAcceleration;
        _networkCharacterController.braking = movementBraking;

        if (moveDirection.sqrMagnitude > 0.001f)
        {
            _lastMoveDirection = moveDirection.normalized;
            Quaternion targetRotation = Quaternion.LookRotation(_lastMoveDirection, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                rotationSpeed * Runner.DeltaTime);
        }

        _networkCharacterController.Move(moveDirection);
    }

    private void UpdateMovementState(bool isMoving, bool isRunning)
    {
        IsMovingNetworked = isMoving;
        IsRunningNetworked = isRunning;
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
        }
    }
}
