#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

public sealed class GothicFbxLodStripper : AssetPostprocessor
{
    private const string GothicMeshFolder =
        "Assets/Gothic_Map/Gothic_Interior/Environment/Asset/Mesh";

    private static readonly Regex RemovedLodNamePattern = new Regex(
        @"(?:^|_)(?:LOD(?:1|2|4|5)|ConvexHulls)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private void OnPostprocessModel(GameObject root)
    {
        if (!IsTargetFbx(assetPath))
        {
            return;
        }

        List<GameObject> objectsToRemove = new List<GameObject>();
        CollectUnusedLodObjects(root.transform, objectsToRemove);

        foreach (GameObject objectToRemove in objectsToRemove)
        {
            UnityEngine.Object.DestroyImmediate(objectToRemove);
        }
    }

    [MenuItem("Tools/Optimization/Reimport Gothic FBXs With Reduced LODs")]
    private static void ReimportGothicFbxFiles()
    {
        string[] modelGuids = AssetDatabase.FindAssets(
            "t:Model",
            new[] { GothicMeshFolder });
        List<string> fbxPaths = new List<string>();

        foreach (string guid in modelGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (IsTargetFbx(path))
            {
                fbxPaths.Add(path);
            }
        }

        if (!EditorUtility.DisplayDialog(
                "Reimport Gothic FBXs",
                $"{fbxPaths.Count} FBX files will be reimported.\n" +
                "LOD1, LOD2, LOD4, LOD5 and ConvexHulls nodes will be excluded " +
                "from Unity imports.\n\n" +
                "This can take several minutes.",
                "Reimport",
                "Cancel"))
        {
            return;
        }

        try
        {
            for (int index = 0; index < fbxPaths.Count; index++)
            {
                string path = fbxPaths[index];
                if (EditorUtility.DisplayCancelableProgressBar(
                        "Reimporting Gothic FBXs",
                        path,
                        (float)index / fbxPaths.Count))
                {
                    Debug.LogWarning(
                        $"[GothicFbxLodStripper] Reimport cancelled after {index} files.");
                    return;
                }

                AssetDatabase.ImportAsset(
                    path,
                    ImportAssetOptions.ForceUpdate);
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            AssetDatabase.SaveAssets();
        }

        Debug.Log(
            $"[GothicFbxLodStripper] Reimport complete. FBX files={fbxPaths.Count}. " +
            "Imported LODs=0,3,6.");
    }

    private static void CollectUnusedLodObjects(
        Transform parent,
        ICollection<GameObject> objectsToRemove)
    {
        for (int index = 0; index < parent.childCount; index++)
        {
            Transform child = parent.GetChild(index);
            CollectUnusedLodObjects(child, objectsToRemove);

            if (RemovedLodNamePattern.IsMatch(child.name))
            {
                objectsToRemove.Add(child.gameObject);
            }
        }
    }

    private static bool IsTargetFbx(string path)
    {
        return path.StartsWith(
                   GothicMeshFolder + "/",
                   StringComparison.OrdinalIgnoreCase) &&
               string.Equals(
                   Path.GetExtension(path),
                   ".fbx",
                   StringComparison.OrdinalIgnoreCase);
    }
}
#endif
