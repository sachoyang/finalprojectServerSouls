using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

[InitializeOnLoad]
public static class GothicInteriorPrefabTools
{
    private const string CleanupViewEnabledKey = "GothicInterior.CleanupViewEnabled";
    private const string OriginalUrpScenePath = "Assets/Gothic_Interior/Environment/Scenes/Cathedral_Main URP.unity";
    private static bool areaSelectEnabled;
    private static bool areaSelecting;
    private static Vector2 areaStart;
    private static Vector2 areaEnd;

    static GothicInteriorPrefabTools()
    {
        if (EditorPrefs.GetBool(CleanupViewEnabledKey, false))
        {
            SceneView.duringSceneGui += KeepCleanupViewSettings;
        }
    }

    [MenuItem("Tools/Gothic Interior/LOD/Force All LOD 0 For Editing")]
    public static void ForceAllLodZeroForEditing()
    {
        LODGroup[] groups = Object.FindObjectsOfType<LODGroup>(true);
        foreach (LODGroup group in groups)
        {
            group.ForceLOD(0);
        }

        SceneView.RepaintAll();
        Debug.Log("Forced LOD 0 for editing: " + groups.Length + " LOD groups.");
    }

    [MenuItem("Tools/Gothic Interior/LOD/Restore Automatic LOD")]
    public static void RestoreAutomaticLod()
    {
        LODGroup[] groups = Object.FindObjectsOfType<LODGroup>(true);
        foreach (LODGroup group in groups)
        {
            group.ForceLOD(-1);
        }

        SceneView.RepaintAll();
        Debug.Log("Restored automatic LOD: " + groups.Length + " LOD groups.");
    }

    [MenuItem("Tools/Gothic Interior/Prepare Scene View For Cleanup")]
    public static void PrepareSceneViewForCleanup()
    {
        ForceAllLodZeroForEditing();
        EditorPrefs.SetBool(CleanupViewEnabledKey, true);
        SceneView.duringSceneGui -= KeepCleanupViewSettings;
        SceneView.duringSceneGui += KeepCleanupViewSettings;
        SceneView.RepaintAll();
        Debug.Log("Persistent scene cleanup view enabled: LOD 0, dynamic clipping off, far clip 100000, occlusion culling off.");
    }

    [MenuItem("Tools/Gothic Interior/Disable Scene View Cleanup Mode")]
    public static void DisableSceneViewCleanupMode()
    {
        EditorPrefs.SetBool(CleanupViewEnabledKey, false);
        SceneView.duringSceneGui -= KeepCleanupViewSettings;
        RestoreAutomaticLod();
        SceneView.RepaintAll();
        Debug.Log("Scene cleanup view disabled.");
    }

    private static void KeepCleanupViewSettings(SceneView view)
    {
        if (view == null || view.camera == null)
        {
            return;
        }

        SceneView.CameraSettings settings = view.cameraSettings;
        settings.dynamicClip = false;
        view.cameraSettings = settings;
        view.camera.nearClipPlane = 0.03f;
        view.camera.farClipPlane = 100000f;
        view.camera.useOcclusionCulling = false;
    }

    [MenuItem("Tools/Gothic Interior/Selection/Enable Renderer And Light Area Select")]
    public static void EnableRendererAreaSelect()
    {
        areaSelectEnabled = true;
        areaSelecting = false;
        SceneView.duringSceneGui -= RendererAreaSelectSceneGui;
        SceneView.duringSceneGui += RendererAreaSelectSceneGui;
        Debug.Log("Renderer and light area selection enabled. Drag with the left mouse button in Scene view. Hold Shift to add to the current selection.");
    }

    [MenuItem("Tools/Gothic Interior/Selection/Disable Renderer And Light Area Select")]
    public static void DisableRendererAreaSelect()
    {
        areaSelectEnabled = false;
        areaSelecting = false;
        SceneView.duringSceneGui -= RendererAreaSelectSceneGui;
        SceneView.RepaintAll();
        Debug.Log("Renderer and light area selection disabled.");
    }

    [MenuItem("Tools/Gothic Interior/Selection/Select All Particle Systems")]
    public static void SelectAllParticleSystems()
    {
        ParticleSystem[] particleSystems = Object.FindObjectsOfType<ParticleSystem>(true);
        Object[] objects = new Object[particleSystems.Length];
        for (int i = 0; i < particleSystems.Length; i++)
        {
            objects[i] = particleSystems[i].gameObject;
        }

        Selection.objects = objects;
        Debug.Log("Selected all particle system GameObjects: " + particleSystems.Length);
    }

