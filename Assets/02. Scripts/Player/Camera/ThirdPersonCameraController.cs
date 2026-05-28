using UnityEngine;
using UnityEngine.EventSystems;

public class ThirdPersonCameraController : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 targetOffset = new Vector3(0f, 1.6f, 0f);

    [Header("Distance")]
    [SerializeField] private float distance = 4.5f;
    [SerializeField] private float minDistance = 2f;
    [SerializeField] private float maxDistance = 6f;
    [SerializeField] private float zoomSpeed = 2f;

    [Header("Rotation")]
    [SerializeField] private float mouseSensitivity = 180f;
    [SerializeField] private float minPitch = -20f;
    [SerializeField] private float maxPitch = 65f;

    [Header("Smoothing")]
    [SerializeField] private float positionSmoothTime = 0.08f;
    [SerializeField] private bool lockCursorOnStart = false;

    [Header("Lock On")]
    [SerializeField] private Vector3 lockOnTargetOffset = new Vector3(0f, 0.2f, 0f);
    [SerializeField] private float lockOnHeight = 1.2f;
    [SerializeField] private float lockOnTargetClearance = 2.5f;
    [SerializeField] private float lockOnMinLookPitch = -35f;
    [SerializeField] private float lockOnMaxLookPitch = 55f;

    private float _yaw;
    private float _pitch = 15f;
    private Vector3 _currentVelocity;
    private Transform _lockOnTarget;

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
    }

    private void UpdateRotation()
    {
        if (Cursor.lockState != CursorLockMode.Locked)
        {
            return;
        }

        float mouseX = Input.GetAxis("Mouse X");
        float mouseY = Input.GetAxis("Mouse Y");

        _yaw += mouseX * mouseSensitivity * Time.deltaTime;
        _pitch -= mouseY * mouseSensitivity * Time.deltaTime;
        _pitch = Mathf.Clamp(_pitch, minPitch, maxPitch);
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

        transform.position = Vector3.SmoothDamp(
            transform.position,
            desiredPosition,
            ref _currentVelocity,
            positionSmoothTime);

        Vector3 lookDirection = lockPoint - transform.position;
        if (lookDirection.sqrMagnitude > 0.0001f)
        {
            transform.rotation = GetClampedLockOnRotation(lookDirection);
            Vector3 currentEuler = transform.eulerAngles;
            _yaw = currentEuler.y;
            _pitch = NormalizeAngle(currentEuler.x);
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
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            SetCursorLock(false);
            return;
        }

        if (Input.GetMouseButtonDown(0) && Cursor.lockState != CursorLockMode.Locked)
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            SetCursorLock(true);
        }
    }

    private static void SetCursorLock(bool shouldLock)
    {
        Cursor.lockState = shouldLock ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !shouldLock;
    }
}
