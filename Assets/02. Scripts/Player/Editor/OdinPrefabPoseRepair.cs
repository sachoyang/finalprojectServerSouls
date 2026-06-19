using UnityEditor;
using UnityEngine;

public static class OdinPrefabPoseRepair
{
    private const string PlayerPrefabPath = "Assets/06. Prefabs/Player2.prefab";
    private const string OdinModelPath = "Assets/05. Models/OdinPlayer/Model/Odin XVI.fbx";
    private const int AccidentalOverrideThreshold = 10;

    [MenuItem("Tools/Player/Restore Odin Prefab Bind Pose")]
    public static void RepairAccidentalPoseOverrides()
    {
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        if (prefabRoot == null)
        {
            return;
        }

        try
        {
            Transform[] transforms = prefabRoot.GetComponentsInChildren<Transform>(true);
            int overrideCount = CountPoseOverrides(transforms);
            if (overrideCount < AccidentalOverrideThreshold)
            {
                return;
            }

            for (int i = 0; i < transforms.Length; i++)
            {
                RevertPoseOverrides(transforms[i]);
            }

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, PlayerPrefabPath);
            Debug.Log($"Restored Odin bind pose by removing {overrideCount} accidental transform overrides.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    private static int CountPoseOverrides(Transform[] transforms)
    {
        int count = 0;
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform transform = transforms[i];
            if (!IsOdinModelTransform(transform))
            {
                continue;
            }

            SerializedObject serializedTransform = new SerializedObject(transform);
            SerializedProperty position = serializedTransform.FindProperty("m_LocalPosition");
            SerializedProperty rotation = serializedTransform.FindProperty("m_LocalRotation");
            if (position != null && position.prefabOverride)
            {
                count++;
            }

            if (rotation != null && rotation.prefabOverride)
            {
                count++;
            }
        }

        return count;
    }

    private static void RevertPoseOverrides(Transform transform)
    {
        if (!IsOdinModelTransform(transform))
        {
            return;
        }

        SerializedObject serializedTransform = new SerializedObject(transform);
        SerializedProperty position = serializedTransform.FindProperty("m_LocalPosition");
        SerializedProperty rotation = serializedTransform.FindProperty("m_LocalRotation");

        if (position != null && position.prefabOverride)
        {
            PrefabUtility.RevertPropertyOverride(position, InteractionMode.AutomatedAction);
        }

        if (rotation != null && rotation.prefabOverride)
        {
            PrefabUtility.RevertPropertyOverride(rotation, InteractionMode.AutomatedAction);
        }
    }

    private static bool IsOdinModelTransform(Transform transform)
    {
        Object source = PrefabUtility.GetCorrespondingObjectFromSource(transform);
        return source != null && AssetDatabase.GetAssetPath(source) == OdinModelPath;
    }
}
