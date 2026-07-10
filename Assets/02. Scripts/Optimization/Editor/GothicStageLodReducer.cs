#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class GothicStageLodReducer
{
    private const string ComparisonScenePath = "Assets/01. Scenes/Gothic_Stage_LOD.unity";
    private const string OriginalScenePath = "Assets/01. Scenes/Gothic_Stage.unity";
    private static readonly Regex LodNamePattern =
        new Regex(@"(?:^|_)LOD([0-6])(?:$|\s|\()", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    [MenuItem("Tools/Optimization/Reduce Gothic Stage LODs")]
    public static void ReduceGothicStageLods()
    {
        ReduceScene(ComparisonScenePath);
    }

    [MenuItem("Tools/Optimization/Reduce Original Gothic Stage LODs")]
    public static void ReduceOriginalGothicStageLods()
    {
        ReduceScene(OriginalScenePath);
    }

    [MenuItem("Tools/Optimization/Keep Only LOD0 In Original Gothic Stage")]
    public static void KeepOnlyLodZeroInOriginalGothicStage()
    {
        KeepOnlyLodZero(OriginalScenePath);
    }

    // Entry point for a non-interactive Unity batch run.
    public static void KeepOnlyLodZeroInOriginalGothicStageBatch()
    {
        KeepOnlyLodZero(OriginalScenePath, false);
    }

    private static void ReduceScene(string scenePath)
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        LODGroup[] groups = Object.FindObjectsByType<LODGroup>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        int changedGroups = 0;
        int skippedGroups = 0;
        int removedObjects = 0;

        foreach (LODGroup group in groups)
        {
            LOD[] sourceLods = group.GetLODs();
            List<Renderer> lod0 = new List<Renderer>();
            List<Renderer> lod3 = new List<Renderer>();
            List<Renderer> lod6 = new List<Renderer>();
            HashSet<GameObject> unusedObjects = new HashSet<GameObject>();

            foreach (LOD sourceLod in sourceLods)
            {
                foreach (Renderer renderer in sourceLod.renderers)
                {
                    if (renderer == null)
                    {
                        continue;
                    }

                    int lodIndex = GetLodIndex(renderer.gameObject.name);
                    switch (lodIndex)
                    {
                        case 0:
                            lod0.Add(renderer);
                            break;
                        case 3:
                            lod3.Add(renderer);
                            break;
                        case 6:
                            lod6.Add(renderer);
                            break;
                        case 1:
                        case 2:
                        case 4:
                        case 5:
                            unusedObjects.Add(renderer.gameObject);
                            break;
                    }
                }
            }

            // 이름으로 세 단계가 모두 확인되는 정규 LODGroup만 변경한다.
            if (lod0.Count == 0 || lod3.Count == 0 || lod6.Count == 0)
            {
                skippedGroups++;
                continue;
            }

            Undo.RecordObject(group, "Reduce Gothic Stage LODs");
            group.SetLODs(new[]
            {
                new LOD(0.30f, lod0.ToArray()),
                new LOD(0.05f, lod3.ToArray()),
                // LOD6은 5%부터 1%까지 표시하고, 1% 아래는 Culled 구간으로 둔다.
                new LOD(0.01f, lod6.ToArray())
            });
            group.RecalculateBounds();
            EditorUtility.SetDirty(group);

            foreach (GameObject unusedObject in unusedObjects)
            {
                if (unusedObject == null)
                {
                    continue;
                }

                Undo.DestroyObjectImmediate(unusedObject);
                removedObjects++;
            }

            changedGroups++;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        Debug.Log(
            $"[GothicStageLodReducer] Complete: {scenePath}. " +
            $"Changed LODGroups={changedGroups}, " +
            $"Removed LOD objects={removedObjects}, " +
            $"Skipped groups={skippedGroups}.");
    }

    private static void KeepOnlyLodZero(string scenePath, bool askToSave = true)
    {
        if (askToSave && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        LODGroup[] groups = Object.FindObjectsByType<LODGroup>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        int changedGroups = 0;
        int removedObjects = 0;
        int skippedGroups = 0;

        foreach (LODGroup group in groups)
        {
            LOD[] sourceLods = group.GetLODs();
            if (sourceLods.Length == 0)
            {
                skippedGroups++;
                continue;
            }

            List<Renderer> lod0Renderers = new List<Renderer>();
            HashSet<GameObject> lowerLodObjects = new HashSet<GameObject>();

            foreach (LOD sourceLod in sourceLods)
            {
                foreach (Renderer renderer in sourceLod.renderers)
                {
                    if (renderer == null)
                    {
                        continue;
                    }

                    if (GetLodIndex(renderer.gameObject.name) == 0)
                    {
                        lod0Renderers.Add(renderer);
                    }
                    else
                    {
                        lowerLodObjects.Add(renderer.gameObject);
                    }
                }
            }

            // Some assets do not encode the level in renderer names. In that case,
            // the first configured LOD is the authoritative highest-detail level.
            if (lod0Renderers.Count == 0)
            {
                foreach (Renderer renderer in sourceLods[0].renderers)
                {
                    if (renderer != null)
                    {
                        lod0Renderers.Add(renderer);
                        lowerLodObjects.Remove(renderer.gameObject);
                    }
                }
            }

            if (lod0Renderers.Count == 0)
            {
                skippedGroups++;
                continue;
            }

            Undo.RecordObject(group, "Keep Only Gothic LOD0");
            group.SetLODs(new[]
            {
                // Keep LOD0 visible until the object is effectively sub-pixel sized.
                new LOD(0.0001f, lod0Renderers.ToArray())
            });
            group.RecalculateBounds();
            EditorUtility.SetDirty(group);

            foreach (GameObject lowerLodObject in lowerLodObjects)
            {
                if (lowerLodObject == null || lod0Renderers.Exists(r => r != null && r.gameObject == lowerLodObject))
                {
                    continue;
                }

                Undo.DestroyObjectImmediate(lowerLodObject);
                removedObjects++;
            }

            changedGroups++;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        Debug.Log(
            $"[GothicStageLodReducer] LOD0-only complete: {scenePath}. " +
            $"Changed LODGroups={changedGroups}, " +
            $"Removed lower-LOD objects={removedObjects}, " +
            $"Skipped groups={skippedGroups}.");
    }

    [MenuItem("Tools/Optimization/Set Gothic Stage Culled To 1 Percent")]
    public static void SetGothicStageCulledToOnePercent()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        Scene scene = EditorSceneManager.OpenScene(ComparisonScenePath, OpenSceneMode.Single);
        LODGroup[] groups = Object.FindObjectsByType<LODGroup>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        int changedGroups = 0;
        foreach (LODGroup group in groups)
        {
            LOD[] lods = group.GetLODs();
            if (lods.Length == 0)
            {
                continue;
            }

            int lastIndex = lods.Length - 1;
            if (Mathf.Approximately(lods[lastIndex].screenRelativeTransitionHeight, 0.01f))
            {
                continue;
            }

            Undo.RecordObject(group, "Set Gothic Stage Culled To 1 Percent");
            lods[lastIndex].screenRelativeTransitionHeight = 0.01f;
            group.SetLODs(lods);
            EditorUtility.SetDirty(group);
            changedGroups++;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        Debug.Log(
            $"[GothicStageLodReducer] Culled boundary set to 1%. " +
            $"Changed LODGroups={changedGroups}.");
    }

    private static int GetLodIndex(string objectName)
    {
        Match match = LodNamePattern.Match(objectName);
        return match.Success && int.TryParse(match.Groups[1].Value, out int index)
            ? index
            : -1;
    }
}
#endif
