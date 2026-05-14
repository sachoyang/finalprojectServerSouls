using UnityEngine;

public class GizmoVisualizer : MonoBehaviour
{
    public enum GizmoShape { Sphere, Box, Ray }
    public enum DrawMode { Always, OnlyWhenSelected }

    [Header("모양 및 모드 설정")]
    public GizmoShape shape = GizmoShape.Sphere;
    public DrawMode drawMode = DrawMode.OnlyWhenSelected;

    [Header("색상 설정")]
    public Color fillColor = new Color(1f, 0.5f, 0f, 0.3f);
    public bool showWireframe = true;
    public Color wireColor = Color.red;

    [Header("위치 및 오프셋")]
    public Vector3 offset = Vector3.zero;

    [Header("크기 설정 (모양에 따라 적용)")]
    [Tooltip("Sphere 선택 시 적용되는 반지름")]
    public float radius = 2.0f;
    
    [Tooltip("Box 선택 시 적용되는 가로/세로/높이")]
    public Vector3 boxSize = Vector3.one;
    
    [Tooltip("Ray 선택 시 뻗어나가는 길이")]
    public float rayLength = 5.0f;

    // 항상 그리기 모드일 때 실행
    private void OnDrawGizmos()
    {
        if (drawMode == DrawMode.Always)
        {
            DrawCustomGizmo();
        }
    }

    // 오브젝트를 선택했을 때만 그리기 모드일 때 실행
    private void OnDrawGizmosSelected()
    {
        if (drawMode == DrawMode.OnlyWhenSelected)
        {
            DrawCustomGizmo();
        }
    }

    private void DrawCustomGizmo()
    {
        // [핵심] 현재 오브젝트의 회전값(Rotation)을 기즈모에 반영합니다.
        // 스케일은 물리 연산 수치의 정직함을 위해 1(Vector3.one)로 고정합니다.
        Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);

        // 1. 내부 채우기 (Alpha값이 0보다 클 때만)
        if (fillColor.a > 0)
        {
            Gizmos.color = fillColor;
            switch (shape)
            {
                case GizmoShape.Sphere:
                    Gizmos.DrawSphere(offset, radius);
                    break;
                case GizmoShape.Box:
                    Gizmos.DrawCube(offset, boxSize);
                    break;
                case GizmoShape.Ray:
                    Gizmos.DrawRay(offset, Vector3.forward * rayLength);
                    break;
            }
        }

        // 2. 테두리(와이어프레임) 그리기
        if (showWireframe)
        {
            Gizmos.color = wireColor;
            switch (shape)
            {
                case GizmoShape.Sphere:
                    Gizmos.DrawWireSphere(offset, radius);
                    break;
                case GizmoShape.Box:
                    Gizmos.DrawWireCube(offset, boxSize);
                    break;
                case GizmoShape.Ray:
                    // 레이의 끝점에 작은 구체를 하나 더 그려서 도달 지점을 명확히 보여줌
                    Gizmos.DrawSphere(offset + (Vector3.forward * rayLength), 0.1f);
                    break;
            }
        }
    }
}