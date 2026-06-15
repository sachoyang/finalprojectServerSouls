using UnityEngine;

public class PlayerTurnStateBehaviour : StateMachineBehaviour
{
    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        GetController(animator)?.BeginTurnAnimationState();
    }

    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        GetController(animator)?.EndTurnAnimationState();
    }

    private static NetworkPlayerController GetController(Animator animator)
    {
        return animator != null ? animator.GetComponentInParent<NetworkPlayerController>() : null;
    }
}
