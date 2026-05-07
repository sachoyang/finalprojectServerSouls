using Fusion;
using UnityEngine;

[RequireComponent(typeof(NetworkCharacterController))]
public class NetworkPlayerController : Player
{
    // Animator 파라미터 이름을 매 프레임 문자열로 찾지 않도록 해시로 미리 변환한다.
    // Fusion의 Render 단계에서 여러 플레이어가 동시에 갱신되므로, 작은 비용도 줄이는 편이 좋다.
    private static readonly int IsMoving = Animator.StringToHash("IsMoving");
    private static readonly int IsRunning = Animator.StringToHash("IsRunning");
    private static readonly int Attack = Animator.StringToHash("Attack");
    private static readonly int Parry = Animator.StringToHash("Parry");
    private static readonly int Roll = Animator.StringToHash("Roll");
    private static readonly int Jump = Animator.StringToHash("Jump");

    // 애니메이션 트리거를 네트워크 상태로 전달하기 위한 액션 코드.
    // Trigger 자체는 bool처럼 상태가 남지 않으므로, LastAction + ActionSequence 조합으로
    // "어떤 액션이 새로 발생했는지"를 모든 클라이언트가 동일하게 감지한다.
    private const byte ActionAttack = 1;
    private const byte ActionParry = 2;
    private const byte ActionRoll = 3;
    private const byte ActionJump = 4;

    [Header("References")]
    // 캐릭터 모델의 Animator. 인스펙터에서 지정하지 않으면 Awake에서 같은 오브젝트에서 찾는다.
    [SerializeField] private Animator animator;
    // 입력 권한을 가진 로컬 플레이어가 바라보는 카메라.
    // 이동 입력을 카메라 기준으로 만들 때 쓰는 값이며, Spawned에서 메인 카메라로 보정한다.
    [SerializeField] private Camera viewCamera;

    [Header("Movement")]
    // 일반 이동 속도.
    [SerializeField] private float walkSpeed = 2.6f;
    // Shift를 일정 시간 이상 누르고 있을 때 적용되는 달리기 속도.
    [SerializeField] private float runSpeed = 4.8f;
    // 구르기 중 강제로 적용되는 이동 속도.
    [SerializeField] private float rollSpeed = 6.5f;
    // 이동 방향으로 캐릭터가 회전하는 초당 각도.
    [SerializeField] private float rotationSpeed = 720f;
    // 구르기 모션과 이동 잠금이 유지되는 시간.
    [SerializeField] private float rollDuration = 0.9f;
    // Shift를 짧게 눌렀다 떼면 구르기, 이 시간 이상 누르면 달리기로 판정한다.
    [SerializeField] private float shiftHoldThreshold = 0.25f;
    // NetworkCharacterController에 전달할 가속도.
    [SerializeField] private float movementAcceleration = 80f;
    // 입력이 줄거나 멈췄을 때 감속되는 정도.
    [SerializeField] private float movementBraking = 30f;

    [Header("Action Locks")]
    // 공격 애니메이션 중 다른 행동과 이동을 막는 시간.
    [SerializeField] private float attackLockDuration = 0.65f;
    // 패링 애니메이션 중 다른 행동과 이동을 막는 시간.
    [SerializeField] private float parryLockDuration = 0.5f;
    // 점프 입력 시 NetworkCharacterController에 전달하는 위쪽 힘.
    [SerializeField] private float jumpImpulse = 8f;