    [MenuItem("Tools/Gothic Interior/Selection/Select Broken Or Empty LOD Roots")]
    public static void SelectBrokenOrEmptyLodRoots()
    {
        LODGroup[] groups = Object.FindObjectsOfType<LODGroup>(true);
        List<Object> brokenRoots = new List<Object>();

        foreach (LODGroup group in groups)
        {
            bool hasValidRenderer = false;
            foreach (LOD lod in group.GetLODs())
            {
                foreach (Renderer renderer in lod.renderers)
                {
                    if (renderer != null)
                    {
                        hasValidRenderer = true;
                        break;
                    }
                }

                if (hasValidRenderer)
                {
                    break;
                }
            }

            if (!hasValidRenderer)
            {
                brokenRoots.Add(group.gameObject);
            }
        }

        Selection.objects = brokenRoots.ToArray();
        Debug.Log("Selected broken or empty LOD roots: " + brokenRoots.Count + ". Review them, then press Delete.");
    }

    [MenuItem("Tools/Gothic Interior/Restore Missing Lights From Original URP Scene")]
    public static void RestoreMissingLightsFromOriginalUrpScene()
    {
        Scene targetScene = SceneManager.GetActiveScene();
        if (!targetScene.IsValid() || string.IsNullOrEmpty(targetScene.path))
        {
            Debug.LogError("Open and save the target Fixed scene before restoring lights.");
            return;
        }

        if (targetScene.path == OriginalUrpScenePath)
        {
            Debug.LogError("Open the Fixed scene first. The original URP scene is the restore source.");
            return;
        }

        HashSet<string> targetLightKeys = new HashSet<string>();
        foreach (Light light in GetLightsInScene(targetScene))
        {
            targetLightKeys.Add(GetLightKey(light));
        }

        Scene sourceScene = EditorSceneManager.OpenScene(OriginalUrpScenePath, OpenSceneMode.Additive);
        List<GameObject> restoredObjects = new List<GameObject>();
        GameObject restoredRoot = new GameObject("Restored Lights From Original");
        SceneManager.MoveGameObjectToScene(restoredRoot, targetScene);
        Undo.RegisterCreatedObjectUndo(restoredRoot, "Restore missing Gothic Interior lights");

        foreach (Light sourceLight in GetLightsInScene(sourceScene))
        {
            string key = GetLightKey(sourceLight);
            if (targetLightKeys.Contains(key))
            {
                continue;
            }

            Vector3 worldPosition = sourceLight.transform.position;
            Quaternion worldRotation = sourceLight.transform.rotation;
            Vector3 worldScale = sourceLight.transform.lossyScale;
            GameObject clone = Object.Instantiate(sourceLight.gameObject);
            clone.name = sourceLight.gameObject.name;
            SceneManager.MoveGameObjectToScene(clone, targetScene);
            clone.transform.SetParent(restoredRoot.transform, true);
            clone.transform.position = worldPosition;
            clone.transform.rotation = worldRotation;
            clone.transform.localScale = worldScale;
            Undo.RegisterCreatedObjectUndo(clone, "Restore missing Gothic Interior light");
            restoredObjects.Add(clone);
            targetLightKeys.Add(key);
        }

        EditorSceneManager.CloseScene(sourceScene, true);
        if (restoredObjects.Count == 0)
        {
            Undo.DestroyObjectImmediate(restoredRoot);
        }
        else
        {
            Selection.objects = restoredObjects.ToArray();
            EditorSceneManager.MarkSceneDirty(targetScene);
        }

        Debug.Log("Restored missing lights from original URP scene: " + restoredObjects.Count + ". Review the Restored Lights From Original group, then save.");
    }

