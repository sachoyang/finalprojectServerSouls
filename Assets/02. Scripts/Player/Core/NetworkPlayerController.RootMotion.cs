using UnityEngine;

public partial class NetworkPlayerController
{
    private void OnAnimatorMove()
    {
        if (animator == null ||
            Object == null ||
            !Object.HasStateAuthority ||
            (!IsTurnAnimationActive() && !IsForwardJumpRootMotionActive()))
        {
            return;
        }

        _queuedRootMotionDeltaPosition += animator.deltaPosition;
        _queuedRootMotionDeltaRotation = animator.deltaRotation * _queuedRootMotionDeltaRotation;
        _hasQueuedRootMotion = true;
    }

    private void ApplyTurnRootMotion()
    {
        StopHorizontalVelocity();

        if (_hasQueuedRootMotion)
        {
            Vector3 planarDelta = Vector3.ProjectOnPlane(_queuedRootMotionDeltaPosition, Vector3.up);
            if (planarDelta.sqrMagnitude > 0.000001f && Runner != null && Runner.DeltaTime > 0f)
            {
                _networkCharacterController.maxSpeed = planarDelta.magnitude / Runner.DeltaTime;
                _networkCharacterController.acceleration = movementAcceleration;
                _networkCharacterController.braking = movementBraking;
                _networkCharacterController.rotationSpeed = 0f;
                _networkCharacterController.Move(planarDelta.normalized);
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

        _networkCharacterController.maxSpeed = planarDelta.magnitude / Runner.DeltaTime;
        _networkCharacterController.acceleration = movementAcceleration;
        _networkCharacterController.braking = movementBraking;
        _networkCharacterController.rotationSpeed = _networkControllerRotationSpeed;
        _networkCharacterController.Move(jumpDirection);
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