    // Animator의 이동 bool을 모든 클라이언트에서 같은 값으로 재생하기 위한 네트워크 상태.
    [Networked] private bool IsMovingNetworked { get; set; }
    // 달리기 애니메이션 여부. 이동 중이어도 구르기/공격 중에는 false가 된다.
    [Networked] private bool IsRunningNetworked { get; set; }
    // 이전 네트워크 틱에서 Shift가 눌려 있었는지 저장한다. 짧게 눌렀다 뗀 순간을 감지하는 데 사용한다.
    [Networked] private bool WasShiftHeld { get; set; }
    // Shift를 누른 누적 시간. 짧은 탭은 구르기, 길게 누르기는 달리기로 분리한다.
    [Networked] private float ShiftHoldTime { get; set; }
    // 구르기 이동이 끝나는 시점을 네트워크 틱 기준으로 관리한다.
    [Networked] private TickTimer RollTimer { get; set; }
    // 공격/패링처럼 행동 중 이동을 잠그는 시간을 네트워크 틱 기준으로 관리한다.
    [Networked] private TickTimer ActionTimer { get; set; }
    // 구르기 시작 순간의 방향을 저장해, 구르기 중 입력이 바뀌어도 같은 방향으로 이동하게 한다.
    [Networked] private Vector3 RollDirection { get; set; }
    // 가장 최근에 발생한 액션 종류. ActionSequence가 바뀔 때 이 값을 읽어 애니메이션 트리거를 실행한다.
    [Networked] private byte LastAction { get; set; }
    // 액션 발생 횟수. 같은 액션이 연속으로 발생해도 값이 증가하므로 Render에서 변화를 감지할 수 있다.
    [Networked] private int ActionSequence { get; set; }

    // Fusion 기본 NetworkCharacterController. 실제 이동, 점프, 접지 판정을 담당한다.
    private NetworkCharacterController _networkCharacterController;
    // 네트워크 상태 변화 감지기. Render에서 ActionSequence 변경 여부를 확인하는 데 사용한다.
    private ChangeDetector _changeDetector;
    // 마지막으로 유효했던 이동 방향. 입력 없이 구르기를 시작할 때 캐릭터가 바라보던 방향으로 구른다.
    private Vector3 _lastMoveDirection = Vector3.forward;

    private void Awake()
    {
        // 인스펙터 연결이 빠져도 가능한 한 자동으로 참조를 채운다.
        animator ??= GetComponent<Animator>();
        _networkCharacterController = GetComponent<NetworkCharacterController>();
        viewCamera ??= Camera.main;

        // 필수 컴포넌트가 없으면 이후 네트워크 틱에서 NullReference가 반복되므로 스크립트를 비활성화한다.
        if (animator == null || _networkCharacterController == null)
        {
            enabled = false;
            return;
        }

        // 이동은 NetworkCharacterController가 담당하므로, 애니메이션 루트 모션이 위치를 끌고 가지 않게 한다.
        animator.applyRootMotion = false;
        // 첫 구르기 방향이 Vector3.forward로 고정되지 않도록 현재 캐릭터 방향으로 초기화한다.
        _lastMoveDirection = transform.forward;
    }

    public override void Spawned()
    {
        // SimulationState 기준 변화만 감지하면 네트워크로 확정된 액션 발생을 Render에서 안정적으로 재생할 수 있다.
        _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);

