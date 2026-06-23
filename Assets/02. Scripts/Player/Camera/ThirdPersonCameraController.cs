using UnityEngine;
public class ThirdPersonCameraController : MonoBehaviour
{
    public static bool ForceCursorVisible { get; set; }

    [Header("Target")]
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 targetOffset = new Vector3(0f, 1.6f, 0f);

    [Header("Distance")]
    [SerializeField] private float distance = 4.5f;
    [SerializeField] private float minDistance = 2f;
    [SerializeField] private float maxDistance = 6f;
    [SerializeField] private float zoomSpeed = 2f;

    [Header("Collision")]
    [SerializeField] private LayerMask obstructionLayers = ~0;
    [SerializeField] private float collisionOffset = 0.15f;
    [SerializeField] private float collisionFocusOffset = 0.45f;
    [SerializeField] private float minCollisionDistance = 1.6f;
    [SerializeField] private float collisionSmoothTime = 0.06f;
    [SerializeField] private float collisionReturnSmoothTime = 0.18f;
    [SerializeField] private float collisionReturnDelay = 0.12f;

    [Header("Rotation")]
    [SerializeField] private float mouseSensitivity = 4f;
    [SerializeField] private float maxMouseDelta = 8f;
    [SerializeField] private float startPitch = 15f;
    [SerializeField] private float minPitch = -20f;
    [SerializeField] private float maxPitch = 65f;

    [Header("Smoothing")]
    [SerializeField] private float positionSmoothTime = 0.08f;
    [SerializeField] private float lockOnRotationSharpness = 14f;
    [SerializeField] private bool alignBehindTargetOnStart = true;
    [SerializeField] private bool lockCursorOnStart = true;

    [Header("Lock On")]
    [SerializeField] private Vector3 lockOnTargetOffset = new Vector3(0f, 0.2f, 0f);
    [SerializeField] private float lockOnHeight = 1.2f;
    [SerializeField] private float lockOnTargetClearance = 2.5f;
    [SerializeField] private float lockOnMinLookPitch = -35f;
    [SerializeField] private float lockOnMaxLookPitch = 55f;

    private float _yaw;
    private float _pitch = 15f;
    private float _targetYaw;
    private float _targetPitch = 15f;
    private Vector3 _currentVelocity;
    private Transform _lockOnTarget;
    private bool _hasAlignedToTarget;
    private float _currentCollisionDistance = -1f;
    private float _collisionDistanceVelocity;
    private float _lastCollisionTime = -999f;

    public Transform Target => target;
    public Vector3 TargetOffset => targetOffset;
    public float Distance => distance;
    public float Pitch => _pitch;
    public float Yaw => _yaw;
    public Transform LockOnTarget => _lockOnTarget;
    public Vector3 LockOnTargetOffset => lockOnTargetOffset;
    public float LockOnHeight => lockOnHeight;

    private void Start()
    {
        Vector3 currentEuler = transform.eulerAngles;
        _yaw = currentEuler.y;
        _pitch = NormalizeAngle(currentEuler.x);
        _targetYaw = _yaw;
        _targetPitch = _pitch;

        if (alignBehindTargetOnStart)
        {
            AlignBehindTarget(true);
        }

        SetCursorLock(lockCursorOnStart);
    }

    private void LateUpdate()
    {
        UpdateCursorState();

        if (target == null)
        {
            return;
        }

        if (_lockOnTarget == null)
        {
            UpdateRotation();
        }

        UpdateZoom();
        if (_lockOnTarget != null)
        {
            UpdateLockOnPosition();
        }
        else
        {
            UpdatePosition();
        }
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        if (alignBehindTargetOnStart && !_hasAlignedToTarget)
        {
            AlignBehindTarget(true);
        }
    }

    public void SetLockOnTarget(Transform newTarget)
    {
        _lockOnTarget = newTarget;
    }

    public void ClearLockOnTarget()
    {
        _lockOnTarget = null;
        Vector3 currentEuler = transform.eulerAngles;
        _yaw = currentEuler.y;
        _pitch = NormalizeAngle(currentEuler.x);
        _targetYaw = _yaw;
        _targetPitch = _pitch;
    }

