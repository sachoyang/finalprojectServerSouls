using UnityEngine;

public sealed class ComboInputWindowStateBehaviour : AnimatorStateBehaviourBase
{
    [SerializeField, Range(0f, 1f)] private float openNormalizedTime = 0.72f;

    private bool _opened;

    public void Configure(float normalizedTime)
    {
        openNormalizedTime = Mathf.Clamp01(normalizedTime);
    }

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        _opened = false;
    }

    public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (_opened || GetNormalizedTime(stateInfo) < openNormalizedTime)
        {
            return;
        }

        _opened = true;
        ForEachReceiver<IComboInputStateReceiver>(
            animator,
            receiver => receiver.OpenComboInputWindow());
    }

    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        _opened = false;
    }
}
