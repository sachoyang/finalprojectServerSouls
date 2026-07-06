using UnityEngine;
using UnityEditor;


public class RemoveBackgroundColliders : EditorWindow
{
    private float threshold = -146f;
    private enum Axis { X, Y, Z }
    private Axis targetAxis = Axis.X; // 기본값을 X축으로 설정 (필요시 Z축으로 변경하세요)

    [MenuItem("Tools/Bonnate/Remove Background Colliders")]
    public static void ShowWindow()
    {
        GetWindow<RemoveBackgroundColliders>("배경 콜라이더 삭제");
    }

    private void OnGUI()
    {
        GUILayout.Label("배경(원경) 콜라이더 일괄 삭제기", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        targetAxis = (Axis)EditorGUILayout.EnumPopup("기준 축 (Axis)", targetAxis);
        threshold = EditorGUILayout.FloatField("기준 좌표 (Threshold)", threshold);

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox($"현재 설정:\n선택한 오브젝트들 중 {targetAxis} 좌표가 [{threshold}] 보다 '큰(>)' 곳에 있는 콜라이더를 싹 다 지웁니다.", MessageType.Info);
        EditorGUILayout.Space();

        if (GUILayout.Button("선택한 오브젝트 하위 콜라이더 삭제 실행", GUILayout.Height(30)))
        {
            ExecuteRemoval();
        }
    }

    private void ExecuteRemoval()
    {
        GameObject[] selectedObjs = Selection.gameObjects;
        if (selectedObjs.Length == 0)
        {
            Debug.LogWarning("[알림] 하이라키에서 검사할 맵 전체(또는 배경 폴더)를 먼저 선택해주세요!");
            return;
        }

        int removedCount = 0;
        Undo.SetCurrentGroupName("Remove Background Colliders");
        int undoGroup = Undo.GetCurrentGroup();

        foreach (GameObject root in selectedObjs)
        {
            // 선택한 오브젝트와 그 자식들에 있는 모든 콜라이더를 찾습니다.
            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            
            foreach (Collider col in colliders)
            {
                float checkValue = 0f;
                
                // 오브젝트의 Pivot이 아닌, '콜라이더의 실제 중심점(Bounds Center)'을 기준으로 정밀하게 검사합니다.
                Vector3 center = col.bounds.center;

                switch (targetAxis)
                {
                    case Axis.X: checkValue = center.x; break;
                    case Axis.Y: checkValue = center.y; break;
                    case Axis.Z: checkValue = center.z; break;
                }

                // 지정한 좌표보다 크면 콜라이더 삭제!
                if (checkValue > threshold)
                {
                    Undo.DestroyObjectImmediate(col);
                    removedCount++;
                }
            }
        }

        Undo.CollapseUndoOperations(undoGroup);
        Debug.Log($"[완료] {threshold} 보다 큰 {targetAxis} 좌표에 있는 배경 콜라이더 {removedCount}개를 성공적으로 삭제했습니다!");
    }
}