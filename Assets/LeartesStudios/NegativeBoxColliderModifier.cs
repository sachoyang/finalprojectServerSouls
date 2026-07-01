#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class NegativeBoxColliderModifier : MonoBehaviour
{
    [MenuItem("Tools/Bonnate/Negative Box Collider Modifier")]
    static void OpenNegativeBoxColliderModifierWindow()
    {
        // 에디터 창 열기
        NegativeBoxColliderWindow window = EditorWindow.GetWindow<NegativeBoxColliderWindow>("Negative Box Collider Modifier");
        window.Show();
    }
}

public class NegativeBoxColliderWindow : EditorWindow
{
    private Vector2 mScrollPosition; // 스크롤

    // 유효 크기(로컬 크기 * 로스시 스케일)가 하나라도 음수/0이면 문제가 있는 콜라이더로 판단.
    // 음수 스케일로 미러링된 프랍의 BoxCollider가 "negative scale/size" 워닝과
    // PhysX 'fidA != fidB' box-box 어설션 에러를 동시에 유발한다.
    static bool IsNegative(BoxCollider collider)
    {
        Vector3 scale = collider.transform.lossyScale;
        Vector3 size = collider.size;
        return scale.x * size.x <= 0f || scale.y * size.y <= 0f || scale.z * size.z <= 0f;
    }

    // 유효 크기가 양수가 되도록 축별로 m_Size 부호를 뒤집는다. 씬은 dirty로 표시하고
    // Undo 그룹에 기록해 되돌릴 수 있게 한다.
    static void FixCollider(BoxCollider collider)
    {
        Undo.RecordObject(collider, "Fix Negative Box Collider");

        SerializedObject colliderObj = new SerializedObject(collider);

        if (collider.transform.lossyScale.x * collider.size.x < 0f)
            colliderObj.FindProperty("m_Size.x").floatValue *= -1f;
        if (collider.transform.lossyScale.y * collider.size.y < 0f)
            colliderObj.FindProperty("m_Size.y").floatValue *= -1f;
        if (collider.transform.lossyScale.z * collider.size.z < 0f)
            colliderObj.FindProperty("m_Size.z").floatValue *= -1f;

        colliderObj.ApplyModifiedProperties();
        EditorUtility.SetDirty(collider);

        if (!Application.isPlaying)
            EditorSceneManager.MarkSceneDirty(collider.gameObject.scene);
    }

    // m_Size 부호만 뒤집는다(Undo/씬 dirty 없이). 프리팹 컨텐츠 편집에서 사용.
    static bool FlipSizeSigns(BoxCollider collider)
    {
        SerializedObject so = new SerializedObject(collider);
        bool changed = false;

        if (collider.transform.lossyScale.x * collider.size.x < 0f)
        {
            so.FindProperty("m_Size.x").floatValue *= -1f;
            changed = true;
        }
        if (collider.transform.lossyScale.y * collider.size.y < 0f)
        {
            so.FindProperty("m_Size.y").floatValue *= -1f;
            changed = true;
        }
        if (collider.transform.lossyScale.z * collider.size.z < 0f)
        {
            so.FindProperty("m_Size.z").floatValue *= -1f;
            changed = true;
        }

        if (changed)
            so.ApplyModifiedProperties();

        return changed;
    }