    private void UpdateRotation()
    {
        if (Cursor.lockState != CursorLockMode.Locked)
        {
            return;
        }

        float mouseX = Input.GetAxisRaw("Mouse X");
        float mouseY = Input.GetAxisRaw("Mouse Y");
        mouseX = Mathf.Clamp(mouseX, -maxMouseDelta, maxMouseDelta);
        mouseY = Mathf.Clamp(mouseY, -maxMouseDelta, maxMouseDelta);

        float yawDelta = mouseX * mouseSensitivity;
        float pitchDelta = mouseY * mouseSensitivity;

        _targetYaw += yawDelta;
        _targetPitch -= pitchDelta;
        _targetPitch = Mathf.Clamp(_targetPitch, minPitch, maxPitch);
        _yaw = _targetYaw;
        _pitch = _targetPitch;
    }

    private void AlignBehindTarget(bool snapPosition)
    {
        if (target == null)
        {
            return;
        }

        Vector3 targetForward = Vector3.ProjectOnPlane(target.forward, Vector3.up);
        if (targetForward.sqrMagnitude < 0.0001f)
        {
            targetForward = Vector3.forward;
        }

        Quaternion targetRotation = Quaternion.LookRotation(targetForward.normalized, Vector3.up);
        _yaw = targetRotation.eulerAngles.y;
        _pitch = Mathf.Clamp(startPitch, minPitch, maxPitch);
        _targetYaw = _yaw;
        _targetPitch = _pitch;
        _currentVelocity = Vector3.zero;
        _hasAlignedToTarget = true;

        if (!snapPosition)
        {
            return;
        }

        Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        Vector3 focusPoint = target.position + targetOffset;
        Vector3 desiredPosition = focusPoint - rotation * Vector3.forward * distance;
        transform.position = ResolveCameraCollision(focusPoint, desiredPosition);
        transform.rotation = rotation;
    }

