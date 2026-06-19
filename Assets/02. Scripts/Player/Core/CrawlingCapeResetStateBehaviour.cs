using UnityEngine;

public sealed class CrawlingCapeResetStateBehaviour : StateMachineBehaviour
{
    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        CapeClothWind cape = animator.GetComponentInChildren<CapeClothWind>(true);
        cape?.ResetForCrawlingState();
    }
}