    private static void RendererAreaSelectSceneGui(SceneView view)
    {
        if (!areaSelectEnabled || view == null || view.camera == null)
        {
            return;
        }

        Event current = Event.current;
        int controlId = GUIUtility.GetControlID("GothicRendererAreaSelect".GetHashCode(), FocusType.Passive);
        if (current.type == EventType.Layout)
        {
            HandleUtility.AddDefaultControl(controlId);
        }

        if (current.type == EventType.MouseDown && current.button == 0 && !current.alt)
        {
            areaStart = current.mousePosition;
            areaEnd = areaStart;
            areaSelecting = true;
            current.Use();
        }
        else if (current.type == EventType.MouseDrag && areaSelecting && current.button == 0)
        {
            areaEnd = current.mousePosition;
            view.Repaint();
            current.Use();
        }
        else if (current.type == EventType.MouseUp && areaSelecting && current.button == 0)
        {
            areaEnd = current.mousePosition;
            SelectRenderersInArea(view.camera, NormalizedRect(areaStart, areaEnd), current.shift);
            areaSelecting = false;
            view.Repaint();
            current.Use();
        }

        if (areaSelecting)
        {
            Rect rect = NormalizedRect(areaStart, areaEnd);
            Handles.BeginGUI();
            EditorGUI.DrawRect(rect, new Color(0.15f, 0.55f, 1f, 0.08f));
            Handles.color = new Color(0.25f, 0.7f, 1f, 1f);
            Handles.DrawAAPolyLine(2f,
                new Vector3(rect.xMin, rect.yMin),
                new Vector3(rect.xMax, rect.yMin),
                new Vector3(rect.xMax, rect.yMax),
                new Vector3(rect.xMin, rect.yMax),
                new Vector3(rect.xMin, rect.yMin));
            Handles.EndGUI();
        }
    }

    private static void SelectRenderersInArea(Camera camera, Rect selectionRect, bool addToSelection)
    {
        Renderer[] renderers = Object.FindObjectsOfType<Renderer>(true);
        HashSet<GameObject> selected = new HashSet<GameObject>();
        if (addToSelection)
        {
            foreach (GameObject selectedObject in Selection.gameObjects)
            {
                selected.Add(selectedObject);
            }
        }

        foreach (Renderer renderer in renderers)
        {
            if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
            {
                continue;
            }

            Rect rendererRect;
            if (TryGetRendererGuiRect(camera, renderer.bounds, out rendererRect) && selectionRect.Overlaps(rendererRect, true))
            {
                selected.Add(GetRendererSelectionRoot(renderer));
            }
        }

        int selectedLights = 0;
        Light[] lights = Object.FindObjectsOfType<Light>(true);
        foreach (Light light in lights)
        {
            if (light == null || !light.enabled || !light.gameObject.activeInHierarchy)
            {
                continue;
            }

            Vector3 viewportPoint = camera.WorldToViewportPoint(light.transform.position);
            if (viewportPoint.z <= 0f)
            {
                continue;
            }

            Vector2 guiPoint = HandleUtility.WorldToGUIPoint(light.transform.position);
            if (selectionRect.Contains(guiPoint) && selected.Add(light.gameObject))
            {
                selectedLights++;
            }
        }

        int selectedParticleSystems = 0;
        ParticleSystem[] particleSystems = Object.FindObjectsOfType<ParticleSystem>(true);
        foreach (ParticleSystem particleSystem in particleSystems)
        {
            if (particleSystem == null || !particleSystem.gameObject.activeInHierarchy)
            {
                continue;
            }

            bool inside = false;
            ParticleSystemRenderer particleRenderer = particleSystem.GetComponent<ParticleSystemRenderer>();
            Rect particleRect;
            if (particleRenderer != null && TryGetRendererGuiRect(camera, particleRenderer.bounds, out particleRect))
            {
                inside = selectionRect.Overlaps(particleRect, true);
            }

            if (!inside)
            {
                Vector3 viewportPoint = camera.WorldToViewportPoint(particleSystem.transform.position);
                if (viewportPoint.z > 0f)
                {
                    inside = selectionRect.Contains(HandleUtility.WorldToGUIPoint(particleSystem.transform.position));
                }
            }

            if (inside && selected.Add(particleSystem.gameObject))
            {
                selectedParticleSystems++;
            }
        }

        Object[] objects = new Object[selected.Count];
        int index = 0;
        foreach (GameObject gameObject in selected)
        {
            objects[index++] = gameObject;
        }

        Selection.objects = objects;
        Debug.Log("Area selection selected objects: " + selected.Count + " (lights: " + selectedLights + ", particle systems: " + selectedParticleSystems + ").");
    }

