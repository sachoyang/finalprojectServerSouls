using UnityEngine;

public sealed class GuardWindowStateBehaviour : AnimatorStateWindowBehaviour
{
    protected override void OnWindowChanged(Animator animator, bool isActive)
    {
        if (isActive)
        {
            ForEachReceiver<IParryGuardStateReceiver>(
                animator,
                receiver => receiver.SetParryGuardActive(true));
            return;
        }

        ForEachReceiver<IParryGuardStateReceiver>(
            animator,
            receiver => receiver.EndParryGuardState());
    }
}
