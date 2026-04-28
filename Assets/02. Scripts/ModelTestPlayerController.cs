using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class ModelTestPlayerController : MonoBehaviour
{
    // Animator 파라미터 이름을 매 프레임 문자열로 찾지 않도록 해시로 캐싱한다.
    private static readonly int IsMoving = Animator.StringToHash("IsMoving");
    private static readonly int IsRunning = Animator.StringToHash("IsRunning");
    private static readonly int Attack = Animator.StringToHash("Attack");
    private static readonly int Parry = Animator.StringToHash("Parry");
    private static readonly int Roll = Animator.StringToHash("Roll");

    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private CharacterController characterController;
    [SerializeField] private Camera viewCamera;

    [Header("Movement")]
    [SerializeField] private float walkSpeed = 2.6f;
    [SerializeField] private float runSpeed = 4.8f;
    [SerializeField] private float rollSpeed = 6.5f;
    [SerializeField] private float rotationSpeed = 720f;
    [SerializeField] private float gravity = -20f;
    [SerializeField] private float rollDuration = 0.9f;

    // CharacterController는 중력을 직접 처리해야 하므로 현재 수직 속도를 따로 유지한다.
    private float _verticalVelocity;
    // 구르기 시작 후 남은 시간을 저장해서 액션이 끝날 때까지 이동을 유지한다.
    private float _rollTimeRemaining;
    // 구르기 입력이 들어온 순간의 방향을 기억해서 중간에 입력이 바뀌어도 같은 방향으로 굴러가게 한다.
    private Vector3 _rollDirection;

    private void Awake()
    {
        // 인스펙터에 직접 할당하지 않아도 기본적으로 같은 오브젝트의 컴포넌트를 찾아서 연결한다.
        animator ??= GetComponent<Animator>();
        characterController ??= GetComponent<CharacterController>();
        viewCamera ??= Camera.main;

        if (animator == null || characterController == null)
        {
            enabled = false;
            return;
        }

        animator.applyRootMotion = false;
    }

    private void Update()
    {
        // 실행 중 카메라가 바뀌거나 아직 연결되지 않은 경우를 대비해 메인 카메라를 다시 잡는다.
        if (viewCamera == null && Camera.main != null)
        {
            viewCamera = Camera.main;
        }

        float deltaTime = Time.deltaTime;
        Vector2 moveInput = ReadMoveInput();
        // 현재 카메라 방향을 기준으로 WASD 입력을 월드 이동 방향으로 바꾼다.
        Vector3 desiredMove = GetCameraRelativeMove(moveInput);
        bool isRolling = _rollTimeRemaining > 0f;
        // 액션 애니메이션이 재생 중일 때는 일반 이동/입력을 막는다.
        bool isBusy = isRolling || IsInActionState();

        // 이번 프레임에 새 액션 입력이 들어왔는지 확인한다.
        HandleActionInput(desiredMove, isBusy);
        // 확정된 입력/상태를 바탕으로 실제 CharacterController 이동을 수행한다.
        UpdateMovement(deltaTime, desiredMove, moveInput, isBusy);
        // 마지막으로 Animator 파라미터를 갱신해서 현재 상태에 맞는 애니메이션이 재생되도록 한다.
        UpdateAnimatorParameters(moveInput);
    }

    private Vector2 ReadMoveInput()
    {
        // 대각선 입력에서 속도가 더 빨라지지 않도록 길이가 1을 넘으면 정규화한다.
        Vector2 input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        return input.sqrMagnitude > 1f ? input.normalized : input;
    }

    private Vector3 GetCameraRelativeMove(Vector2 moveInput)
    {
        if (moveInput.sqrMagnitude <= 0.0001f)
        {
            return Vector3.zero;
        }

        // 캐릭터는 카메라가 보는 방향을 기준으로 움직이도록 카메라의 전/우측 벡터를 사용한다.
        Transform reference = viewCamera != null ? viewCamera.transform : transform;
        Vector3 forward = Vector3.ProjectOnPlane(reference.forward, Vector3.up).normalized;
        Vector3 right = Vector3.ProjectOnPlane(reference.right, Vector3.up).normalized;

        // 카메라가 위/아래를 심하게 보더라도 수평 이동만 하도록 바닥면에 투영한 방향을 사용한다.
        if (forward.sqrMagnitude < 0.0001f)
        {
            forward = Vector3.forward;
        }

        if (right.sqrMagnitude < 0.0001f)
        {
            right = Vector3.right;
        }

        Vector3 worldMove = forward * moveInput.y + right * moveInput.x;
        return worldMove.sqrMagnitude > 1f ? worldMove.normalized : worldMove;
    }

    private void HandleActionInput(Vector3 desiredMove, bool isBusy)
    {
        // 이미 다른 액션 재생 중이면 공격/패링/구르기 입력을 무시한다.
        if (isBusy)
        {
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            // 서로 다른 액션 트리거가 겹치지 않도록 나머지 트리거를 정리한 뒤 공격을 건다.
            animator.ResetTrigger(Parry);
            animator.ResetTrigger(Roll);
            animator.SetTrigger(Attack);
            return;
        }

        if (Input.GetMouseButtonDown(1))
        {
            // 패링도 같은 방식으로 다른 액션 트리거를 비우고 시작한다.
            animator.ResetTrigger(Attack);
            animator.ResetTrigger(Roll);
            animator.SetTrigger(Parry);
            return;
        }

        if (Input.GetKeyDown(KeyCode.Space))
        {
            // 구르기 시작 순간의 방향을 고정해서 끝까지 유지한다.
            _rollDirection = desiredMove.sqrMagnitude > 0.001f ? desiredMove : transform.forward;
            _rollDirection.y = 0f;
            _rollDirection.Normalize();

            if (_rollDirection.sqrMagnitude < 0.001f)
            {
                _rollDirection = transform.forward;
            }

            // 남은 시간을 설정한 뒤 같은 프레임에 바라보는 방향도 구르기 방향으로 맞춘다.
            _rollTimeRemaining = rollDuration;
            RotateTowards(_rollDirection, Time.deltaTime);

            animator.ResetTrigger(Attack);
            animator.ResetTrigger(Parry);
            animator.SetTrigger(Roll);
        }
    }

    private void UpdateMovement(float deltaTime, Vector3 desiredMove, Vector2 moveInput, bool isBusy)
    {
        // CharacterController는 Rigidbody처럼 자동 중력을 받지 않으므로 직접 수직 속도를 누적한다.
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
            // 구르기 이동은 스크립트로 직접 처리해서 컨트롤러 기준 이동을 유지한다.
            _rollTimeRemaining -= deltaTime;
            planarVelocity = _rollDirection * rollSpeed;
            RotateTowards(_rollDirection, deltaTime);
        }
        else if (!isBusy && desiredMove.sqrMagnitude > 0.001f)
        {
            // Shift + 전진 입력일 때만 달리기로 처리하고, 나머지는 걷기 속도를 사용한다.
            bool isRunning = Input.GetKey(KeyCode.LeftShift) && moveInput.y > 0.1f;
            planarVelocity = desiredMove * (isRunning ? runSpeed : walkSpeed);

            // 이동 방향으로 몸을 돌려서 앞뒤좌우 입력을 모두 walk/run 애니메이션 회전으로 해결한다.
            RotateTowards(desiredMove, deltaTime);
        }

        // 수평 이동과 중력 이동을 합쳐 한 번에 CharacterController에 적용한다.
        Vector3 totalVelocity = planarVelocity + Vector3.up * _verticalVelocity;
        characterController.Move(totalVelocity * deltaTime);
    }

    private void UpdateAnimatorParameters(Vector2 moveInput)
    {
        // 실제 이동 가능 여부와 별개로, 현재 입력이 있으면 이동 애니메이션으로 전환한다.
        bool isMoving = moveInput.sqrMagnitude > 0.01f;
        bool isRunning = isMoving && Input.GetKey(KeyCode.LeftShift) && moveInput.y > 0.1f;

        animator.SetBool(IsMoving, isMoving);
        animator.SetBool(IsRunning, isRunning);
    }

    private void RotateTowards(Vector3 direction, float deltaTime)
    {
        if (direction.sqrMagnitude <= 0.001f)
        {
            return;
        }

        // CharacterController 이동 방향과 캐릭터의 바라보는 방향이 자연스럽게 맞도록 점진적으로 회전시킨다.
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

        // 공격/패링/구르기 같은 액션 상태는 Animator에서 "Action" 태그로 묶어 두고 공통으로 검사한다.
        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
        return stateInfo.IsTag("Action") && stateInfo.normalizedTime < 0.98f;
    }
}