    private static bool TryGetRendererGuiRect(Camera camera, Bounds bounds, out Rect rect)
    {
        Vector3 center = bounds.center;
        Vector3 extents = bounds.extents;
        float minX = float.PositiveInfinity;
        float minY = float.PositiveInfinity;
        float maxX = float.NegativeInfinity;
        float maxY = float.NegativeInfinity;
        bool hasPointInFront = false;

        for (int x = -1; x <= 1; x += 2)
        for (int y = -1; y <= 1; y += 2)
        for (int z = -1; z <= 1; z += 2)
        {
            Vector3 worldPoint = center + Vector3.Scale(extents, new Vector3(x, y, z));
            if (camera.WorldToViewportPoint(worldPoint).z <= 0f)
            {
                continue;
            }

            Vector2 guiPoint = HandleUtility.WorldToGUIPoint(worldPoint);
            minX = Mathf.Min(minX, guiPoint.x);
            minY = Mathf.Min(minY, guiPoint.y);
            maxX = Mathf.Max(maxX, guiPoint.x);
            maxY = Mathf.Max(maxY, guiPoint.y);
            hasPointInFront = true;
        }

        rect = hasPointInFront ? Rect.MinMaxRect(minX, minY, maxX, maxY) : default(Rect);
        return hasPointInFront;
    }

    private static GameObject GetRendererSelectionRoot(Renderer renderer)
    {
        LODGroup lodGroup = renderer.GetComponentInParent<LODGroup>();
        return lodGroup != null ? lodGroup.gameObject : renderer.gameObject;
    }

    private static List<Light> GetLightsInScene(Scene scene)
    {
        List<Light> lights = new List<Light>();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            lights.AddRange(root.GetComponentsInChildren<Light>(true));
        }

        return lights;
    }

    private static string GetLightKey(Light light)
    {
        Vector3 position = light.transform.position;
        return light.name + "|" + light.type + "|" +
               Mathf.RoundToInt(position.x * 100f) + "|" +
               Mathf.RoundToInt(position.y * 100f) + "|" +
               Mathf.RoundToInt(position.z * 100f);
    }

    private static Rect NormalizedRect(Vector2 start, Vector2 end)
    {
        return Rect.MinMaxRect(
            Mathf.Min(start.x, end.x),
            Mathf.Min(start.y, end.y),
            Mathf.Max(start.x, end.x),
            Mathf.Max(start.y, end.y));
    }

    [MenuItem("Tools/Gothic Interior/Duplicate Current Scene As 2022 Fixed")]
    public static void DuplicateCurrentSceneAs2022Fixed()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || string.IsNullOrEmpty(scene.path))
        {
            Debug.LogError("Save or open a scene before duplicating it.");
            return;
        }

        string newPath = scene.path.Replace(".unity", " 2022 Fixed.unity");
        AssetDatabase.CopyAsset(scene.path, newPath);
        AssetDatabase.Refresh();
        EditorSceneManager.OpenScene(newPath, OpenSceneMode.Single);
        Debug.Log("Opened duplicated scene: " + newPath);
    }

    [MenuItem("Tools/Gothic Interior/Create 2022 Fixed Scene And Unpack Meshes")]
    public static void Create2022FixedSceneAndUnpackMeshes()
    {
        DuplicateCurrentSceneAs2022Fixed();
        UnpackPrefabsUnderMeshes();
    }

    [MenuItem("Tools/Gothic Interior/Unpack Prefabs Under Meshes")]
    public static void UnpackPrefabsUnderMeshes()
    {
        GameObject meshes = GameObject.Find("Meshes");
        if (meshes == null)
        {
            Debug.LogError("Could not find a GameObject named 'Meshes' in the active scene.");
            return;
        }

        HashSet<GameObject> roots = new HashSet<GameObject>();
        Transform[] transforms = meshes.GetComponentsInChildren<Transform>(true);
        foreach (Transform transform in transforms)
        {
            GameObject root = PrefabUtility.GetNearestPrefabInstanceRoot(transform.gameObject);
            if (root != null && root.transform.IsChildOf(meshes.transform))
            {
                roots.Add(root);
            }
        }

        List<GameObject> orderedRoots = new List<GameObject>(roots);
        orderedRoots.Sort((a, b) => GetDepth(b.transform).CompareTo(GetDepth(a.transform)));

        int unpacked = 0;
        int failed = 0;
        foreach (GameObject root in orderedRoots)
        {
            if (root == null || !PrefabUtility.IsAnyPrefabInstanceRoot(root))
            {
                continue;
            }

            try
            {
                PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                unpacked++;
            }
            catch (System.ArgumentException ex)
            {
                failed++;
                Debug.LogWarning("Skipped non-root prefab object: " + root.name + "\n" + ex.Message, root);
            }
        }

        Scene scene = SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Unpacked prefab instances under Meshes: " + unpacked + ", failed/skipped: " + failed);
    }

    private static int GetDepth(Transform transform)
    {
        int depth = 0;
        while (transform.parent != null)
        {
            depth++;
            transform = transform.parent;
        }

        return depth;
    }
}
