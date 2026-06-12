using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class ModelTestPlayerController : MonoBehaviour
{
    // Animator 파라미터 이름을 매번 문자열로 찾지 않도록 해시 값으로 미리 캐싱한다.
    // 이렇게 해두면 매 프레임 SetFloat/SetTrigger를 호출해도 불필요한 문자열 비교를 줄일 수 있다.
    private static readonly int MoveSpeed = Animator.StringToHash("MoveSpeed");
    private static readonly int Attack = Animator.StringToHash("Attack");
    private static readonly int Parry = Animator.StringToHash("Parry");
    private static readonly int Roll = Animator.StringToHash("Roll");

    [Header("참조 컴포넌트")]
    [SerializeField] private Animator animator;
    [SerializeField] private CharacterController characterController;
    [SerializeField] private Camera viewCamera;

    [Header("이동 설정")]
    [SerializeField] private float walkSpeed = 2.6f;
    [SerializeField] private float runSpeed = 4.8f;
    [SerializeField] private float rollSpeed = 6.5f;
    [SerializeField] private float rotationSpeed = 720f;
    [SerializeField] private float gravity = -20f;
    [SerializeField] private float rollDuration = 0.9f;
    [SerializeField] private float shiftHoldThreshold = 0.25f;

    // CharacterController는 Rigidbody처럼 중력을 자동으로 적용하지 않기 때문에
    // 현재 수직 속도를 직접 누적해서 매 프레임 이동 벡터에 더해준다.
    private float _verticalVelocity;

    // 구르기는 입력 순간의 방향으로 일정 시간 계속 이동해야 하므로
    // 남은 시간과 시작 방향을 따로 저장해 둔다.
    private float _rollTimeRemaining;
    private Vector3 _rollDirection;

    // Shift를 짧게 눌렀는지, 길게 눌렀는지 구분하기 위한 상태값이다.
    // 눌린 동안 시간을 누적하고, 뗐을 때 누적 시간이 임계값보다 짧으면 구르기,
    // 임계값 이상이면 달리기로 해석한다.
    private bool _isShiftHeld;
    private float _shiftHoldTime;
    private bool _movementInputLockedUntilRelease;

    private void Awake()
    {
        // 인스펙터에 비워둔 경우에도 같은 오브젝트에서 기본 컴포넌트를 자동으로 찾아 연결한다.
        animator ??= GetComponent<Animator>();
        characterController ??= GetComponent<CharacterController>();
        viewCamera ??= Camera.main;

        // 이동과 애니메이션 재생에 필수인 컴포넌트가 없으면 더 이상 동작시키지 않는다.
        if (animator == null || characterController == null)
        {
            enabled = false;
            return;
        }

        // 실제 이동은 CharacterController로 직접 처리하므로 루트 모션은 끈다.
        animator.applyRootMotion = false;
    }

    private void Update()
    {
        // 실행 중 메인 카메라가 바뀌었거나 아직 연결되지 않았다면 다시 참조를 잡아준다.
        if (viewCamera == null && Camera.main != null)
        {
            viewCamera = Camera.main;
        }

        float deltaTime = Time.deltaTime;

        // 현재 프레임의 WASD 입력을 읽는다.
        Vector2 moveInput = ReadMoveInput();

        if (_movementInputLockedUntilRelease)
        {
            if (moveInput.sqrMagnitude <= 0.0001f)
            {
                _movementInputLockedUntilRelease = false;
            }
            else
            {
                moveInput = Vector2.zero;
            }
        }

        // 입력값을 카메라 기준 월드 이동 방향으로 변환한다.
        Vector3 desiredMove = GetCameraRelativeMove(moveInput);

        // 구르기 중인지, 혹은 액션 애니메이션 중인지 먼저 판단한다.
        bool isRolling = _rollTimeRemaining > 0f;
        bool isBusy = isRolling || IsInActionState();

        // Shift 탭/홀드 판정을 먼저 갱신한다.
        // 이 단계에서 Shift를 짧게 눌렀다가 떼면 구르기가 시작될 수 있다.
        UpdateShiftState(deltaTime, desiredMove, isBusy);

        // 공격, 패링, Space 구르기 같은 즉시 액션 입력을 처리한다.
        bool startedAction = HandleActionInput(desiredMove, isBusy);

        if (startedAction)
        {
            isBusy = true;
            moveInput = Vector2.zero;
            desiredMove = Vector3.zero;
            _movementInputLockedUntilRelease = true;
        }

        // 확정된 상태를 기준으로 실제 캐릭터 이동을 수행한다.
        UpdateMovement(deltaTime, desiredMove, moveInput, isBusy);

        // 마지막으로 현재 입력 상태를 Animator 파라미터에 반영한다.
        UpdateAnimatorParameters(moveInput);
    }

    private Vector2 ReadMoveInput()
    {
        // Raw 입력을 사용해서 키보드 입력을 즉각적으로 받는다.
        // 대각선 입력 시 속도가 더 빨라지지 않도록 길이가 1을 넘으면 정규화한다.
        Vector2 input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        return input.sqrMagnitude > 1f ? input.normalized : input;
    }

    private Vector3 GetCameraRelativeMove(Vector2 moveInput)
    {
        // 이동 입력이 거의 없으면 불필요한 계산 없이 바로 정지 벡터를 반환한다.
        if (moveInput.sqrMagnitude <= 0.0001f)
        {
            return Vector3.zero;
        }

        // 플레이어 이동은 카메라가 바라보는 방향 기준으로 계산한다.
        // 단, 위아래 기울기는 제거해서 항상 바닥면 위에서만 움직이도록 만든다.
        Transform reference = viewCamera != null ? viewCamera.transform : transform;
        Vector3 forward = Vector3.ProjectOnPlane(reference.forward, Vector3.up).normalized;
        Vector3 right = Vector3.ProjectOnPlane(reference.right, Vector3.up).normalized;

        // 극단적인 카메라 각도 등으로 벡터 길이가 0에 가까워졌을 때를 대비한 보정값이다.
        if (forward.sqrMagnitude < 0.0001f)
        {
            forward = Vector3.forward;
        }

        if (right.sqrMagnitude < 0.0001f)
        {
            right = Vector3.right;
        }

        // 세로 입력은 카메라 전방, 가로 입력은 카메라 우측 기준으로 합쳐서 월드 이동 벡터를 만든다.
        Vector3 worldMove = forward * moveInput.y + right * moveInput.x;
        return worldMove.sqrMagnitude > 1f ? worldMove.normalized : worldMove;
    }

    private bool HandleActionInput(Vector3 desiredMove, bool isBusy)
    {
        // 이미 다른 액션이 재생 중이면 새로운 액션 입력은 무시한다.
        if (isBusy)
        {
            return false;
        }

        if (Input.GetMouseButtonDown(0))
        {
            // 공격 시작 전에 다른 액션 트리거를 정리해서 상태 충돌을 줄인다.
            animator.ResetTrigger(Parry);
            animator.ResetTrigger(Roll);
            animator.SetTrigger(Attack);
            return true;
        }

        if (Input.GetMouseButtonDown(1))
        {
            // 패링도 같은 방식으로 다른 액션 트리거를 정리한 뒤 시작한다.
            animator.ResetTrigger(Attack);
            animator.ResetTrigger(Roll);
            animator.SetTrigger(Parry);
            return true;
        }

        return false;
    }

    private void UpdateMovement(float deltaTime, Vector3 desiredMove, Vector2 moveInput, bool isBusy)
    {
        // CharacterController용 수동 중력 처리.
        // 땅에 닿아 있고 아래로 떨어지는 중이면 작은 음수 값으로 고정해서 바닥에 안정적으로 붙인다.
        bool grounded = characterController.isGrounded;
        if (grounded && _verticalVelocity < 0f)
        {
            _verticalVelocity = -2f;
        }
        else
        {
            _verticalVelocity += gravity * deltaTime;
        }

        Vector3 planarVelocity = Vector3.zero;

        if (_rollTimeRemaining > 0f)
        {
            // 구르기 중에는 일반 이동 입력보다 구르기 이동이 우선한다.
            // 시작할 때 저장한 방향으로 일정 시간 계속 전진한다.
            _rollTimeRemaining -= deltaTime;
            planarVelocity = _rollDirection * rollSpeed;
            RotateTowards(_rollDirection, deltaTime);
        }
        else if (!isBusy && desiredMove.sqrMagnitude > 0.001f)
        {
            // 구르기 중도 아니고 다른 액션 중도 아닐 때만 일반 걷기/달리기 이동을 적용한다.
            bool isRunning = ShouldRun(moveInput);
            planarVelocity = desiredMove * (isRunning ? runSpeed : walkSpeed);

            // 실제 이동 방향을 바라보도록 천천히 회전시킨다.
            RotateTowards(desiredMove, deltaTime);
        }

        // 수평 이동과 수직 속도를 합쳐 최종 이동 벡터를 만들고 한 번에 적용한다.
        Vector3 totalVelocity = planarVelocity + Vector3.up * _verticalVelocity;
        characterController.Move(totalVelocity * deltaTime);
    }

    private void UpdateAnimatorParameters(Vector2 moveInput)
    {
        bool isMoving = moveInput.sqrMagnitude > 0.01f;
        float moveSpeed = isMoving ? (ShouldRun(moveInput) ? 1f : 0.5f) : 0f;
        animator.SetFloat(MoveSpeed, moveSpeed, 0.12f, Time.deltaTime);
    }

    private void UpdateShiftState(float deltaTime, Vector3 desiredMove, bool isBusy)
    {
        bool shiftHeldNow = IsShiftHeld();

        // 이번 프레임에 Shift를 새로 누른 경우, 홀드 시간 측정을 0부터 다시 시작한다.
        if (!_isShiftHeld && shiftHeldNow)
        {
            _isShiftHeld = true;
            _shiftHoldTime = 0f;
        }

        // Shift를 계속 누르고 있는 동안에는 홀드 시간을 누적한다.
        if (_isShiftHeld && shiftHeldNow)
        {
            _shiftHoldTime += deltaTime;
        }

        // Shift를 떼는 순간, 짧게 눌렀는지 길게 눌렀는지 최종 판정한다.
        if (_isShiftHeld && !shiftHeldNow)
        {
            bool shouldRoll = _shiftHoldTime < shiftHoldThreshold;

            // 다음 입력 판정을 위해 상태값을 즉시 초기화한다.
            _isShiftHeld = false;
            _shiftHoldTime = 0f;

            // 짧게 눌렀고 현재 다른 액션 중이 아니면 구르기를 시작한다.
            if (shouldRoll && !isBusy)
            {
                StartRoll(desiredMove);
            }
        }
    }

    private bool IsShiftHeld()
    {
        // 좌/우 Shift 어느 쪽이든 누르고 있으면 동일하게 처리한다.
        return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
    }

    private bool ShouldRun(Vector2 moveInput)
    {
        // 달리기는 세 조건을 모두 만족해야 한다.
        // 1. 실제 이동 입력이 있어야 하고
        // 2. Shift를 현재 누르고 있어야 하며
        // 3. 누른 시간이 임계값 이상이어야 한다.
        return moveInput.sqrMagnitude > 0.01f &&
               IsShiftHeld() &&
               _shiftHoldTime >= shiftHoldThreshold;
    }

    private void StartRoll(Vector3 desiredMove)
    {
        // 이동 입력이 있는 상태라면 그 방향으로, 없으면 현재 바라보는 정면 방향으로 구른다.
        _rollDirection = desiredMove.sqrMagnitude > 0.001f ? desiredMove : transform.forward;
        _rollDirection.y = 0f;
        _rollDirection.Normalize();

        // 방향 벡터가 비정상적으로 작아진 경우 마지막 안전장치로 정면 방향을 사용한다.
        if (_rollDirection.sqrMagnitude < 0.001f)
        {
            _rollDirection = transform.forward;
        }

        // 구르기 시간을 설정하고, 시작 즉시 해당 방향을 바라보도록 회전시킨다.
        _rollTimeRemaining = rollDuration;
        RotateTowards(_rollDirection, Time.deltaTime);

        // 공격/패링과 겹치지 않도록 트리거를 정리한 뒤 Roll 트리거를 켠다.
        animator.ResetTrigger(Attack);
        animator.ResetTrigger(Parry);
        animator.SetTrigger(Roll);
    }

    private void RotateTowards(Vector3 direction, float deltaTime)
    {
        if (direction.sqrMagnitude <= 0.001f)
        {
            return;
        }

        // 목표 방향을 향해 즉시 꺾지 않고 일정 속도로 회전시켜 더 자연스럽게 보이도록 한다.
        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            targetRotation,
            rotationSpeed * deltaTime);
    }

    private bool IsInActionState()
    {
        if (animator == null)
        {
            return false;
        }

        // 공격, 패링, 구르기 같은 액션 상태는 Animator에서 "Action" 태그로 묶어 두고,
        // 해당 태그가 붙은 상태가 아직 거의 끝나지 않았다면 바쁜 상태로 간주한다.
        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
        return stateInfo.IsTag("Action") && stateInfo.normalizedTime < 0.98f;
    }
}
