using UnityEngine;
using UnityEngine.Rendering.Universal;

[DisallowMultipleComponent]
[RequireComponent(typeof(DecalProjector))]
public sealed class PlayerShadowDecalFollower : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private DecalProjector projector;
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField] private Vector3 raycastOffset = new Vector3(0f, 0.35f, 0f);
    [SerializeField] private float maxRaycastDistance = 3.5f;
    [SerializeField] private float surfaceOffset = 0.08f;
    [SerializeField] private Vector2 decalSize = new Vector2(1.25f, 1.6f);
    [SerializeField] private float projectionDepth = 1.8f;
    [SerializeField] private float drawDistance = 45f;
    [SerializeField] private bool useDistanceFade = true;
    [SerializeField] private float fadeOutHeight = 1.2f;
    [SerializeField] private bool followTargetYaw = true;

    private readonly RaycastHit[] hits = new RaycastHit[12];
    private float baseFadeFactor = 1f;

    private void Reset()
    {
        projector = GetComponent<DecalProjector>();
        target = transform.parent;
        EnsureGroundMask();
        ApplyProjectorDefaults();
    }

    private void Awake()
    {
        if (projector == null)
        {
            projector = GetComponent<DecalProjector>();
        }

        if (target == null)
        {
            target = transform.parent;
        }

        EnsureGroundMask();
        ApplyProjectorDefaults();
        CacheBaseFadeFactor();
    }

    private void OnValidate()
    {
        if (projector == null)
        {
            projector = GetComponent<DecalProjector>();
        }

        EnsureGroundMask();
        ApplyProjectorDefaults();
        CacheBaseFadeFactor();
    }

    private void LateUpdate()
    {
        if (target == null || projector == null)
        {
            SetVisible(false);
            return;
        }

        Vector3 rayOrigin = target.position + raycastOffset;
        if (!TryGetGroundHit(rayOrigin, out RaycastHit hit))
        {
            SetVisible(false);
            return;
        }

        SetVisible(true);
        transform.position = hit.point + hit.normal * surfaceOffset;

        Vector3 upReference = followTargetYaw ? Vector3.ProjectOnPlane(target.forward, hit.normal) : Vector3.forward;
        if (upReference.sqrMagnitude < 0.0001f)
        {
            upReference = Vector3.ProjectOnPlane(target.right, hit.normal);
        }

        if (upReference.sqrMagnitude < 0.0001f)
        {
            upReference = Vector3.forward;
        }

        transform.rotation = Quaternion.LookRotation(-hit.normal, upReference.normalized);

        ApplyDistanceFade(hit.distance);
    }

    private void ApplyProjectorDefaults()
    {
        if (projector == null)
        {
            return;
        }

        projector.size = new Vector3(decalSize.x, decalSize.y, projectionDepth);
        projector.pivot = Vector3.zero;
        projector.drawDistance = drawDistance;
    }

    private void SetVisible(bool visible)
    {
        if (projector != null)
        {
            projector.enabled = visible;
        }
    }

    private void CacheBaseFadeFactor()
    {
        if (projector != null)
        {
            baseFadeFactor = projector.fadeFactor;
        }
    }

    private void ApplyDistanceFade(float groundDistance)
    {
        if (projector == null)
        {
            return;
        }

        if (!useDistanceFade)
        {
            projector.fadeFactor = baseFadeFactor;
            return;
        }

        float airDistance = Mathf.Max(0f, groundDistance - raycastOffset.y);
        float fade = fadeOutHeight > 0f ? 1f - Mathf.Clamp01(airDistance / fadeOutHeight) : 1f;
        projector.fadeFactor = baseFadeFactor * fade;
    }

    private void EnsureGroundMask()
    {
        if (groundMask.value == 0)
        {
            groundMask = ~0;
        }
    }

    private bool TryGetGroundHit(Vector3 rayOrigin, out RaycastHit groundHit)
    {
        int hitCount = Physics.RaycastNonAlloc(rayOrigin, Vector3.down, hits, maxRaycastDistance, groundMask, QueryTriggerInteraction.Ignore);
        float closestDistance = float.MaxValue;
        groundHit = default;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = hits[i];
            if (target != null && hit.transform != null && hit.transform.IsChildOf(target))
            {
                continue;
            }

            if (hit.distance < closestDistance)
            {
                closestDistance = hit.distance;
                groundHit = hit;
            }
        }

        return closestDistance < float.MaxValue;
    }
}
