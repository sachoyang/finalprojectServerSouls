using UnityEngine;

public sealed class TurnMotionStateBehaviour : AnimatorStateBehaviourBase
{
    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        ForEachReceiver<ITurnStateReceiver>(
            animator,
            receiver => receiver.BeginTurnAnimationState());
    }

    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        ForEachReceiver<ITurnStateReceiver>(
            animator,
            receiver => receiver.EndTurnAnimationState());
    }
}
