using UnityEditor;
using UnityEngine;

public sealed class InvalidSkinnedMeshBoundsSaveGuard : AssetModificationProcessor
{
    private static string[] OnWillSaveAssets(string[] paths)
    {
        for (int i = 0; i < paths.Length; i++)
        {
            string path = paths[i];
            if (!path.EndsWith(".prefab"))
            {
                continue;
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                continue;
            }

            SkinnedMeshRenderer[] renderers = prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                RepairBounds(renderers[rendererIndex], path);
            }
        }

        return paths;
    }

    private static void RepairBounds(SkinnedMeshRenderer renderer, string assetPath)
    {
        if (renderer.GetComponent<Cloth>() == null)
        {
            return;
        }

        SerializedObject serializedRenderer = new SerializedObject(renderer);
        SerializedProperty centerProperty = serializedRenderer.FindProperty("m_AABB.m_Center");
        SerializedProperty extentProperty = serializedRenderer.FindProperty("m_AABB.m_Extent");
        if (centerProperty == null || extentProperty == null)
        {
            return;
        }

        Vector3 center = centerProperty.vector3Value;
        Vector3 extents = extentProperty.vector3Value;
        if (IsFinite(center) && IsFinite(extents))
        {
            return;
        }

        Mesh mesh = renderer.sharedMesh;
        Bounds repairedBounds = mesh != null ? mesh.bounds : new Bounds(Vector3.zero, Vector3.one);
        repairedBounds.Expand(repairedBounds.size * 0.25f);
        centerProperty.vector3Value = repairedBounds.center;
        extentProperty.vector3Value = repairedBounds.extents;
        serializedRenderer.ApplyModifiedPropertiesWithoutUndo();
        renderer.localBounds = repairedBounds;
    }

    private static bool IsFinite(Vector3 value)
    {
        return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
