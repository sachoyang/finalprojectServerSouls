using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class GothicBoxColliderMergeTool
{
    private const string RootName = "__Merged_BoxColliders";
    private const float BoundsTolerance = 0.01f;
    private const float AxisTolerance = 0.9999f;

    [MenuItem("Soul Rush/Optimization/Gothic Stage/Analyze Box Collider Merge")]
    public static void Analyze()
    {
        if (!TryBuildMergeResult(out List<MergeBox> result, out int sourceCount))
            return;

        int mergedSourceCount = 0;
        int mergedColliderCount = 0;
        foreach (MergeBox box in result)
        {
            if (box.Sources.Count < 2)
                continue;

            mergedSourceCount += box.Sources.Count;
            mergedColliderCount++;
        }

        int finalCount = sourceCount - mergedSourceCount + mergedColliderCount;
        Debug.Log(
            $"[Gothic Collider Merge] 대상 {sourceCount}개 → 예상 {finalCount}개 " +
            $"(병합되는 원본 {mergedSourceCount}개, 생성 Collider {mergedColliderCount}개)");
    }

    [MenuItem("Soul Rush/Optimization/Gothic Stage/Analyze Duplicate Box Colliders")]
    public static void AnalyzeDuplicateBoxColliders()
    {
        if (!TryFindDuplicateBoxColliders(out List<BoxCollider> duplicates))
            return;

        Debug.Log(
            $"[Gothic Collider Merge] 같은 오브젝트의 MeshCollider와 중복된 " +
            $"BoxCollider {duplicates.Count}개를 찾았습니다.");
    }

    [MenuItem("Soul Rush/Optimization/Gothic Stage/Remove Duplicate Box Colliders")]
    public static void RemoveDuplicateBoxColliders()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!TryFindDuplicateBoxColliders(out List<BoxCollider> duplicates))
            return;

        if (duplicates.Count == 0)
        {
            Debug.Log("[Gothic Collider Merge] 제거할 중복 BoxCollider가 없습니다.");
            return;
        }

        if (!EditorUtility.DisplayDialog(
                "중복 BoxCollider 제거",
                $"같은 오브젝트에 MeshCollider가 있는 BoxCollider {duplicates.Count}개를 제거합니다.\n" +
                "씬은 자동 저장하지 않으며 Undo 또는 Git으로 복원할 수 있습니다.",
                "제거",
                "취소"))
        {
            return;
        }

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Remove Duplicate Gothic Box Colliders");

        foreach (BoxCollider duplicate in duplicates)
        {
            if (duplicate != null)
                Undo.DestroyObjectImmediate(duplicate);
        }

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log(
            $"<color=green>[Gothic Collider Merge]</color> 중복 BoxCollider " +
            $"{duplicates.Count}개를 제거했습니다. 충돌을 확인한 뒤 씬을 직접 저장하세요.");
    }

    [MenuItem("Soul Rush/Optimization/Gothic Stage/Merge Adjacent Box Colliders")]
    public static void Merge()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!ValidateScene(scene))
            return;

        if (GameObject.Find(RootName) != null)
        {
            Debug.LogError(
                $"[Gothic Collider Merge] '{RootName}'이 이미 존재합니다. 중복 실행을 중단합니다.");
            return;
        }

        if (!TryBuildMergeResult(out List<MergeBox> result, out int sourceCount))
            return;

        int mergeGroupCount = 0;
        int mergedSourceCount = 0;
        foreach (MergeBox box in result)
        {
            if (box.Sources.Count < 2)
                continue;

            mergeGroupCount++;
            mergedSourceCount += box.Sources.Count;
        }

        if (mergeGroupCount == 0)
        {
            Debug.Log("[Gothic Collider Merge] 안전하게 병합할 수 있는 Collider가 없습니다.");
            return;
        }

        if (!EditorUtility.DisplayDialog(
                "Gothic Box Collider 병합",
                $"{mergedSourceCount}개의 기존 Collider를 {mergeGroupCount}개로 교체합니다.\n" +
                "씬은 자동 저장하지 않으며 Undo 또는 Git으로 복원할 수 있습니다.",
                "병합",
                "취소"))
        {
            return;
        }

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Merge Gothic Box Colliders");

        GameObject root = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(root, "Create merged collider root");
        SceneManager.MoveGameObjectToScene(root, scene);
        GameObjectUtility.SetStaticEditorFlags(root, StaticEditorFlags.BatchingStatic);

        int createdIndex = 0;
        HashSet<GameObject> sourceObjects = new HashSet<GameObject>();
        foreach (MergeBox box in result)
        {
            if (box.Sources.Count < 2)
                continue;

            GameObject mergedObject = new GameObject($"Merged_BoxCollider_{createdIndex:000}");
            Undo.RegisterCreatedObjectUndo(mergedObject, "Create merged box collider");
            mergedObject.transform.SetParent(root.transform, false);
            mergedObject.transform.position = box.Bounds.center;
            mergedObject.layer = box.Layer;
            GameObjectUtility.SetStaticEditorFlags(
                mergedObject,
                StaticEditorFlags.BatchingStatic |
                StaticEditorFlags.OccluderStatic |
                StaticEditorFlags.OccludeeStatic);

            BoxCollider mergedCollider = Undo.AddComponent<BoxCollider>(mergedObject);
            mergedCollider.center = Vector3.zero;
            mergedCollider.size = box.Bounds.size;
            mergedCollider.sharedMaterial = box.Material;
            mergedCollider.contactOffset = box.ContactOffset;

            foreach (BoxCollider source in box.Sources)
            {
                if (source != null)
                {
                    sourceObjects.Add(source.gameObject);
                    Undo.DestroyObjectImmediate(source);
                }
            }

            createdIndex++;
        }

        int removedEmptyObjects = 0;
        foreach (GameObject sourceObject in sourceObjects)
        {
            if (!CanRemoveEmptySourceObject(sourceObject))
                continue;

            Undo.DestroyObjectImmediate(sourceObject);
            removedEmptyObjects++;
        }

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(scene);

        int finalCount = sourceCount - mergedSourceCount + mergeGroupCount;
        Selection.activeGameObject = root;
        Debug.Log(
            $"<color=green>[Gothic Collider Merge]</color> {sourceCount}개 → {finalCount}개. " +
            $"{mergedSourceCount}개를 {mergeGroupCount}개로 병합했습니다. " +
            $"빈 Collider 전용 오브젝트 {removedEmptyObjects}개도 제거했습니다. " +
            "충돌을 확인한 뒤 씬을 직접 저장하세요.");
    }

    private static bool TryBuildMergeResult(out List<MergeBox> boxes, out int sourceCount)
    {
        boxes = new List<MergeBox>();
        sourceCount = 0;

        Scene scene = SceneManager.GetActiveScene();
        if (!ValidateScene(scene))
            return false;

        BoxCollider[] colliders = UnityEngine.Object.FindObjectsByType<BoxCollider>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        foreach (BoxCollider collider in colliders)
        {
            if (!IsMergeCandidate(collider, scene))
                continue;

            boxes.Add(new MergeBox(collider));
        }

        sourceCount = boxes.Count;
        bool merged;
        do
        {
            merged = false;
            for (int i = 0; i < boxes.Count && !merged; i++)
            {
                for (int j = i + 1; j < boxes.Count; j++)
                {
                    if (!CanMerge(boxes[i], boxes[j]))
                        continue;

                    boxes[i].Absorb(boxes[j]);
                    boxes.RemoveAt(j);
                    merged = true;
                    break;
                }
            }
        }
        while (merged);

        return true;
    }

    private static bool TryFindDuplicateBoxColliders(out List<BoxCollider> duplicates)
    {
        duplicates = new List<BoxCollider>();

        Scene scene = SceneManager.GetActiveScene();
        if (!ValidateScene(scene))
            return false;

        BoxCollider[] boxColliders = UnityEngine.Object.FindObjectsByType<BoxCollider>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        foreach (BoxCollider boxCollider in boxColliders)
        {
            if (boxCollider == null ||
                boxCollider.gameObject.scene != scene)
            {
                continue;
            }

            MeshCollider[] meshColliders = boxCollider.GetComponents<MeshCollider>();
            foreach (MeshCollider meshCollider in meshColliders)
            {
                if (boxCollider.enabled != meshCollider.enabled ||
                    boxCollider.isTrigger != meshCollider.isTrigger)
                {
                    continue;
                }

                duplicates.Add(boxCollider);
                break;
            }
        }

        return true;
    }

    private static bool ValidateScene(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
        {
            Debug.LogError("[Gothic Collider Merge] 열린 씬이 없습니다.");
            return false;
        }

        if (!string.Equals(scene.name, "Gothic_Stage", StringComparison.OrdinalIgnoreCase))
        {
            Debug.LogError(
                $"[Gothic Collider Merge] 현재 씬은 '{scene.name}'입니다. Gothic_Stage에서만 실행할 수 있습니다.");
            return false;
        }

        return true;
    }

    private static bool IsMergeCandidate(BoxCollider collider, Scene scene)
    {
        if (collider == null ||
            !collider.enabled ||
            collider.isTrigger ||
            !collider.gameObject.activeInHierarchy ||
            collider.gameObject.scene != scene ||
            collider.GetComponentInParent<Rigidbody>() != null ||
            collider.GetComponents<Collider>().Length != 1)
        {
            return false;
        }

        Transform transform = collider.transform;
        return IsWorldAxisAligned(transform.right) &&
               IsWorldAxisAligned(transform.up) &&
               IsWorldAxisAligned(transform.forward);
    }

    private static bool CanRemoveEmptySourceObject(GameObject sourceObject)
    {
        if (sourceObject == null ||
            sourceObject.transform.childCount != 0 ||
            PrefabUtility.IsPartOfPrefabInstance(sourceObject))
        {
            return false;
        }

        Component[] remainingComponents = sourceObject.GetComponents<Component>();
        return remainingComponents.Length == 1 &&
               remainingComponents[0] is Transform;
    }

    private static bool IsWorldAxisAligned(Vector3 direction)
    {
        direction.Normalize();
        float bestAlignment = Mathf.Max(
            Mathf.Abs(Vector3.Dot(direction, Vector3.right)),
            Mathf.Abs(Vector3.Dot(direction, Vector3.up)),
            Mathf.Abs(Vector3.Dot(direction, Vector3.forward)));
        return bestAlignment >= AxisTolerance;
    }

    private static bool CanMerge(MergeBox a, MergeBox b)
    {
        if (a.Layer != b.Layer ||
            a.Material != b.Material ||
            !Mathf.Approximately(a.ContactOffset, b.ContactOffset))
        {
            return false;
        }

        Bounds first = a.Bounds;
        Bounds second = b.Bounds;
        for (int mergeAxis = 0; mergeAxis < 3; mergeAxis++)
        {
            int axisA = (mergeAxis + 1) % 3;
            int axisB = (mergeAxis + 2) % 3;
            if (!SameInterval(first, second, axisA) ||
                !SameInterval(first, second, axisB))
            {
                continue;
            }

            if (IntervalsTouchOrOverlap(first, second, mergeAxis))
                return true;
        }

        return false;
    }

    private static bool SameInterval(Bounds a, Bounds b, int axis)
    {
        return Mathf.Abs(GetAxis(a.min, axis) - GetAxis(b.min, axis)) <= BoundsTolerance &&
               Mathf.Abs(GetAxis(a.max, axis) - GetAxis(b.max, axis)) <= BoundsTolerance;
    }

    private static bool IntervalsTouchOrOverlap(Bounds a, Bounds b, int axis)
    {
        float aMin = GetAxis(a.min, axis);
        float aMax = GetAxis(a.max, axis);
        float bMin = GetAxis(b.min, axis);
        float bMax = GetAxis(b.max, axis);
        return aMax + BoundsTolerance >= bMin &&
               bMax + BoundsTolerance >= aMin;
    }

    private static float GetAxis(Vector3 value, int axis)
    {
        return axis == 0 ? value.x : axis == 1 ? value.y : value.z;
    }

    private sealed class MergeBox
    {
        public Bounds Bounds;
        public readonly int Layer;
        public readonly PhysicMaterial Material;
        public readonly float ContactOffset;
        public readonly List<BoxCollider> Sources = new List<BoxCollider>();

        public MergeBox(BoxCollider source)
        {
            Bounds = source.bounds;
            Layer = source.gameObject.layer;
            Material = source.sharedMaterial;
            ContactOffset = source.contactOffset;
            Sources.Add(source);
        }

        public void Absorb(MergeBox other)
        {
            Bounds.Encapsulate(other.Bounds);
            Sources.AddRange(other.Sources);
        }
    }
}
