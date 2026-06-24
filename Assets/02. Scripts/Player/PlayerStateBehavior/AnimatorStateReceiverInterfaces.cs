public interface IActionLockStateReceiver
{
    void BeginActionAnimation(StateActionLockType lockType);
    void EndActionAnimation(StateActionLockType lockType);
}

public interface IComboInputStateReceiver
{
    void OpenComboInputWindow();
}

public interface IParryGuardStateReceiver
{
    void SetParryGuardActive(bool isActive);
    void EndParryGuardState();
}

public interface IInvincibilityStateReceiver
{
    void SetActionInvincible(bool isInvincible);
}

public interface ITurnStateReceiver
{
    void BeginTurnAnimationState();
    void EndTurnAnimationState();
}

public interface IRootMotionStateReceiver
{
    void SetAnimatorStateRootMotionActive(bool isActive);
}

public interface IStaminaRecoveryStateReceiver
{
    void DelayStaminaRecoveryAfterAnimation();
}

public interface IStateResetReceiver
{
    void ResetForAnimatorState(string resetKey);
}
