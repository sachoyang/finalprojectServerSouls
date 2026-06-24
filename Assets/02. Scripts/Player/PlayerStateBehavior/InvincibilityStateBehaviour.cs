using UnityEngine;

public sealed class InvincibilityStateBehaviour : AnimatorStateWindowBehaviour
{
    protected override void OnWindowChanged(Animator animator, bool isActive)
    {
        ForEachReceiver<IInvincibilityStateReceiver>(
            animator,
            receiver => receiver.SetActionInvincible(isActive));
    }
}
