using UnityEngine;

public sealed class CrawlingCapeResetStateBehaviour : StateMachineBehaviour
{
    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        CapeSpringBone springCape = animator.GetComponentInParent<CapeSpringBone>(true);
        if (springCape != null && springCape.isActiveAndEnabled)
        {
            springCape.ResetForCrawlingState();
        }
    }
}
