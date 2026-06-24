using UnityEngine;

public sealed class StaminaRecoveryDelayStateBehaviour : AnimatorStateBehaviourBase
{
    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        ForEachReceiver<IStaminaRecoveryStateReceiver>(
            animator,
            receiver => receiver.DelayStaminaRecoveryAfterAnimation());
    }
}
