using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

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
    [Header("Skill Hit Events")]
    [SerializeField] private bool showEquippedSkillRanges = true;
    [SerializeField] private bool showSkillLabels = true;
    [SerializeField] private float fallbackRadius = 1.4f;
    [SerializeField] private float fallbackDistance = 1.8f;
    [SerializeField] private float fallbackHeight = 1.1f;

    private NetworkPlayerController _player;
    private PlayerAbilityExecutor _abilityExecutor;
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

        DrawEquippedSkillRanges();
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

    private void DrawEquippedSkillRanges()
    {
        if (!showEquippedSkillRanges || !Application.isPlaying)
        {
            return;
        }

        PlayerAbilityExecutor executor = GetAbilityExecutor();
        if (executor == null)
        {
            DrawSkillStatusLabel("PlayerAbilityExecutor 없음", Color.red);
            return;
        }

        PlayerAbilityModule module = executor.ActiveHitEventModule;
        AbilityHitEvent hitEvent = executor.ActiveHitEvent;
        if (module == null || hitEvent == null)
        {
            return;
        }

        DrawSkillEventCylinder(module, hitEvent, GetHitEventIndex(module, hitEvent));
    }

    private void DrawSkillEventCylinder(
        PlayerAbilityModule module,
        AbilityHitEvent hitEvent,
        int eventIndex)
    {
#if UNITY_EDITOR
        float radius = hitEvent.Radius;
        float bottomHeight = hitEvent.CenterHeight;
        float topHeight = bottomHeight + hitEvent.Height;
        Vector3 bottom = transform.position + Vector3.up * bottomHeight;
        Vector3 top = transform.position + Vector3.up * topHeight;
        Color color = hitEvent.PreviewColor;
        color.a = 1f;

        Handles.color = color;
        Handles.DrawWireDisc(bottom, Vector3.up, radius);
        Handles.DrawWireDisc(top, Vector3.up, radius);
        Handles.DrawLine(bottom + Vector3.forward * radius, top + Vector3.forward * radius);
        Handles.DrawLine(bottom - Vector3.forward * radius, top - Vector3.forward * radius);
        Handles.DrawLine(bottom + Vector3.right * radius, top + Vector3.right * radius);
        Handles.DrawLine(bottom - Vector3.right * radius, top - Vector3.right * radius);

        if (showSkillLabels)
        {
            CombatSystem combat = GetCombat();
            string diagnostics = combat != null
                ? $"\nPhysics {combat.LastAbilityRawHitCount} / Cylinder {combat.LastAbilityFilteredHitCount} / Boss {combat.LastAbilityBossHurtboxCount}"
                : "\nCombatSystem 없음";
            Handles.Label(
                top + Vector3.up * 0.1f,
                $"{module.AbilityId} / Hit {eventIndex + 1}\n" +
                $"R {radius:0.00}, H {hitEvent.Height:0.00}, " +
                $"Hit x{hitEvent.DamageRate:0.##}, Lv.1 x{module.GetDamageMultiplier(1):0.##}" +
                diagnostics);
        }
#endif
    }

    private void DrawSkillStatusLabel(string message, Color color)
    {
#if UNITY_EDITOR
        if (!showSkillLabels)
        {
            return;
        }

        Handles.color = color;
        Handles.Label(transform.position + Vector3.up * 2.2f, message);
#endif
    }

    private PlayerAbilityExecutor GetAbilityExecutor()
    {
        if (_abilityExecutor == null)
        {
            _abilityExecutor = GetComponent<PlayerAbilityExecutor>();
        }

        return _abilityExecutor;
    }

    private static int GetHitEventIndex(PlayerAbilityModule module, AbilityHitEvent activeEvent)
    {
        AbilityHitEvent[] hitEvents = module.HitEvents;
        if (hitEvents == null)
        {
            return 0;
        }

        for (int i = 0; i < hitEvents.Length; i++)
        {
            if (ReferenceEquals(hitEvents[i], activeEvent))
            {
                return i;
            }
        }

        return 0;
    }
}
