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
            if (existing is AnimatorStateResetBehaviour existingReset)
            {
                // 도메인 리로드마다 같은 값을 다시 쓰고 전체 에셋을 저장하지 않는다.
                SerializedObject serializedReset = new SerializedObject(existingReset);
                SerializedProperty resetKey = serializedReset.FindProperty("resetKey");
                if (resetKey != null && resetKey.stringValue == "Crawling")
                {
                    return;
                }

                Undo.RecordObject(existingReset, "Configure Crawling State Reset");
                existingReset.Configure("Crawling");
                EditorUtility.SetDirty(existingReset);
                EditorUtility.SetDirty(controller);
                AssetDatabase.SaveAssets();
                return;
            }
        }

        AnimatorStateResetBehaviour behaviour =
            crawlingState.AddStateMachineBehaviour<AnimatorStateResetBehaviour>();
        Undo.RegisterCreatedObjectUndo(behaviour, "Add Crawling State Reset");
        behaviour.Configure("Crawling");
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
