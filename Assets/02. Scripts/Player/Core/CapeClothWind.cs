using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Cloth))]
public sealed class CapeClothWind : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Cloth targetCloth;
    [Tooltip("움직임을 측정할 플레이어 루트입니다. 비어 있으면 최상위 Transform을 사용합니다.")]
    [SerializeField] private Transform movementRoot;
    [SerializeField] private PlayerStats playerStats;

    [Header("Wind")]
    [Min(0.01f)]
    [SerializeField] private float fullWindSpeed = 6f;
    [Min(0f)]
    [SerializeField] private float backwardAcceleration = 12f;
    [Min(0f)]
    [SerializeField] private float liftAcceleration = 4f;
    [SerializeField] private bool keepWindBehindCharacter = true;
    [Range(0f, 0.45f)]
    [SerializeField] private float movementDirectionInfluence = 0.25f;
    [Range(0f, 90f)]
    [SerializeField] private float maximumWindAngleFromBack = 55f;
    [Min(0.01f)]
    [SerializeField] private float windDirectionSharpness = 6f;
    [Min(0f)]
    [SerializeField] private float minimumMoveSpeed = 0.2f;
    [Min(0.01f)]
    [SerializeField] private float maximumMeasuredSpeed = 15f;
    [Min(0.01f)]
    [SerializeField] private float teleportDistance = 2f;
    [Min(0.01f)]
    [SerializeField] private float maximumAcceleration = 20f;

    [Header("Response")]
    [Min(0.01f)]
    [SerializeField] private float windRiseTime = 0.12f;
    [Min(0.01f)]
    [SerializeField] private float windFallTime = 0.35f;

    [Header("Death")]
    [Min(0f)]
    [SerializeField] private float deadBackwardAcceleration = 1.5f;
    [Min(0f)]
    [SerializeField] private float deadMovementInfluence = 2f;
    [Min(0f)]
    [SerializeField] private float deadMaximumBackwardAcceleration = 6f;
    [Min(0f)]
    [SerializeField] private float deadLiftAcceleration = 0.5f;
    [Min(0.01f)]
    [SerializeField] private float deadWindResponseTime = 0.2f;
    [Range(0f, 1f)]
    [SerializeField] private float deadWorldVelocityScale;
    [Range(0f, 1f)]
    [SerializeField] private float deadWorldAccelerationScale;
    [SerializeField] private bool resetInvertedCapeWhileDead = true;
    [Min(0.05f)]
    [SerializeField] private float deadFlipCheckInterval = 0.2f;
    [Min(0f)]
    [SerializeField] private float deadFrontResetDistance = 0.05f;
    [Min(0f)]
    [SerializeField] private float deadFlipCheckDelay = 0.5f;

    private Vector3 _lastPosition;
    private Vector3 _currentAcceleration;
    private Vector3 _smoothVelocity;
    private Vector3 _currentWindDirection;
    private bool _hasPreviousPosition;
    private bool _wasDead;
    private float _aliveWorldVelocityScale;
    private float _aliveWorldAccelerationScale;
    private float _nextDeadFlipCheckTime;

    private void Reset()
    {
        targetCloth = GetComponent<Cloth>();
        movementRoot = transform.root;
    }

    private void Awake()
    {
        if (targetCloth == null)
        {
            targetCloth = GetComponent<Cloth>();
        }

        if (movementRoot == null)
        {
            movementRoot = transform.root;
        }

        if (playerStats == null)
        {
            playerStats = GetComponentInParent<PlayerStats>();
        }

        _aliveWorldVelocityScale = targetCloth.worldVelocityScale;
        _aliveWorldAccelerationScale = targetCloth.worldAccelerationScale;
    }

    private void OnEnable()
    {
        ResetMotion();
    }

    private void Update()
    {
        if (targetCloth == null || movementRoot == null)
        {
            return;
        }

        float deltaTime = Time.deltaTime;
        Vector3 currentPosition = movementRoot.position;
        if (!IsFinite(currentPosition) || !IsFinite(deltaTime) || deltaTime <= 0f || !_hasPreviousPosition)
        {
            ResetMotion();
            return;
        }

        bool isDead = playerStats != null && playerStats.IsDead;
        if (isDead != _wasDead)
        {
            _wasDead = isDead;
            _currentAcceleration = Vector3.zero;
            _smoothVelocity = Vector3.zero;
            ApplyWorldMotionTransfer(isDead);
            targetCloth.ClearTransformMotion();
            _nextDeadFlipCheckTime = Time.time + deadFlipCheckDelay;
        }

        if (isDead)
        {
            Vector3 deadVelocity = (currentPosition - _lastPosition) / deltaTime;
            deadVelocity.y = 0f;
            _lastPosition = currentPosition;
            float deadSpeed = IsFinite(deadVelocity)
                ? Mathf.Min(deadVelocity.magnitude, maximumMeasuredSpeed)
                : 0f;
            ApplyDeadState(deltaTime, deadSpeed);
            ResetInvertedDeadCape(currentPosition);
            return;
        }

        Vector3 positionDelta = currentPosition - _lastPosition;
        _lastPosition = currentPosition;
        if (!IsFinite(positionDelta) || positionDelta.sqrMagnitude > teleportDistance * teleportDistance)
        {
            ResetMotion();
            targetCloth.ClearTransformMotion();
            return;
        }

        Vector3 velocity = positionDelta / deltaTime;
        if (!IsFinite(velocity))
        {
            ResetMotion();
            return;
        }

        // 점프와 낙하가 수평 이동용 풍향을 뒤집지 않도록 평면 속도만 사용한다.
        velocity.y = 0f;
        float speed = Mathf.Min(velocity.magnitude, maximumMeasuredSpeed);
        Vector3 targetAcceleration = Vector3.zero;

        if (speed > minimumMoveSpeed)
        {
            float windAmount = Mathf.InverseLerp(minimumMoveSpeed, fullWindSpeed, speed);
            Vector3 moveDirection = velocity.normalized;
            Vector3 windDirection = -moveDirection;
            if (keepWindBehindCharacter)
            {
                Vector3 facingDirection = Vector3.ProjectOnPlane(movementRoot.forward, Vector3.up).normalized;
                if (facingDirection.sqrMagnitude > 0.0001f)
                {
                    Vector3 backDirection = -facingDirection;
                    windDirection = Vector3.Lerp(
                        backDirection,
                        windDirection,
                        movementDirectionInfluence).normalized;
                    windDirection = ClampDirectionFromBack(backDirection, windDirection);

                    float directionBlend = 1f - Mathf.Exp(-windDirectionSharpness * deltaTime);
                    _currentWindDirection = Vector3.Slerp(
                        _currentWindDirection,
                        windDirection,
                        directionBlend).normalized;
                    _currentWindDirection = ClampDirectionFromBack(backDirection, _currentWindDirection);
                    windDirection = _currentWindDirection;
                }
            }

            targetAcceleration =
                windDirection * (backwardAcceleration * windAmount) +
                Vector3.up * (liftAcceleration * windAmount);
        }

        float smoothTime = speed > minimumMoveSpeed ? windRiseTime : windFallTime;
        _currentAcceleration = Vector3.SmoothDamp(
            _currentAcceleration,
            targetAcceleration,
            ref _smoothVelocity,
            smoothTime,
            maximumAcceleration,
            deltaTime);

        if (!IsFinite(_currentAcceleration))
        {
            ResetMotion();
            targetCloth.ClearTransformMotion();
            return;
        }

        _currentAcceleration = Vector3.ClampMagnitude(_currentAcceleration, maximumAcceleration);
        targetCloth.externalAcceleration = _currentAcceleration;
    }

    private void OnDisable()
    {
        if (targetCloth != null)
        {
            targetCloth.externalAcceleration = Vector3.zero;
            targetCloth.worldVelocityScale = _aliveWorldVelocityScale;
            targetCloth.worldAccelerationScale = _aliveWorldAccelerationScale;
        }
    }

    private void ResetMotion()
    {
        if (movementRoot == null)
        {
            return;
        }

        Vector3 currentPosition = movementRoot.position;
        _lastPosition = IsFinite(currentPosition) ? currentPosition : Vector3.zero;
        _currentAcceleration = Vector3.zero;
        _smoothVelocity = Vector3.zero;
        Vector3 facingDirection = Vector3.ProjectOnPlane(movementRoot.forward, Vector3.up).normalized;
        _currentWindDirection = facingDirection.sqrMagnitude > 0.0001f
            ? -facingDirection
            : Vector3.back;
        _hasPreviousPosition = IsFinite(currentPosition);
        _wasDead = playerStats != null && playerStats.IsDead;
        ApplyWorldMotionTransfer(_wasDead);

        if (targetCloth != null)
        {
            targetCloth.externalAcceleration = Vector3.zero;
        }
    }

    private void ApplyDeadState(float deltaTime, float movementSpeed)
    {
        Vector3 facingDirection = Vector3.ProjectOnPlane(movementRoot.forward, Vector3.up).normalized;
        float backwardForce = Mathf.Min(
            deadMaximumBackwardAcceleration,
            deadBackwardAcceleration + movementSpeed * deadMovementInfluence);
        Vector3 targetAcceleration = Vector3.up * deadLiftAcceleration;
        if (facingDirection.sqrMagnitude > 0.0001f)
        {
            targetAcceleration -= facingDirection * backwardForce;
        }

        _currentAcceleration = Vector3.SmoothDamp(
            _currentAcceleration,
            targetAcceleration,
            ref _smoothVelocity,
            deadWindResponseTime,
            maximumAcceleration,
            deltaTime);

        targetCloth.externalAcceleration = _currentAcceleration;
    }

    private void ApplyWorldMotionTransfer(bool isDead)
    {
        if (targetCloth == null)
        {
            return;
        }

        targetCloth.worldVelocityScale = isDead
            ? deadWorldVelocityScale
            : _aliveWorldVelocityScale;
        targetCloth.worldAccelerationScale = isDead
            ? deadWorldAccelerationScale
            : _aliveWorldAccelerationScale;
    }

    private void ResetInvertedDeadCape(Vector3 playerPosition)
    {
        if (!resetInvertedCapeWhileDead || Time.time < _nextDeadFlipCheckTime)
        {
            return;
        }

        _nextDeadFlipCheckTime = Time.time + deadFlipCheckInterval;
        Vector3 facingDirection = Vector3.ProjectOnPlane(movementRoot.forward, Vector3.up).normalized;
        if (facingDirection.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        Vector3[] vertices = targetCloth.vertices;
        ClothSkinningCoefficient[] coefficients = targetCloth.coefficients;
        Vector3 capeCenter = Vector3.zero;
        int vertexCount = 0;
        int count = Mathf.Min(vertices.Length, coefficients.Length);
        for (int i = 0; i < count; i++)
        {
            if (coefficients[i].maxDistance <= 0.0001f)
            {
                continue;
            }

            Vector3 worldPosition = targetCloth.transform.TransformPoint(vertices[i]);
            if (!IsFinite(worldPosition))
            {
                ResetClothSimulation();
                return;
            }

            capeCenter += worldPosition;
            vertexCount++;
        }

        if (vertexCount == 0)
        {
            return;
        }

        capeCenter /= vertexCount;
        float distanceInFront = Vector3.Dot(capeCenter - playerPosition, facingDirection);
        if (distanceInFront > deadFrontResetDistance)
        {
            ResetClothSimulation();
        }
    }

    private void ResetClothSimulation()
    {
        targetCloth.enabled = false;
        targetCloth.enabled = true;
        targetCloth.ClearTransformMotion();
        ApplyWorldMotionTransfer(true);
        targetCloth.externalAcceleration = Vector3.zero;
        _currentAcceleration = Vector3.zero;
        _smoothVelocity = Vector3.zero;
        _nextDeadFlipCheckTime = Time.time + deadFlipCheckDelay;
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private static bool IsFinite(Vector3 value)
    {
        return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
    }

    private Vector3 ClampDirectionFromBack(Vector3 backDirection, Vector3 direction)
    {
        return Vector3.RotateTowards(
            backDirection,
            direction,
            maximumWindAngleFromBack * Mathf.Deg2Rad,
            0f).normalized;
    }
}
