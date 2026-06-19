using UnityEngine;

public partial class NetworkPlayerController
{
    private void LateUpdate()
    {
        UpdateAnimatorRootMotionMode();
    }

    private void OnAnimatorMove()
    {
        UpdateAnimatorRootMotionMode();
        if (animator == null ||
            Object == null ||
            !Object.HasStateAuthority ||
            !ShouldUseAnimatorRootMotion())
        {
            ClearQueuedRootMotion();
            return;
        }

        _queuedRootMotionDeltaPosition += animator.deltaPosition;
        _queuedRootMotionDeltaRotation = animator.deltaRotation * _queuedRootMotionDeltaRotation;
        _hasQueuedRootMotion = true;
    }

    private bool ShouldUseAnimatorRootMotion()
    {
        return Object != null &&
               Object.HasStateAuthority &&
               (IsTurnAnimationActive() ||
                IsForwardJumpRootMotionActive() ||
                IsRollRootMotionActive() ||
                IsSkillRootMotionActive());
    }

    private void UpdateAnimatorRootMotionMode()
    {
        if (animator == null)
        {
            return;
        }

        animator.applyRootMotion = ShouldUseAnimatorRootMotion();
    }

    private void ApplyTurnRootMotion()
    {
        StopHorizontalVelocity();

        if (_hasQueuedRootMotion)
        {
            Vector3 planarDelta = Vector3.ProjectOnPlane(_queuedRootMotionDeltaPosition, Vector3.up);
            if (planarDelta.sqrMagnitude > 0.000001f && Runner != null && Runner.DeltaTime > 0f)
            {
                ApplyPlanarRootMotionDelta(planarDelta, planarDelta.normalized, 0f, false);
            }

            bool appliedRootYaw = ApplyRootMotionYaw(_queuedRootMotionDeltaRotation);
            ClearQueuedRootMotion();
            if (appliedRootYaw)
            {
                _turnUsedRootMotionRotation = true;
                return;
            }
        }

        ApplyTurnFallbackRotation();
    }

    private void ApplyJumpRootMotion()
    {
        if (!_hasQueuedRootMotion)
        {
            return;
        }

        Vector3 planarDelta = Vector3.ProjectOnPlane(_queuedRootMotionDeltaPosition, Vector3.up);
        ClearQueuedRootMotion();
        if (planarDelta.sqrMagnitude <= 0.000001f || Runner == null || Runner.DeltaTime <= 0f)
        {
            return;
        }

        Vector3 jumpDirection = ForwardJumpDirection.sqrMagnitude > 0.001f
            ? ForwardJumpDirection.normalized
            : Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        if (jumpDirection.sqrMagnitude <= 0.001f)
        {
            return;
        }

        ApplyPlanarRootMotionDelta(planarDelta, jumpDirection, _networkControllerRotationSpeed, true);
    }

    private void ApplyRollRootMotion()
    {
        StopHorizontalVelocity();

        if (!_hasQueuedRootMotion)
        {
            return;
        }

        Vector3 planarDelta = Vector3.ProjectOnPlane(_queuedRootMotionDeltaPosition, Vector3.up);
        ClearQueuedRootMotion();
        if (planarDelta.sqrMagnitude <= 0.000001f || Runner == null || Runner.DeltaTime <= 0f)
        {
            return;
        }

        ApplyPlanarRootMotionDelta(planarDelta, planarDelta.normalized, 0f, false);
    }

    private void ApplySkillRootMotion()
    {
        StopHorizontalVelocity();

        if (!_hasQueuedRootMotion)
        {
            return;
        }

        Vector3 planarDelta = Vector3.ProjectOnPlane(_queuedRootMotionDeltaPosition, Vector3.up);
        ClearQueuedRootMotion();
        if (planarDelta.sqrMagnitude <= 0.000001f || Runner == null || Runner.DeltaTime <= 0f)
        {
            return;
        }

        ApplyPlanarRootMotionDelta(planarDelta, planarDelta.normalized, 0f, false);
    }

    private void ApplyPlanarRootMotionDelta(Vector3 planarDelta, Vector3 moveDirection, float rotationSpeedOverride, bool applyGravity)
{
    if (Runner == null || Runner.DeltaTime <= 0f || planarDelta.sqrMagnitude <= 0.000001f)
    {
        return;
    }

    Vector3 flatDirection = Vector3.ProjectOnPlane(moveDirection, Vector3.up);
    if (flatDirection.sqrMagnitude <= 0.001f)
    {
        flatDirection = planarDelta;
    }

    flatDirection.Normalize();
    Vector3 requestedDelta = flatDirection * planarDelta.magnitude;

    if (!TryMoveCharacterByRootDelta(requestedDelta, applyGravity))
    {
        float rootSpeed = planarDelta.magnitude / Runner.DeltaTime;
        Vector3 velocity = _networkCharacterController.Velocity;
        velocity.x = flatDirection.x * rootSpeed;
        velocity.z = flatDirection.z * rootSpeed;
        _networkCharacterController.Velocity = velocity;

        _networkCharacterController.maxSpeed = rootSpeed;
        _networkCharacterController.acceleration = 0f;
        _networkCharacterController.braking = movementBraking;
        _networkCharacterController.rotationSpeed = rotationSpeedOverride;
        _networkCharacterController.Move(flatDirection);
        return;
    }

    if (rotationSpeedOverride > 0f)
    {
        RotateTowards(flatDirection, rotationSpeedOverride);
    }
}