    // LeartesStudios 하위의 모든 프리팹 에셋을 열어 내부 BoxCollider의 negative size를 원본에서 바로 고친다.
    // 프리팹 루트/자식에 음수 스케일이 박혀 있는 경우를 잡는다. (씬 인스턴스 레벨의 음수 스케일은 씬 Fix All이 처리한다.)
    static void FixAllPrefabAssets()
    {
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/LeartesStudios" });
        int prefabsChanged = 0;
        int collidersFixed = 0;

        try
        {
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                EditorUtility.DisplayProgressBar("Fix Negative Box Colliders (Prefabs)", path, (float)i / Mathf.Max(1, guids.Length));

                GameObject root = PrefabUtility.LoadPrefabContents(path);
                bool prefabChanged = false;

                foreach (BoxCollider collider in root.GetComponentsInChildren<BoxCollider>(true))
                {
                    if (!IsNegative(collider))
                        continue;

                    if (FlipSizeSigns(collider))
                    {
                        prefabChanged = true;
                        ++collidersFixed;
                    }
                }

                if (prefabChanged)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    ++prefabsChanged;
                }

                PrefabUtility.UnloadPrefabContents(root);
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"<color=cyan>[Negative Box Collider Modifier]</color> Prefab assets fixed: {prefabsChanged}, colliders fixed: {collidersFixed}.");
    }

    static int HierarchyDepth(Transform t)
    {
        int depth = 0;
        while (t.parent != null)
        {
            ++depth;
            t = t.parent;
        }
        return depth;
    }

    // 'fidA != fidB'(UUM-65056)의 근본 해결: 씬의 모든 프리팹 인스턴스를 Unpack Completely 하여
    // 프리팹 에셋과 인스턴스 간 fileID 충돌 참조를 끊는다. 중첩 프리팹은 깊은 것부터 언팩한다.
    // 오버라이드(예: negative box size 수정)는 씬에 그대로 구워지며, 프리팹 연결만 사라진다.
    static void UnpackAllPrefabInstancesInScene()
    {
        HashSet<GameObject> roots = new HashSet<GameObject>();
        foreach (Transform t in FindObjectsOfType<Transform>(true))
        {
            GameObject root = PrefabUtility.GetNearestPrefabInstanceRoot(t.gameObject);
            if (root != null)
                roots.Add(root);
        }

        // 중첩 프리팹은 안쪽(깊은 것)부터 풀어야 바깥 인스턴스가 유효하다.
        List<GameObject> ordered = new List<GameObject>(roots);
        ordered.Sort((a, b) => HierarchyDepth(b.transform).CompareTo(HierarchyDepth(a.transform)));

        int count = 0;
        foreach (GameObject root in ordered)
        {
            if (root == null || !PrefabUtility.IsAnyPrefabInstanceRoot(root))
                continue;

            try
            {
                PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                ++count;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("Unpack skipped: " + root.name + " - " + ex.Message, root);
            }
        }

        if (!Application.isPlaying)
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        Debug.Log($"<color=cyan>[Negative Box Collider Modifier]</color> Unpacked {count} prefab instances. Save the scene (Ctrl+S), then re-enter Play mode to verify the assertion is gone.");
    }

    // ===== 언팩된 오브젝트를 이름 매칭으로 다시 프리팹 인스턴스로 연결 (재프리팹화) =====

    // Unity 중복 접미사 " (1)" 만 제거한다. (프리팹 이름 자체의 _01 같은 접미사는 건드리지 않음)
    static string StripDuplicateSuffix(string name)
    {
        return Regex.Replace(name, @"\s*\(\d+\)$", "");
    }

    // LeartesStudios 하위 프리팹의 (이름 -> 경로) 사전을 만든다. 같은 이름이 둘 이상이면 ambiguous로 표시.
    static void BuildPrefabNameIndex(out Dictionary<string, string> nameToPath, out HashSet<string> ambiguous)
    {
        nameToPath = new Dictionary<string, string>();
        ambiguous = new HashSet<string>();

        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/LeartesStudios" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string name = System.IO.Path.GetFileNameWithoutExtension(path);

            if (nameToPath.ContainsKey(name))
                ambiguous.Add(name);
            else
                nameToPath[name] = path;
        }
    }

    // 오브젝트 이름에 맞는 프리팹 경로를 찾는다. 정확 이름 우선, 실패 시 " (N)" 접미사 제거 후 재시도.
    static string ResolvePrefabPath(string objectName, Dictionary<string, string> nameToPath, HashSet<string> ambiguous)
    {
        if (nameToPath.TryGetValue(objectName, out string path) && !ambiguous.Contains(objectName))
            return path;

        string stripped = StripDuplicateSuffix(objectName);
        if (stripped != objectName && nameToPath.TryGetValue(stripped, out path) && !ambiguous.Contains(stripped))
            return path;

        return null;
    }

    // 계층을 훑어, 이름이 프리팹과 매칭되는 '최상위' 오브젝트만 재프리팹화 대상으로 모은다.
    // 매칭되면 그 자식(LOD 등)은 더 내려가지 않는다.
    static void CollectReconnectTargets(Transform t, Dictionary<string, string> nameToPath, HashSet<string> ambiguous, List<GameObject> targets)
    {
        // 이미 프리팹 인스턴스면 건너뛰고 자식만 계속 탐색.
        if (!PrefabUtility.IsPartOfPrefabInstance(t.gameObject) &&
            ResolvePrefabPath(t.gameObject.name, nameToPath, ambiguous) != null)
        {
            targets.Add(t.gameObject);
            return;
        }

        foreach (Transform child in t)
            CollectReconnectTargets(child, nameToPath, ambiguous, targets);
    }

    static void ReconnectToPrefabs(GameObject[] searchRoots, string scopeLabel)
    {
        BuildPrefabNameIndex(out Dictionary<string, string> nameToPath, out HashSet<string> ambiguous);

        List<GameObject> targets = new List<GameObject>();
        foreach (GameObject root in searchRoots)
        {
            if (root != null)
                CollectReconnectTargets(root.transform, nameToPath, ambiguous, targets);
        }

        Undo.SetCurrentGroupName("Reconnect Prefabs (" + scopeLabel + ")");
        int undoGroup = Undo.GetCurrentGroup();

        int converted = 0;
        int failed = 0;
        ConvertToPrefabInstanceSettings settings = new ConvertToPrefabInstanceSettings();

        foreach (GameObject go in targets)
        {
            string path = ResolvePrefabPath(go.name, nameToPath, ambiguous);
            if (path == null)
                continue;

            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null)
                continue;

            try
            {
                PrefabUtility.ConvertToPrefabInstance(go, asset, settings, InteractionMode.UserAction);
                ++converted;
            }
            catch (System.Exception ex)
            {
                ++failed;
                Debug.LogWarning("Reconnect skipped: " + go.name + " -> " + path + "\n" + ex.Message, go);
            }
        }

        Undo.CollapseUndoOperations(undoGroup);

        if (!Application.isPlaying && searchRoots.Length > 0 && searchRoots[0] != null)
            EditorSceneManager.MarkSceneDirty(searchRoots[0].scene);

        Debug.Log($"<color=cyan>[Negative Box Collider Modifier]</color> Reconnect ({scopeLabel}): matched {targets.Count}, converted {converted}, failed {failed}. Save the scene (Ctrl+S).");
    }

    // 겹친 BoxCollider들의 월드 중심/크기로 그룹핑한다. (같은 자리 = 같은 키)
    static Dictionary<string, List<BoxCollider>> GroupOverlappingColliders(BoxCollider[] colliders)
    {
        Dictionary<string, List<BoxCollider>> groups = new Dictionary<string, List<BoxCollider>>();

        foreach (BoxCollider collider in colliders)
        {
            Bounds b = collider.bounds; // 월드 공간 AABB
            string key =
                Mathf.RoundToInt(b.center.x * 100f) + "_" +
                Mathf.RoundToInt(b.center.y * 100f) + "_" +
                Mathf.RoundToInt(b.center.z * 100f) + "|" +
                Mathf.RoundToInt(b.size.x * 100f) + "_" +
                Mathf.RoundToInt(b.size.y * 100f) + "_" +
                Mathf.RoundToInt(b.size.z * 100f);

            if (!groups.TryGetValue(key, out List<BoxCollider> list))
            {
                list = new List<BoxCollider>();
                groups[key] = list;
            }
            list.Add(collider);
        }

        return groups;
    }

    // 그룹에서 남길 콜라이더 인덱스: 이름에 LOD0이 있으면 우선, 없으면 0.
    static int PickKeepIndex(List<BoxCollider> group)
    {
        for (int i = 0; i < group.Count; i++)
            if (group[i].gameObject.name.IndexOf("LOD0", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return i;
        return 0;
    }

    // 'fidA != fidB' 어설션의 흔한 원인: 같은 자리에 정확히 겹쳐 놓인 중복 BoxCollider.
    // 월드 중심/유효 크기가 사실상 동일한 콜라이더 그룹을 찾아 선택해 준다(수동 검토용).
    static void SelectDuplicateOverlappingColliders()
    {
        Dictionary<string, List<BoxCollider>> groups = GroupOverlappingColliders(FindObjectsOfType<BoxCollider>(true));

        List<Object> duplicates = new List<Object>();
        int groupCount = 0;
        foreach (KeyValuePair<string, List<BoxCollider>> pair in groups)
        {
            if (pair.Value.Count < 2)
                continue;

            ++groupCount;
            int keepIndex = PickKeepIndex(pair.Value);
            for (int i = 0; i < pair.Value.Count; i++)
                if (i != keepIndex)
                    duplicates.Add(pair.Value[i].gameObject);
        }

        Selection.objects = duplicates.ToArray();
        Debug.Log($"<color=cyan>[Negative Box Collider Modifier]</color> Overlapping duplicate collider groups: {groupCount}, redundant colliders: {duplicates.Count}.");
    }

    // 씬에서 겹친 BoxCollider 그룹마다 하나(LOD0 우선)만 남기고 나머지 '콜라이더 컴포넌트'만 제거한다.
    // GameObject/렌더러는 유지되므로 LOD 표시는 그대로고, 물리 접촉만 프랍당 하나로 정리된다. (Undo 지원)
    static void RemoveDuplicateCollidersInScene()
    {
        Dictionary<string, List<BoxCollider>> groups = GroupOverlappingColliders(FindObjectsOfType<BoxCollider>(true));

        Undo.SetCurrentGroupName("Remove Duplicate Scene Colliders");
        int undoGroup = Undo.GetCurrentGroup();

        int removed = 0;
        foreach (KeyValuePair<string, List<BoxCollider>> pair in groups)
        {
            if (pair.Value.Count < 2)
                continue;

            int keepIndex = PickKeepIndex(pair.Value);
            for (int i = 0; i < pair.Value.Count; i++)
            {
                if (i == keepIndex)
                    continue;

                UnityEngine.SceneManagement.Scene scene = pair.Value[i].gameObject.scene;
                Undo.DestroyObjectImmediate(pair.Value[i]);
                ++removed;
                if (!Application.isPlaying)
                    EditorSceneManager.MarkSceneDirty(scene);
            }
        }

        Undo.CollapseUndoOperations(undoGroup);
        Debug.Log($"<color=cyan>[Negative Box Collider Modifier]</color> Duplicate colliders removed from scene: {removed}. Save the scene (Ctrl+S).");
    }

    void OnGUI()
    {
        // === 'fidA != fidB' 근본 해결 (Unity fileID 충돌 버그, UUM-65056) ===
        EditorGUILayout.LabelField("fidA != fidB Assertion Fix (Prefab fileID conflict)", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "'Assertion failed: fidA != fidB'는 콜라이더가 아니라 구버전 프리팹의 fileID 충돌 버그입니다.\n" +
            "아래 버튼으로 씬의 모든 프리팹 인스턴스를 Unpack Completely 하면 충돌 참조가 끊겨 에러가 사라집니다.\n" +
            "(오버라이드는 씬에 유지, 프리팹 연결만 해제. Undo 불가 → 먼저 git 백업)",
            MessageType.Info);
        if (GUILayout.Button("Unpack All Prefab Instances In Scene (Fix fidA != fidB)", GUILayout.Height(30)))
        {
            if (EditorUtility.DisplayDialog(
                    "Unpack All Prefab Instances",
                    "현재 씬의 모든 프리팹 인스턴스를 완전히 언팩합니다.\n\n" +
                    "이후 오브젝트들은 프리팹과의 연결이 끊어지며 되돌릴 수 없습니다(Undo 불가).\n" +
                    "먼저 git 커밋으로 백업했는지 확인하세요. 계속할까요?",
                    "Unpack (Fix)", "Cancel"))
            {
                UnpackAllPrefabInstancesInScene();
            }
        }

        GUILayout.Space(10);

        // === 언팩된 오브젝트 재프리팹화 (이름 매칭, ConvertToPrefabInstance) ===
        EditorGUILayout.LabelField("Re-link Unpacked Objects To Prefabs", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "언팩으로 끊긴 오브젝트를 이름 매칭으로 현재 프리팹 에셋의 인스턴스로 다시 연결합니다(기존 모습 유지, 차이는 override).\n" +
            "먼저 오브젝트 몇 개만 선택해 'Reconnect Selected'로 테스트 → Play로 fidA 재발 없는지 확인 후 씬 전체를 진행하세요.\n" +
            "이름이 같으면 하나의 프리팹으로 통일되며, 이름 중복 프리팹은 자동 스킵됩니다. Undo 가능.",
            MessageType.Info);

        using (new EditorGUI.DisabledScope(Selection.gameObjects.Length == 0))
        {
            if (GUILayout.Button($"Reconnect Selected ({Selection.gameObjects.Length}) — test first", GUILayout.Height(24)))
            {
                ReconnectToPrefabs(Selection.gameObjects, "Selection");
            }
        }

        if (GUILayout.Button("Reconnect Entire Active Scene To Prefabs", GUILayout.Height(24)))
        {
            if (EditorUtility.DisplayDialog(
                    "Reconnect Active Scene To Prefabs",
                    "현재 씬에서 이름이 프리팹과 매칭되는 최상위 오브젝트를 모두 프리팹 인스턴스로 다시 연결합니다.\n\n" +
                    "먼저 소수 선택으로 테스트하고 Play에서 fidA 재발이 없는지 확인했는지 점검하세요. 계속할까요?",
                    "Reconnect Scene", "Cancel"))
            {
                ReconnectToPrefabs(UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects(), "Active Scene");
            }
        }

        GUILayout.Space(10);

        // 상단 일괄 처리 영역
        BoxCollider[] allColliders = FindObjectsOfType<BoxCollider>(true);
        int negativeCount = 0;
        foreach (BoxCollider collider in allColliders)
            if (IsNegative(collider))
                ++negativeCount;

        GUILayout.Space(4);
        EditorGUILayout.LabelField($"Negative Box Colliders in scene: {negativeCount}", EditorStyles.boldLabel);

        using (new EditorGUI.DisabledScope(negativeCount == 0))
        {
            if (GUILayout.Button($"Fix All ({negativeCount})", GUILayout.Height(28)))
            {
                Undo.SetCurrentGroupName("Fix All Negative Box Colliders");
                int group = Undo.GetCurrentGroup();

                int fixedCount = 0;
                foreach (BoxCollider collider in allColliders)
                {
                    if (!IsNegative(collider))
                        continue;

                    FixCollider(collider);
                    ++fixedCount;
                }

                Undo.CollapseUndoOperations(group);
                Debug.Log($"<color=cyan>[Negative Box Collider Modifier]</color> Fixed {fixedCount} negative box colliders.");
            }
        }

        GUILayout.Space(8);
        EditorGUILayout.LabelField("Prefab Assets (원본 프리팹 직접 수정)", EditorStyles.boldLabel);
        if (GUILayout.Button("Fix All in Prefab Assets (LeartesStudios)", GUILayout.Height(24)))
        {
            if (EditorUtility.DisplayDialog(
                    "Fix Negative Box Colliders in Prefab Assets",
                    "Assets/LeartesStudios 하위의 모든 프리팹 에셋을 열어 내부 BoxCollider의 negative size를 수정합니다.\n\n원본 에셋이 덮어써지며 Undo가 되지 않습니다. 계속할까요?",
                    "Fix Prefabs", "Cancel"))
            {
                FixAllPrefabAssets();
            }
        }

        GUILayout.Space(8);
        EditorGUILayout.LabelField("Diagnostics ('fidA != fidB' 원인 추적)", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "negative scale 워닝을 다 고쳤는데도 'fidA != fidB' 에러가 남는다면, 같은 자리에 겹쳐 놓인 중복 콜라이더가 원인일 확률이 높습니다. 아래 버튼으로 중복본을 선택한 뒤 검토하고 삭제하세요.",
            MessageType.Info);
        if (GUILayout.Button("1) Select Overlapping Duplicate Box Colliders", GUILayout.Height(24)))
        {
            SelectDuplicateOverlappingColliders();
        }

        GUILayout.Space(4);
        EditorGUILayout.HelpBox(
            "겹친 콜라이더 그룹마다 하나(LOD0 우선)만 남기고 나머지 '콜라이더 컴포넌트'만 제거합니다. GameObject/렌더러는 그대로 유지되고 Undo 가능합니다. 실행 후 씬 저장(Ctrl+S).",
            MessageType.Info);
        if (GUILayout.Button("2) Remove Duplicate Colliders in Scene", GUILayout.Height(28)))
        {
            RemoveDuplicateCollidersInScene();
        }

        GUILayout.Space(6);

        mScrollPosition = GUILayout.BeginScrollView(mScrollPosition);

        int cnt = 0;

        foreach (BoxCollider collider in allColliders)
        {
            if (!IsNegative(collider))
                continue;

            ++cnt;

            GUILayout.BeginHorizontal();
            GUILayout.Label($"{collider.gameObject.name}", EditorStyles.boldLabel);
            if (GUILayout.Button("View", GUILayout.Width(60)))
            {
                SceneView.lastActiveSceneView.LookAt(collider.transform.position);
                Selection.activeGameObject = collider.gameObject;
            }

            if (GUILayout.Button("Fix", GUILayout.Width(60)))
            {
                FixCollider(collider);
            }
            GUILayout.EndHorizontal();
        }

        GUILayout.EndScrollView();

        if (cnt == 0)
            GUILayout.Label($"There's no negative Box colliders in scene!", EditorStyles.boldLabel);

        GUILayout.Space(20);

        GUILayout.BeginHorizontal();

        EditorGUILayout.LabelField("Powered by: Bonnate");

        if (GUILayout.Button("Github", GetHyperlinkLabelStyle()))
        {
            OpenURL("https://github.com/bonnate");
        }

        if (GUILayout.Button("Blog", GetHyperlinkLabelStyle()))
        {
            OpenURL("https://bonnate.tistory.com/");
        }

        GUILayout.EndHorizontal();
    }

    #region _HYPERLINK
    private GUIStyle GetHyperlinkLabelStyle()
    {
        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.normal.textColor = new Color(0f, 0.5f, 1f);
        style.stretchWidth = false;
        style.wordWrap = false;
        return style;
    }

    private void OpenURL(string url)
    {
        EditorUtility.OpenWithDefaultApp(url);
    }
    #endregion

    #region 
    private void Log(string content)
    {
        Debug.Log($"<color=cyan>[WAV Easy Volume Editor]</color> {content}");
    }
    #endregion
}
#endif