        // 카메라는 로컬 입력 권한을 가진 플레이어에게만 붙인다.
        // 원격 플레이어가 Spawn될 때마다 메인 카메라 타겟을 빼앗지 않도록 권한을 확인한다.
        if (Object.HasInputAuthority)
        {
            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                viewCamera = mainCamera;

                // 프로젝트에 ThirdPersonCameraController가 있으면 그 컨트롤러를 우선 사용한다.
                ThirdPersonCameraController thirdPersonCamera = mainCamera.GetComponent<ThirdPersonCameraController>();
                if (thirdPersonCamera != null)
                {
                    thirdPersonCamera.SetTarget(transform);
                }
                else
                {
                    // 전용 3인칭 카메라가 없을 때는 단순 추적 컴포넌트를 붙여 기본 동작을 보장한다.
                    CameraFollow follow = mainCamera.GetComponent<CameraFollow>();
                    if (follow == null)
                    {
                        follow = mainCamera.gameObject.AddComponent<CameraFollow>();
                    }

                    follow.target = transform;
                }

                // 두 카메라 추적 방식이 동시에 동작하면 카메라 위치가 서로 덮어써질 수 있다.
                // 전용 3인칭 컨트롤러가 존재할 때는 기존 CameraFollow를 꺼서 충돌을 막는다.
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
        // Fusion 입력은 입력 권한 클라이언트가 수집하고, 상태 권한이 있는 쪽에서 시뮬레이션된다.
        // 입력이 없으면 이동 애니메이션과 Shift 탭 상태를 초기화해 끊긴 입력이 남지 않게 한다.
        if (!GetInput(out NetworkInputData data))
        {
            UpdateMovementState(false, false);
            WasShiftHeld = false;
            ShiftHoldTime = 0f;
            return;
        }

        // 입력 방향은 대각선 이동이 더 빨라지지 않도록 최대 길이를 1로 제한한다.
        Vector3 desiredMove = data.direction;
        if (desiredMove.sqrMagnitude > 1f)
        {
            desiredMove.Normalize();
        }

        // Shift는 두 의미를 가진다.
        // 1) 짧게 탭 후 떼기: 구르기
        // 2) 일정 시간 이상 누르고 유지하기: 달리기
        // 따라서 현재 눌림 상태와 누적 시간을 네트워크 상태로 저장한다.
        bool shiftHeld = data.buttons.IsSet(NetworkInputData.SHIFT);
        if (shiftHeld)
        {
            ShiftHoldTime = WasShiftHeld ? ShiftHoldTime + Runner.DeltaTime : 0f;
        }
        else if (!WasShiftHeld)
        {
            ShiftHoldTime = 0f;
        }

        // 현재 틱에서 Shift가 눌림 상태에서 해제 상태로 바뀐 순간.
        bool shiftReleased = WasShiftHeld && !shiftHeld;
        // TickTimer는 Runner 기준으로 만료 여부를 판단해야 네트워크 틱과 정확히 맞는다.
        bool isRolling = !RollTimer.ExpiredOrNotRunning(Runner);
        bool isActing = !ActionTimer.ExpiredOrNotRunning(Runner);
        // 구르기/공격/패링 중에는 새 액션 입력과 일반 이동을 제한한다.
        bool isBusy = isRolling || isActing;

        // 구르기 입력이 이동 입력 없이 들어와도 자연스러운 방향을 쓰기 위해 마지막 이동 방향을 보관한다.
        if (desiredMove.sqrMagnitude > 0.001f)
        {
            _lastMoveDirection = desiredMove.normalized;
        }

        // 행동 잠금이 없을 때만 새 액션을 시작한다.
        // 우선순위는 점프 > 공격 > 패링 > 구르기이며, 한 틱에 하나만 처리한다.
        if (!isBusy)
        {
            if (data.buttons.IsSet(NetworkInputData.JUMP) && _networkCharacterController.Grounded)
            {
                // 점프는 별도 잠금 타이머 없이 즉시 컨트롤러에 힘을 전달하고 애니메이션만 동기화한다.
                _networkCharacterController.Jump(false, jumpImpulse);
                LastAction = ActionJump;
                ActionSequence++;
            }
            else if (data.buttons.IsSet(NetworkInputData.MOUSEBUTTON0))
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

        // Shift를 누르고 있는 시간이 임계값을 넘은 뒤, 움직이고 있으며 다른 액션 중이 아닐 때만 달리기로 본다.
        bool shouldRun = desiredMove.sqrMagnitude > 0.001f &&
                         shiftHeld &&
                         ShiftHoldTime >= shiftHoldThreshold &&
                         !isBusy;

        float currentSpeed = walkSpeed;
        Vector3 moveDirection = Vector3.zero;

        // 구르기는 입력 변화와 무관하게 시작 시 저장한 방향/속도로 진행한다.
        if (isRolling)
        {
            currentSpeed = rollSpeed;
            moveDirection = RollDirection;
        }
        // 공격/패링 중에는 이동 방향을 zero로 유지해 제자리 액션이 되도록 한다.
        else if (!isActing && desiredMove.sqrMagnitude > 0.001f)
        {
            currentSpeed = shouldRun ? runSpeed : walkSpeed;
            moveDirection = desiredMove.normalized;
        }

        // 실제 이동과 애니메이션 상태는 매 네트워크 틱에서 계산된 최종값으로 갱신한다.
        ApplyMovement(moveDirection, currentSpeed);
        UpdateMovementState(moveDirection.sqrMagnitude > 0.001f, shouldRun);
        WasShiftHeld = shiftHeld;

        // Shift가 떨어져 있으면 다음 탭/홀드 판정을 위해 누적 시간을 초기화한다.
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

        // Render는 화면 프레임마다 호출되므로, 네트워크 상태가 실제로 바뀐 경우에만 Trigger를 재생한다.
        // ActionSequence가 증가하면 LastAction에 저장된 액션을 한 번만 트리거한다.
        foreach (string change in _changeDetector.DetectChanges(this))
        {
            if (change == nameof(ActionSequence))
            {
                // 이전 프레임에 남아 있을 수 있는 Trigger를 정리한 뒤 새 액션을 넣는다.
                // 이렇게 하면 공격 직후 패링 같은 연속 입력에서도 Animator가 잘못된 Trigger를 소비할 가능성이 줄어든다.
                animator.ResetTrigger(Attack);
                animator.ResetTrigger(Parry);
                animator.ResetTrigger(Roll);
                animator.ResetTrigger(Jump);
                TriggerAction(LastAction);
            }
        }

        // bool 파라미터는 현재 네트워크 상태를 그대로 반영한다.
        animator.SetBool(IsMoving, IsMovingNetworked);
        animator.SetBool(IsRunning, IsRunningNetworked);
    }

    private void StartAction(byte actionType, float lockDuration)
    {
        // 액션 종류와 발생 횟수를 네트워크 상태로 기록해 모든 클라이언트에서 같은 애니메이션을 시작한다.
        LastAction = actionType;
        ActionSequence++;
        // lockDuration 동안 다른 액션과 일반 이동 입력을 막는다.
        ActionTimer = TickTimer.CreateFromSeconds(Runner, lockDuration);
    }

    private void StartRoll(Vector3 desiredMove)
    {
        // 이동 입력이 있으면 그 방향으로 구르고, 없으면 마지막 이동 방향을 사용한다.
        Vector3 rollDirection = desiredMove.sqrMagnitude > 0.001f ? desiredMove.normalized : _lastMoveDirection;
        if (rollDirection.sqrMagnitude < 0.001f)
        {
            // 마지막 방향도 유효하지 않은 예외 상황에서는 현재 캐릭터 정면을 사용한다.
            rollDirection = transform.forward;
        }

        // 경사나 점프 상태에서 y값이 섞여도 수평 방향 구르기가 되도록 평면에 투영한다.
        RollDirection = Vector3.ProjectOnPlane(rollDirection, Vector3.up).normalized;
        if (RollDirection.sqrMagnitude < 0.001f)
        {
            RollDirection = transform.forward;
        }

        // 구르기는 애니메이션 액션이면서 동시에 이동 타이머가 필요한 특수 액션이다.
        StartAction(ActionRoll, rollDuration);
        RollTimer = TickTimer.CreateFromSeconds(Runner, rollDuration);
    }

    private void ApplyMovement(Vector3 moveDirection, float moveSpeed)
    {
        // 현재 상태에 맞는 속도/가속/제동 값을 컨트롤러에 전달한다.
        _networkCharacterController.maxSpeed = moveSpeed;
        _networkCharacterController.acceleration = movementAcceleration;
        _networkCharacterController.braking = movementBraking;

        // 이동 방향이 있을 때만 캐릭터를 회전시킨다.
        // 공격/패링처럼 moveDirection이 zero인 상태에서는 마지막 바라보던 방향을 유지한다.
        if (moveDirection.sqrMagnitude > 0.001f)
        {
            _lastMoveDirection = moveDirection.normalized;
            Quaternion targetRotation = Quaternion.LookRotation(_lastMoveDirection, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                rotationSpeed * Runner.DeltaTime);
        }

        // 실제 위치 이동은 Fusion의 NetworkCharacterController가 처리한다.
        _networkCharacterController.Move(moveDirection);
    }

    private void UpdateMovementState(bool isMoving, bool isRunning)
    {
        // 이동 애니메이션 bool은 Render 단계에서 Animator에 반영되도록 네트워크 상태에 저장한다.
        IsMovingNetworked = isMoving;
        IsRunningNetworked = isRunning;
    }

    private void TriggerAction(byte actionType)
    {
        // 네트워크로 전달된 액션 코드에 맞는 Animator Trigger를 실행한다.
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