    private void UpdateZoom()
    {
        float scrollInput = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scrollInput) <= 0.0001f)
        {
            return;
        }

        distance -= scrollInput * zoomSpeed;
        distance = Mathf.Clamp(distance, minDistance, maxDistance);
    }

    private void UpdatePosition()
    {
        Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        Vector3 focusPoint = target.position + targetOffset;
        Vector3 desiredPosition = focusPoint - rotation * Vector3.forward * distance;
        desiredPosition = ResolveCameraCollision(focusPoint, desiredPosition);

        transform.position = Vector3.SmoothDamp(
            transform.position,
            desiredPosition,
            ref _currentVelocity,
            positionSmoothTime);

        transform.rotation = rotation;
    }

    private void UpdateLockOnPosition()
    {
        Vector3 focusPoint = target.position + targetOffset;
        Vector3 lockPoint = _lockOnTarget.position + lockOnTargetOffset;
        Vector3 flatToLock = Vector3.ProjectOnPlane(lockPoint - focusPoint, Vector3.up);

        if (flatToLock.sqrMagnitude < 0.0001f)
        {
            flatToLock = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        }

        if (flatToLock.sqrMagnitude < 0.0001f)
        {
            flatToLock = Vector3.forward;
        }

        flatToLock.Normalize();
        Vector3 desiredPosition = focusPoint - flatToLock * distance + Vector3.up * lockOnHeight;
        desiredPosition = KeepClearOfLockOnTarget(desiredPosition, lockPoint, flatToLock);
        desiredPosition = ResolveCameraCollision(focusPoint, desiredPosition);

        transform.position = Vector3.SmoothDamp(
            transform.position,
            desiredPosition,
            ref _currentVelocity,
            positionSmoothTime);

        Vector3 lookDirection = lockPoint - transform.position;
        if (lookDirection.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRotation = GetClampedLockOnRotation(lookDirection);
            float rotationLerp = 1f - Mathf.Exp(-lockOnRotationSharpness * Time.deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationLerp);

            Vector3 currentEuler = transform.eulerAngles;
            _yaw = currentEuler.y;
            _pitch = NormalizeAngle(currentEuler.x);
            _targetYaw = _yaw;
            _targetPitch = _pitch;
        }
    }

    private Vector3 KeepClearOfLockOnTarget(Vector3 desiredPosition, Vector3 lockPoint, Vector3 flatToLock)
    {
        if (lockOnTargetClearance <= 0f)
        {
            return desiredPosition;
        }

        Vector3 fromLockPoint = desiredPosition - lockPoint;
        if (fromLockPoint.sqrMagnitude >= lockOnTargetClearance * lockOnTargetClearance)
        {
            return desiredPosition;
        }

        Vector3 pushDirection = Vector3.ProjectOnPlane(fromLockPoint, Vector3.up);
        if (pushDirection.sqrMagnitude < 0.0001f)
        {
            pushDirection = -flatToLock;
        }

        pushDirection.Normalize();
        Vector3 clearedPosition = lockPoint + pushDirection * lockOnTargetClearance;
        clearedPosition.y = desiredPosition.y;
        return clearedPosition;
    }

    private Vector3 ResolveCameraCollision(Vector3 focusPoint, Vector3 desiredPosition)
    {
        Vector3 toCamera = desiredPosition - focusPoint;
        float desiredDistance = toCamera.magnitude;
        if (desiredDistance <= 0.0001f)
        {
            return desiredPosition;
        }

        Vector3 direction = toCamera / desiredDistance;
        Vector3 castOrigin = focusPoint + direction * collisionFocusOffset;
        float castDistance = Mathf.Max(0f, desiredDistance - collisionFocusOffset);
        float targetDistance = desiredDistance;
        RaycastHit hit = default;
        bool hasObstruction = castDistance > 0.0001f &&
            TryGetNearestCameraObstruction(castOrigin, direction, castDistance, out hit);

        if (hasObstruction)
        {
            targetDistance = Mathf.Max(minCollisionDistance, collisionFocusOffset + hit.distance - collisionOffset);
            _lastCollisionTime = Time.time;
        }
        else if (_currentCollisionDistance > 0f &&
                 _currentCollisionDistance < desiredDistance &&
                 Time.time - _lastCollisionTime < collisionReturnDelay)
        {
            targetDistance = _currentCollisionDistance;
        }

        if (_currentCollisionDistance < 0f)
        {
            _currentCollisionDistance = targetDistance;
        }

        float smoothTime = targetDistance < _currentCollisionDistance
            ? collisionSmoothTime
            : collisionReturnSmoothTime;

        _currentCollisionDistance = Mathf.SmoothDamp(
            _currentCollisionDistance,
            targetDistance,
            ref _collisionDistanceVelocity,
            smoothTime);

        return focusPoint + direction * _currentCollisionDistance;
    }

    private bool TryGetNearestCameraObstruction(
        Vector3 origin,
        Vector3 direction,
        float maxDistance,
        out RaycastHit nearestHit)
    {
        nearestHit = default;
        RaycastHit[] hits = Physics.RaycastAll(
            origin,
            direction,
            maxDistance,
            obstructionLayers,
            QueryTriggerInteraction.Ignore);

        float nearestDistance = float.MaxValue;
        bool found = false;
        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit candidate = hits[i];
            if (candidate.collider == null || IsTargetCollider(candidate.collider))
            {
                continue;
            }

            if (candidate.distance >= nearestDistance)
            {
                continue;
            }

            nearestDistance = candidate.distance;
            nearestHit = candidate;
            found = true;
        }

        return found;
    }

    private bool IsTargetCollider(Collider candidate)
    {
        return target != null && candidate.transform.IsChildOf(target);
    }

    private Quaternion GetClampedLockOnRotation(Vector3 lookDirection)
    {
        Quaternion rawRotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
        Vector3 euler = rawRotation.eulerAngles;
        float pitch = Mathf.Clamp(NormalizeAngle(euler.x), lockOnMinLookPitch, lockOnMaxLookPitch);
        return Quaternion.Euler(pitch, euler.y, 0f);
    }

    private static float NormalizeAngle(float angle)
    {
        while (angle > 180f)
        {
            angle -= 360f;
        }

        while (angle < -180f)
        {
            angle += 360f;
        }

        return angle;
    }

    private void UpdateCursorState()
    {
        bool altHeld = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
        if (ForceCursorVisible || altHeld)
        {
            SetCursorLock(false);
            return;
        }

        SetCursorLock(true);
    }

    private static void SetCursorLock(bool shouldLock)
    {
        Cursor.lockState = shouldLock ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !shouldLock;
    }
}