    private bool TryMoveCharacterByRootDelta(Vector3 planarDelta, bool applyGravity)
    {
        if (_characterController == null || !_characterController.enabled)
        {
            return false;
        }

        float deltaTime = Runner.DeltaTime;
        Vector3 previousPosition = transform.position;
        Vector3 velocity = _networkCharacterController.Velocity;
        Vector3 requestedDelta = planarDelta;
        if (applyGravity)
        {
            if (_networkCharacterController.Grounded && velocity.y < 0f)
            {
                velocity.y = 0f;
            }

            velocity.y += _networkCharacterController.gravity * deltaTime;
            requestedDelta += Vector3.up * velocity.y * deltaTime;
        }
        else
        {
            velocity.y = 0f;
        }

        _characterController.Move(requestedDelta);

        Vector3 actualDelta = transform.position - previousPosition;
        Vector3 actualVelocity = actualDelta / deltaTime;
        if (!applyGravity)
        {
            actualVelocity.y = 0f;
        }

        _networkCharacterController.Velocity = actualVelocity;
        _networkCharacterController.Grounded = _characterController.isGrounded;
        return true;
    }

    private bool ApplyRootMotionYaw(Quaternion deltaRotation)
    {
        deltaRotation.ToAngleAxis(out float angle, out Vector3 axis);
        if (float.IsNaN(angle) || axis.sqrMagnitude <= 0.001f)
        {
            return false;
        }

        if (angle > 180f)
        {
            angle -= 360f;
        }

        float yaw = Vector3.Dot(axis.normalized, Vector3.up) * angle;
        if (Mathf.Abs(yaw) <= 0.01f)
        {
            return false;
        }

        transform.rotation = Quaternion.AngleAxis(yaw, Vector3.up) * transform.rotation;
        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        if (forward.sqrMagnitude > 0.001f)
        {
            _lastMoveDirection = forward.normalized;
        }

        return true;
    }

    private void ApplyTurnFallbackRotation()
    {
        if (Runner == null)
        {
            return;
        }

        RotateTurnTowardsTarget(turnRootMotionFallbackRotationSpeed * Runner.DeltaTime);
    }

    private void RotateTurnTowardsTarget(float maxDegrees)
    {
        if (TurnTargetDirection.sqrMagnitude <= 0.001f || maxDegrees <= 0f)
        {
            return;
        }

        Vector3 currentForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        Vector3 targetForward = Vector3.ProjectOnPlane(TurnTargetDirection, Vector3.up);
        if (currentForward.sqrMagnitude <= 0.001f || targetForward.sqrMagnitude <= 0.001f)
        {
            return;
        }

        currentForward.Normalize();
        targetForward.Normalize();

        float remainingAngle = Vector3.Angle(currentForward, targetForward);
        if (remainingAngle <= 0.25f)
        {
            transform.rotation = Quaternion.LookRotation(targetForward, Vector3.up);
            _lastMoveDirection = targetForward;
            return;
        }

        float signedRemaining = Vector3.SignedAngle(currentForward, targetForward, Vector3.up);
        float configuredDirection = Mathf.Sign(Mathf.Approximately(turnAnimationYawDirection, 0f)
            ? 1f
            : turnAnimationYawDirection);
        float rotateDirection = Mathf.Abs(signedRemaining) >= 170f
            ? configuredDirection
            : Mathf.Sign(signedRemaining);

        if (Mathf.Approximately(rotateDirection, 0f))
        {
            rotateDirection = configuredDirection;
        }

        float step = Mathf.Min(maxDegrees, remainingAngle);
        transform.rotation = Quaternion.AngleAxis(step * rotateDirection, Vector3.up) * transform.rotation;

        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        if (forward.sqrMagnitude > 0.001f)
        {
            _lastMoveDirection = forward.normalized;
        }
    }

    private void ClearQueuedRootMotion()
    {
        _queuedRootMotionDeltaPosition = Vector3.zero;
        _queuedRootMotionDeltaRotation = Quaternion.identity;
        _hasQueuedRootMotion = false;
    }
}
