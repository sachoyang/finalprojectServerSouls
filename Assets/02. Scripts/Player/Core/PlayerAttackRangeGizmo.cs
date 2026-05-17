using UnityEngine;

[ExecuteAlways]
public class PlayerAttackRangeGizmo : MonoBehaviour
{
    public enum DrawMode { Always, OnlyWhenSelected }

    [SerializeField] private DrawMode drawMode = DrawMode.Always;
    [SerializeField] private bool onlyShowAfterAttackInput;
    [SerializeField] private float visibleSecondsAfterAttack = 0.35f;
    [SerializeField] private Color fillColor = new Color(1f, 0.25f, 0f, 0.25f);
    [SerializeField] private Color wireColor = new Color(1f, 0.1f, 0f, 1f);
    [SerializeField] private bool showWireframe = true;
    [SerializeField] private bool drawCenterLine = true;

    private NetworkPlayerController _player;
    private float _showUntilTime;

    private void Update()
    {
        if (!Application.isPlaying || !onlyShowAfterAttackInput)
        {
            return;
        }

        NetworkPlayerController player = GetPlayer();
        if (player == null || player.Object == null || !player.Object.HasInputAuthority)
        {
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            _showUntilTime = Time.time + Mathf.Max(0.01f, visibleSecondsAfterAttack);
        }
    }

    private void OnDrawGizmos()
    {
        if (drawMode == DrawMode.Always)
        {
            DrawAttackRangeIfAllowed();
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (drawMode == DrawMode.OnlyWhenSelected)
        {
            DrawAttackRangeIfAllowed();
        }
    }

    private void DrawAttackRangeIfAllowed()
    {
        if (onlyShowAfterAttackInput && Application.isPlaying && Time.time > _showUntilTime)
        {
            return;
        }

        NetworkPlayerController player = GetPlayer();
        if (player == null)
        {
            return;
        }

        Gizmos.matrix = Matrix4x4.TRS(player.transform.position, player.transform.rotation, Vector3.one);

        Vector3 center = player.AttackHitLocalCenter;
        float radius = player.AttackHitRadius;

        if (fillColor.a > 0f)
        {
            Gizmos.color = fillColor;
            Gizmos.DrawSphere(center, radius);
        }

        if (showWireframe)
        {
            Gizmos.color = wireColor;
            Gizmos.DrawWireSphere(center, radius);
        }

        if (drawCenterLine)
        {
            Gizmos.color = wireColor;
            Gizmos.DrawLine(Vector3.up * center.y, center);
        }
    }

    private NetworkPlayerController GetPlayer()
    {
        if (_player == null)
        {
            _player = GetComponent<NetworkPlayerController>();
        }

        return _player;
    }
}
