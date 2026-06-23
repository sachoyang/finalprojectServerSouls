using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class GothicCategoryPrefabBuilder
{
    private const string OutputFolder =
        "Assets/Gothic_Map/Gothic_Interior/Environment/Asset/CategoryPrefabs";

    private static readonly CategoryDefinition[] Categories =
    {
        new("Static_Architecture", "Gothic_Static_Architecture.prefab", KeepColliders,
            "arch", "wall", "floor", "pillar", "stair", "ceiling", "trim", "baseboard", "railing", "window", "glass"),
        new("WoodDebris", "Gothic_WoodDebris.prefab", StripColliders,
            "wooddebris", "wood_debris"),
        new("Props", "Gothic_Props.prefab", KeepColliders,
            "chandelier", "candelabra", "candleabra", "statue", "skull", "bone", "chain", "carpet", "door", "ornement", "ornament",
            "decoration", "spiderweb", "wood", "globe", "holder"),
        new("Candles", "Gothic_Candles.prefab", StripColliders,
            "candle_cluster", "candlecluster", "thincandle", "thickcandle", "wax"),
        new("Debris", "Gothic_Debris.prefab", StripColliders,
            "debris", "broken", "glassdebris", "stonedebris"),
        new("BooksPapers", "Gothic_BooksPapers.prefab", StripColliders,
            "book", "paper", "scroll", "quill", "inkbottle"),
        new("Furniture", "Gothic_Furniture.prefab", KeepColliders,
            "chair", "table", "cabinet", "bench", "cupboard", "pedestal", "chest"),
        new("FakeCloud", "Gothic_FakeCloud.prefab", StripColliders,
            "fake_cloud", "fakecloud"),
        new("Other", "Gothic_Other.prefab", KeepColliders)
    };

    private const bool KeepColliders = true;
    private const bool StripColliders = false;

    [MenuItem("Tools/Gothic Map/Rebuild Scene As Category Prefabs")]
    public static void RebuildSceneAsCategoryPrefabs()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid())
        {
            Debug.LogError("[GothicCategoryPrefabBuilder] Active scene is not valid.");
            return;
        }

        List<GameObject> chunkRoots = FindChunkRoots(activeScene);
        if (chunkRoots.Count == 0)
        {
            RepairExistingCategoryPrefabs();
            return;
        }

        if (!EditorUtility.DisplayDialog(
                "Rebuild Gothic Category Prefabs",
                $"Found {chunkRoots.Count} Gothic_MeshChunk roots.\n\n" +
                "This will create category prefab instances and remove the old chunk roots from the open scene.\n" +
                "Furniture and Props keep colliders. Candles, Debris, and Books/Papers lose colliders.",
                "Rebuild",
                "Cancel"))
        {
            return;
        }

        Directory.CreateDirectory(OutputFolder);
        AssetDatabase.Refresh();

        Dictionary<CategoryDefinition, GameObject> categoryRoots = CreateCategoryRoots();
        Dictionary<CategoryDefinition, int> movedCounts = new();

        foreach (GameObject chunkRoot in chunkRoots)
        {
            int childCount = chunkRoot.transform.childCount;
            for (int i = 0; i < childCount; i++)
            {
                Transform child = chunkRoot.transform.GetChild(i);
                CategoryDefinition category = Classify(child.gameObject);
                GameObject clone = UnityEngine.Object.Instantiate(child.gameObject);
                clone.name = child.gameObject.name;
                clone.transform.SetParent(categoryRoots[category].transform, true);
                ApplyCategoryPolicy(clone, category);
                movedCounts.TryGetValue(category, out int count);
                movedCounts[category] = count + 1;
            }
        }

        foreach (CategoryDefinition category in Categories)
        {
            GameObject root = categoryRoots[category];
            if (root.transform.childCount == 0)
            {
                UnityEngine.Object.DestroyImmediate(root);
                continue;
            }

            string prefabPath = $"{OutputFolder}/{category.PrefabName}";
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefabAsset == null)
            {
                Debug.LogError($"[GothicCategoryPrefabBuilder] Failed to load saved prefab: {prefabPath}");
                continue;
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefabAsset, activeScene);
            instance.name = root.name;
            instance.transform.SetPositionAndRotation(root.transform.position, root.transform.rotation);
            instance.transform.localScale = root.transform.localScale;
            GameObjectUtility.SetStaticEditorFlags(instance, GameObjectUtility.GetStaticEditorFlags(root));
            UnityEngine.Object.DestroyImmediate(root);
        }

        foreach (GameObject chunkRoot in chunkRoots)
        {
            UnityEngine.Object.DestroyImmediate(chunkRoot);
        }

        EditorSceneManager.MarkSceneDirty(activeScene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(BuildReport(chunkRoots.Count, movedCounts));
    }

    private static void RepairExistingCategoryPrefabs()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid())
        {
            Debug.LogError("[GothicCategoryPrefabBuilder] Active scene is not valid.");
            return;
        }

        if (!EditorUtility.DisplayDialog(
                "Repair Gothic Category Prefabs",
                "No Gothic_MeshChunk roots were found in the active scene.\n\n" +
                "Repair existing Gothic category prefabs instead?\n" +
                "This reapplies cleanup rules and adds missing category prefab instances to the open scene.",
                "Repair Prefabs",
                "Cancel"))
        {
            return;
        }

        int repairedCount = 0;
        int instantiatedCount = 0;
        SplitExistingOtherFakeCloudPrefab();

        foreach (CategoryDefinition category in Categories)
        {
            string prefabPath = $"{OutputFolder}/{category.PrefabName}";
            if (!File.Exists(prefabPath))
            {
                continue;
            }

            GameObject root;
            try
            {
                root = PrefabUtility.LoadPrefabContents(prefabPath);
            }
            catch (Exception exception)
            {
                Debug.LogError($"[GothicCategoryPrefabBuilder] Failed to load prefab for repair: {prefabPath}\n{exception.Message}");
                continue;
            }

            try
            {
                ApplyCategoryPolicy(root, category);
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            repairedCount++;

            if (!SceneHasCategoryRoot(activeScene, category))
            {
                GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefabAsset == null)
                {
                    Debug.LogError($"[GothicCategoryPrefabBuilder] Failed to load saved prefab: {prefabPath}");
                    continue;
                }

                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefabAsset, activeScene);
                instance.name = $"Gothic_{category.Name}";
                instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                instance.transform.localScale = Vector3.one;
                instantiatedCount++;
            }
        }

        EditorSceneManager.MarkSceneDirty(activeScene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[GothicCategoryPrefabBuilder] Repaired {repairedCount} existing category prefabs and added {instantiatedCount} missing scene instances.");
    }

    private static bool SceneHasCategoryRoot(Scene scene, CategoryDefinition category)
    {
        string rootName = $"Gothic_{category.Name}";
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == rootName)
            {
                if (PrefabUtility.GetPrefabInstanceStatus(root) == PrefabInstanceStatus.MissingAsset)
                {
                    UnityEngine.Object.DestroyImmediate(root);
                    return false;
                }

                return true;
            }
        }

        return false;
    }

    private static void SplitExistingOtherFakeCloudPrefab()
    {
        CategoryDefinition otherCategory = GetCategory("Other");
        CategoryDefinition fakeCloudCategory = GetCategory("FakeCloud");
        string otherPath = $"{OutputFolder}/{otherCategory.PrefabName}";
        string fakeCloudPath = $"{OutputFolder}/{fakeCloudCategory.PrefabName}";
        if (!File.Exists(otherPath))
        {
            return;
        }

        GameObject otherRoot = null;
        GameObject fakeCloudRoot = null;
        try
        {
            otherRoot = PrefabUtility.LoadPrefabContents(otherPath);
            List<Transform> fakeCloudChildren = FindDirectCategoryChildren(otherRoot, fakeCloudCategory);
            if (fakeCloudChildren.Count == 0)
            {
                return;
            }

            fakeCloudRoot = new GameObject("Gothic_FakeCloud");
            fakeCloudRoot.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            fakeCloudRoot.transform.localScale = Vector3.one;
            GameObjectUtility.SetStaticEditorFlags(fakeCloudRoot, GameObjectUtility.GetStaticEditorFlags(otherRoot));

            foreach (Transform child in fakeCloudChildren)
            {
                child.SetParent(fakeCloudRoot.transform, true);
            }

            ApplyCategoryPolicy(fakeCloudRoot, fakeCloudCategory);
            ApplyCategoryPolicy(otherRoot, otherCategory);
            PrefabUtility.SaveAsPrefabAsset(fakeCloudRoot, fakeCloudPath);
            PrefabUtility.SaveAsPrefabAsset(otherRoot, otherPath);
            Debug.Log($"[GothicCategoryPrefabBuilder] Moved {fakeCloudChildren.Count} fake cloud roots from Gothic_Other to Gothic_FakeCloud.");
        }
        catch (Exception exception)
        {
            Debug.LogError($"[GothicCategoryPrefabBuilder] Failed to split fake clouds from Gothic_Other.\n{exception.Message}");
        }
        finally
        {
            if (otherRoot != null)
            {
                PrefabUtility.UnloadPrefabContents(otherRoot);
            }

            if (fakeCloudRoot != null)
            {
                UnityEngine.Object.DestroyImmediate(fakeCloudRoot);
            }
        }
    }

    private static List<Transform> FindDirectCategoryChildren(GameObject root, CategoryDefinition category)
    {
        List<Transform> children = new();
        for (int i = 0; i < root.transform.childCount; i++)
        {
            Transform child = root.transform.GetChild(i);
            if (category.Matches(BuildSearchName(child.gameObject)))
            {
                children.Add(child);
            }
        }

        return children;
    }

    private static CategoryDefinition GetCategory(string name)
    {
        foreach (CategoryDefinition category in Categories)
        {
            if (category.Name == name)
            {
                return category;
            }
        }

        throw new InvalidOperationException($"Missing Gothic category: {name}");
    }

    private static Dictionary<CategoryDefinition, GameObject> CreateCategoryRoots()
    {
        Dictionary<CategoryDefinition, GameObject> roots = new();
        foreach (CategoryDefinition category in Categories)
        {
            GameObject root = new($"Gothic_{category.Name}");
            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            root.transform.localScale = Vector3.one;
            GameObjectUtility.SetStaticEditorFlags(root, StaticEditorFlags.BatchingStatic);
            roots.Add(category, root);
        }

        return roots;
    }

    private static List<GameObject> FindChunkRoots(Scene scene)
    {
        List<GameObject> roots = new();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            foreach (Transform transform in transforms)
            {
                GameObject candidate = transform.gameObject;
                if (!candidate.name.StartsWith("Gothic_MeshChunk_", StringComparison.Ordinal))
                {
                    continue;
                }

                GameObject nearestPrefabRoot = PrefabUtility.GetNearestPrefabInstanceRoot(candidate);
                if (nearestPrefabRoot != null && nearestPrefabRoot != candidate)
                {
                    continue;
                }

                roots.Add(candidate);
            }
        }

        return roots;
    }

    private static CategoryDefinition Classify(GameObject gameObject)
    {
        string combinedName = BuildSearchName(gameObject);
        foreach (CategoryDefinition category in Categories)
        {
            if (category.Name == "Other")
            {
                continue;
            }

            if (category.Matches(combinedName))
            {
                return category;
            }
        }

        return Categories[^1];
    }

    private static string BuildSearchName(GameObject gameObject)
    {
        string searchName = gameObject.name;
        MeshFilter meshFilter = gameObject.GetComponentInChildren<MeshFilter>(true);
        if (meshFilter != null && meshFilter.sharedMesh != null)
        {
            searchName += " " + meshFilter.sharedMesh.name;
        }

        return searchName.ToLowerInvariant();
    }

    private static void ApplyCategoryPolicy(GameObject root, CategoryDefinition category)
    {
        if (!category.KeepColliders)
        {
            foreach (Collider collider in root.GetComponentsInChildren<Collider>(true))
            {
                UnityEngine.Object.DestroyImmediate(collider);
            }
        }
        else if (category.Name == "Props")
        {
            RemoveSmallCandleColliders(root);
        }

        if (category.Name == "Candles" || category.Name == "Props")
        {
            foreach (ParticleSystem particleSystem in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                UnityEngine.Object.DestroyImmediate(particleSystem);
            }

            foreach (ParticleSystemRenderer particleRenderer in root.GetComponentsInChildren<ParticleSystemRenderer>(true))
            {
                UnityEngine.Object.DestroyImmediate(particleRenderer);
            }
        }
    }

    private static void RemoveSmallCandleColliders(GameObject root)
    {
        foreach (Collider collider in root.GetComponentsInChildren<Collider>(true))
        {
            string searchName = BuildSearchName(collider.gameObject);
            string objectName = collider.gameObject.name;
            bool isSmallCandlePart =
                IsSmallCandleMeshName(searchName) ||
                IsLooseCandlePart(objectName);

            if (isSmallCandlePart)
            {
                UnityEngine.Object.DestroyImmediate(collider);
            }
        }
    }

    private static bool IsLooseCandlePart(string objectName)
    {
        return objectName.Contains("candle", StringComparison.OrdinalIgnoreCase) &&
            !objectName.Contains("chandelier", StringComparison.OrdinalIgnoreCase) &&
            !objectName.Contains("candelabra", StringComparison.OrdinalIgnoreCase) &&
            !objectName.Contains("candleabra", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSmallCandleMeshName(string searchName)
    {
        return searchName.Contains("thincandle", StringComparison.OrdinalIgnoreCase) ||
            searchName.Contains("thickcandle", StringComparison.OrdinalIgnoreCase) ||
            searchName.Contains("_wax", StringComparison.OrdinalIgnoreCase) ||
            searchName.Contains(" wax", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildReport(int chunkRootCount, Dictionary<CategoryDefinition, int> movedCounts)
    {
        System.Text.StringBuilder builder = new();
        builder.AppendLine($"[GothicCategoryPrefabBuilder] Rebuilt {chunkRootCount} chunk roots into category prefabs.");
        foreach (CategoryDefinition category in Categories)
        {
            movedCounts.TryGetValue(category, out int count);
            builder.AppendLine($"- {category.PrefabName}: {count} roots, colliders {(category.KeepColliders ? "kept" : "stripped")}");
        }

        builder.AppendLine($"Output: {OutputFolder}");
        builder.AppendLine("Scene is dirty. Save the scene after checking the result.");
        return builder.ToString();
    }

    private sealed class CategoryDefinition
    {
        private readonly string[] keywords;

        public CategoryDefinition(string name, string prefabName, bool keepColliders, params string[] keywords)
        {
            Name = name;
            PrefabName = prefabName;
            KeepColliders = keepColliders;
            this.keywords = keywords;
        }

        public string Name { get; }
        public string PrefabName { get; }
        public bool KeepColliders { get; }

        public bool Matches(string value)
        {
            foreach (string keyword in keywords)
            {
                if (value.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
