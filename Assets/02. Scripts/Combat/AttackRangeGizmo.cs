using UnityEngine;

[ExecuteAlways]
public class AttackRangeGizmo : MonoBehaviour
{
    public enum DrawMode { Always, OnlyWhenSelected }

    [SerializeField] private DrawMode drawMode = DrawMode.Always;
    [SerializeField] private bool onlyShowAfterAttackInput;
    [SerializeField] private float visibleSecondsAfterAttack = 0.35f;
    [SerializeField] private Color fillColor = new Color(1f, 0.25f, 0f, 0.25f);
    [SerializeField] private Color wireColor = new Color(1f, 0.1f, 0f, 1f);
    [SerializeField] private bool showWireframe = true;
    [SerializeField] private bool drawCenterLine = true;
    [SerializeField] private float fallbackRadius = 1.4f;
    [SerializeField] private float fallbackDistance = 1.8f;
    [SerializeField] private float fallbackHeight = 1.1f;

    private NetworkPlayerController _player;
    private CombatSystem _combat;
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

        CombatSystem combat = GetCombat();
        Vector3 center = combat != null
            ? combat.BasicAttackHitLocalCenter
            : Vector3.up * fallbackHeight + Vector3.forward * fallbackDistance;
        float radius = combat != null ? combat.BasicAttackHitRadius : fallbackRadius;

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

    private CombatSystem GetCombat()
    {
        if (_combat == null)
        {
            _combat = FindFirstObjectByType<CombatSystem>();
        }

        return _combat;
    }
}
