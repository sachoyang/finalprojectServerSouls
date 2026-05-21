using UnityEngine;

public class PlayerActionStateBehaviour : StateMachineBehaviour
{
    [SerializeField] private bool opensComboInput;
    [SerializeField, Range(0f, 1f)] private float comboInputOpenNormalizedTime = 0.5f;

    private bool _comboInputOpened;

    public void Configure(bool shouldOpenComboInput, float openNormalizedTime)
    {
        opensComboInput = shouldOpenComboInput;
        comboInputOpenNormalizedTime = Mathf.Clamp01(openNormalizedTime);
    }

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        _comboInputOpened = false;
        GetController(animator)?.BeginActionAnimation();
    }

    public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (!opensComboInput || _comboInputOpened)
        {
            return;
        }

        if (stateInfo.normalizedTime < comboInputOpenNormalizedTime)
        {
            return;
        }

        _comboInputOpened = true;
        GetController(animator)?.OpenComboInputWindow();
    }

    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        _comboInputOpened = false;
        GetController(animator)?.EndActionAnimation();
    }

    private static NetworkPlayerController GetController(Animator animator)
    {
        return animator != null ? animator.GetComponentInParent<NetworkPlayerController>() : null;
    }
}
