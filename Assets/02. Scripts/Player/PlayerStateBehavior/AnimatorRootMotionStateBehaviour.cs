using UnityEngine;

public sealed class AnimatorRootMotionStateBehaviour : AnimatorStateBehaviourBase
{
    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        ForEachReceiver<IRootMotionStateReceiver>(
            animator,
            receiver => receiver.SetAnimatorStateRootMotionActive(true));
    }

    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        ForEachReceiver<IRootMotionStateReceiver>(
            animator,
            receiver => receiver.SetAnimatorStateRootMotionActive(false));
    }
}
