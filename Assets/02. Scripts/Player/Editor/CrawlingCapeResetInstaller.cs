using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

[InitializeOnLoad]
public static class CrawlingCapeResetInstaller
{
    private const string ControllerPath = "Assets/07. Animations/PlayerCtrl.controller";

    static CrawlingCapeResetInstaller()
    {
        EditorApplication.delayCall += Install;
    }

    private static void Install()
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            return;
        }

        AnimatorState crawlingState = FindState(controller, "Crawling");
        if (crawlingState == null)
        {
            return;
        }

        foreach (StateMachineBehaviour existing in crawlingState.behaviours)
        {
            if (existing is CrawlingCapeResetStateBehaviour)
            {
                return;
            }
        }

        CrawlingCapeResetStateBehaviour behaviour =
            crawlingState.AddStateMachineBehaviour<CrawlingCapeResetStateBehaviour>();
        EditorUtility.SetDirty(behaviour);
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
    }

    private static AnimatorState FindState(AnimatorController controller, string stateName)
    {
        for (int i = 0; i < controller.layers.Length; i++)
        {
            AnimatorState state = FindState(controller.layers[i].stateMachine, stateName);
            if (state != null)
            {
                return state;
            }
        }

        return null;
    }

    private static AnimatorState FindState(AnimatorStateMachine stateMachine, string stateName)
    {
        foreach (ChildAnimatorState childState in stateMachine.states)
        {
            if (childState.state != null && childState.state.name == stateName)
            {
                return childState.state;
            }
        }

        foreach (ChildAnimatorStateMachine childStateMachine in stateMachine.stateMachines)
        {
            AnimatorState state = FindState(childStateMachine.stateMachine, stateName);
            if (state != null)
            {
                return state;
            }
        }

        return null;
    }
}
