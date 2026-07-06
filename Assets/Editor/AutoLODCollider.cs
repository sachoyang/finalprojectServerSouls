using UnityEngine;
using UnityEditor;

public class AutoLODCollider : EditorWindow
{
    [MenuItem("Tools/Bonnate/Auto Fit BoxCollider to LOD0")]
    public static void FitBoxColliderToLOD0()
    {
        GameObject[] selectedObjs = Selection.gameObjects;
        if (selectedObjs.Length == 0)
        {
            Debug.LogWarning("[알림] 하이라키에서 콜라이더를 세팅할 부모 오브젝트들을 먼저 선택해주세요!");
            return;
        }

        int count = 0;
        // Ctrl+Z(실행 취소)를 위해 그룹으로 묶기
        Undo.SetCurrentGroupName("Auto Setup LOD Colliders");
        int undoGroup = Undo.GetCurrentGroup();

        foreach (GameObject root in selectedObjs)
        {
            LODGroup lodGroup = root.GetComponent<LODGroup>();
            if (lodGroup == null) continue;

            // 1. LOD 0의 렌더러(외형)들을 가져옵니다.
            LOD[] lods = lodGroup.GetLODs();
            if (lods.Length == 0 || lods[0].renderers == null || lods[0].renderers.Length == 0) continue;
            Renderer[] lod0Renderers = lods[0].renderers;

            // 2. 자식들(LOD 0, 1, 2)에 흩어져있는 찌꺼기 콜라이더 싹 다 지우기
            Collider[] childColliders = root.GetComponentsInChildren<Collider>(true);
            foreach (Collider c in childColliders)
            {
                if (c.gameObject != root) 
                {
                    Undo.DestroyObjectImmediate(c);
                }
            }

            // 3. 최상위 부모 기준의 통합 바운딩 박스 계산
            Bounds localBounds = new Bounds(Vector3.zero, Vector3.zero);
            bool hasBounds = false;

            foreach (Renderer r in lod0Renderers)
            {
                if (r == null) continue;

                // 유니티의 '자동 핏(Auto-Fit)' 기능을 훔치기 위해 자식에 잠시 BoxCollider를 답니다.
                BoxCollider tempBox = Undo.AddComponent<BoxCollider>(r.gameObject);

                // 자식의 로컬 박스 좌표를 -> 최상위 부모의 로컬 좌표로 정밀하게 변환
                Vector3 worldCenter = r.transform.TransformPoint(tempBox.center);
                Vector3 localCenter = root.transform.InverseTransformPoint(worldCenter);

                Vector3 worldSize = new Vector3(
                    tempBox.size.x * r.transform.lossyScale.x,
                    tempBox.size.y * r.transform.lossyScale.y,
                    tempBox.size.z * r.transform.lossyScale.z
                );
                Vector3 localSize = new Vector3(
                    Mathf.Abs(worldSize.x / root.transform.lossyScale.x),
                    Mathf.Abs(worldSize.y / root.transform.lossyScale.y),
                    Mathf.Abs(worldSize.z / root.transform.lossyScale.z)
                );

                Bounds boxBounds = new Bounds(localCenter, localSize);

                if (!hasBounds)
                {
                    localBounds = boxBounds;
                    hasBounds = true;
                }
                else
                {
                    localBounds.Encapsulate(boxBounds); // 집이 여러 부품이면 하나로 합침
                }

                // 임시로 달았던 BoxCollider 삭제 (흔적 지우기)
                Undo.DestroyObjectImmediate(tempBox);
            }

            // 4. 최상위 부모에 깔끔하게 BoxCollider 하나 딱 달아주기
            if (hasBounds)
            {
                BoxCollider rootBox = root.GetComponent<BoxCollider>();
                if (rootBox == null)
                {
                    rootBox = Undo.AddComponent<BoxCollider>(root);
                }
                else
                {
                    Undo.RecordObject(rootBox, "Update Root BoxCollider");
                }
                
                rootBox.center = localBounds.center;
                rootBox.size = localBounds.size;
                count++;
            }
        }

        Undo.CollapseUndoOperations(undoGroup);
        Debug.Log($"[완료] 총 {count}개의 LOD 오브젝트 최상위에 BoxCollider 자동 핏 세팅을 완료했습니다!");
    }
}