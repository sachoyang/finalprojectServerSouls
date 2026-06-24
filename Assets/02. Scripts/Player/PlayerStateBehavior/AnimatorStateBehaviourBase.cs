using System;
using UnityEngine;

public abstract class AnimatorStateBehaviourBase : StateMachineBehaviour
{
    protected static void ForEachReceiver<T>(Animator animator, Action<T> callback)
        where T : class
    {
        if (animator == null || callback == null)
        {
            return;
        }

        MonoBehaviour[] components = animator.GetComponentsInParent<MonoBehaviour>(true);
        foreach (MonoBehaviour component in components)
        {
            if (component is T receiver)
            {
                callback(receiver);
            }
        }
    }

    protected static float GetNormalizedTime(AnimatorStateInfo stateInfo)
    {
        return stateInfo.loop
            ? stateInfo.normalizedTime - Mathf.Floor(stateInfo.normalizedTime)
            : Mathf.Clamp01(stateInfo.normalizedTime);
    }
}
