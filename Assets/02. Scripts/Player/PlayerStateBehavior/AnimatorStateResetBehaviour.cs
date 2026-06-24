using UnityEngine;

public sealed class AnimatorStateResetBehaviour : AnimatorStateBehaviourBase
{
    [SerializeField] private string resetKey;

    public void Configure(string key)
    {
        resetKey = key ?? string.Empty;
    }

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        ForEachReceiver<IStateResetReceiver>(
            animator,
            receiver => receiver.ResetForAnimatorState(resetKey));
    }
}
