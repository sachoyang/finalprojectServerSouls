using System;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(10050)]
public class CapeSpringBone : MonoBehaviour
{
    [Serializable]
    private sealed class SpringNode
    {
        public Transform bone;
        public Transform child;
        public Quaternion restLocalRotation;
        public Vector3 currentTailPosition;
        public Vector3 previousTailPosition;
        public float length;
    }

    [Header("Roots")]
    [SerializeField] private Transform capeRoot;
    [SerializeField] private Transform[] rootBones;
    [SerializeField] private bool autoFindRootBones = true;
    [SerializeField] private bool autoMappedOnce;

    [Header("Spring")]
    [SerializeField, Range(0f, 1f)] private float stiffness = 0.12f;
    [SerializeField, Range(0f, 1f)] private float drag = 0.12f;
    [SerializeField] private Vector3 gravity = new Vector3(0f, -2f, 0f);
    [SerializeField, Range(0f, 1f)] private float movementInfluence = 0.12f;
    [SerializeField] private float maxAngle = 85f;

    [Header("Collision")]
    [SerializeField] private float collisionRadius = 0.03f;
    [SerializeField] private bool autoFindBodyColliders = true;
    [SerializeField] private Transform colliderSearchRoot;
    [SerializeField] private Collider[] colliders;

    [Header("Debug")]
    [SerializeField] private int foundRootBoneCount;
    [SerializeField] private int foundColliderCount;
    [SerializeField] private int simulatedNodeCount;

    private readonly List<SpringNode> _nodes = new List<SpringNode>();
    private Vector3 _lastRootPosition;
    private bool _initialized;

    private void Awake()
    {
        Rebuild();
    }

    private void Start()
    {
        Rebuild();
    }

    private void OnEnable()
    {
        Rebuild();
        ResetSimulation();
    }

    private void Reset()
    {
        AutoMapOnce();
    }

    private void OnValidate()
    {
        UpdateDebugCounts();
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying || !_initialized || _nodes.Count == 0)
        {
            return;
        }

        float deltaTime = Mathf.Max(Time.deltaTime, 0.0001f);
        Vector3 rootVelocity = (transform.position - _lastRootPosition) / deltaTime;
        _lastRootPosition = transform.position;

        for (int i = 0; i < _nodes.Count; i++)
        {
            SimulateNode(_nodes[i], rootVelocity, deltaTime);
        }
    }

    [ContextMenu("Rebuild")]
    public void Rebuild()
    {
        _nodes.Clear();
        if (rootBones != null)
        {
            for (int i = 0; i < rootBones.Length; i++)
            {
                AddChain(rootBones[i]);
            }
        }

        _initialized = true;
        UpdateDebugCounts();
        ResetSimulation();
    }

    [ContextMenu("Auto Map Once")]
    public void AutoMapOnce()
    {
        if (autoMappedOnce)
        {
            Rebuild();
            return;
        }

        AutoMapNow();
        autoMappedOnce = true;
    }

    [ContextMenu("Auto Map Now")]
    public void AutoMapNow()
    {
        if (autoFindRootBones)
        {
            AutoFindRootBones();
        }

        if (autoFindBodyColliders)
        {
            AutoFindBodyColliders();
        }

        Rebuild();
    }

    [ContextMenu("Reset Simulation")]
    public void ResetSimulation()
    {
        _lastRootPosition = transform.position;
        for (int i = 0; i < _nodes.Count; i++)
        {
            SpringNode node = _nodes[i];
            if (node.bone == null || node.child == null)
            {
                continue;
            }

            node.restLocalRotation = node.bone.localRotation;
            node.length = Vector3.Distance(node.bone.position, node.child.position);
            node.currentTailPosition = node.child.position;
            node.previousTailPosition = node.child.position;
        }
    }

    [ContextMenu("Auto Find Root Bones")]
    private void AutoFindRootBones()
    {
        Transform searchRoot = capeRoot != null ? capeRoot : FindDeepChild(transform, "cape");
        if (searchRoot == null)
        {
            return;
        }

        capeRoot = searchRoot;
        List<Transform> roots = new List<Transform>();
        for (int i = 0; i < searchRoot.childCount; i++)
        {
            Transform child = searchRoot.GetChild(i);
            if (child.name.StartsWith("cape", StringComparison.OrdinalIgnoreCase))
            {
                roots.Add(child);
            }
        }

        rootBones = roots.ToArray();
        UpdateDebugCounts();
    }

    [ContextMenu("Auto Find Body Colliders")]
    private void AutoFindBodyColliders()
    {
        Transform searchRoot = colliderSearchRoot != null ? colliderSearchRoot : transform;
        Collider[] allColliders = searchRoot.GetComponentsInChildren<Collider>(true);
        List<Collider> bodyColliders = new List<Collider>();
        for (int i = 0; i < allColliders.Length; i++)
        {
            Collider bodyCollider = allColliders[i];
            if (bodyCollider == null || bodyCollider.isTrigger || IsCapeTransform(bodyCollider.transform))
            {
                continue;
            }

            bodyColliders.Add(bodyCollider);
        }

        colliders = bodyColliders.ToArray();
        UpdateDebugCounts();
    }

    private void AddChain(Transform root)
    {
        Transform current = root;
        while (current != null)
        {
            Transform next = GetCapeChild(current);
            if (next == null)
            {
                break;
            }

            _nodes.Add(new SpringNode
            {
                bone = current,
                child = next,
                restLocalRotation = current.localRotation,
                currentTailPosition = next.position,
                previousTailPosition = next.position,
                length = Vector3.Distance(current.position, next.position)
            });

            current = next;
        }

        UpdateDebugCounts();
    }

    private void SimulateNode(SpringNode node, Vector3 rootVelocity, float deltaTime)
    {
        if (node.bone == null || node.child == null || node.length <= 0.0001f)
        {
            return;
        }

        Vector3 bonePosition = node.bone.position;
        Vector3 restTailPosition = node.child.position;
        Vector3 velocity = (node.currentTailPosition - node.previousTailPosition) * Mathf.Clamp01(1f - drag);
        Vector3 externalForce = gravity + rootVelocity * -movementInfluence;

        node.previousTailPosition = node.currentTailPosition;
        node.currentTailPosition += velocity;
        node.currentTailPosition += externalForce * (deltaTime * deltaTime);
        node.currentTailPosition += (restTailPosition - node.currentTailPosition) * (stiffness * deltaTime);

        Vector3 direction = node.currentTailPosition - bonePosition;
        if (direction.sqrMagnitude < 0.000001f)
        {
            direction = node.child.position - bonePosition;
        }

        direction.Normalize();
        Vector3 restDirection = (restTailPosition - bonePosition).normalized;
        direction = ClampDirection(restDirection, direction, maxAngle);
        node.currentTailPosition = bonePosition + direction * node.length;
        ResolveCollisions(ref node.currentTailPosition);

        Quaternion rotation = Quaternion.FromToRotation(restDirection, direction);
        node.bone.rotation = rotation * node.bone.rotation;
    }

    private void ResolveCollisions(ref Vector3 position)
    {
        if (colliders == null || collisionRadius <= 0f)
        {
            return;
        }

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider bodyCollider = colliders[i];
            if (bodyCollider == null)
            {
                continue;
            }

            Vector3 closest = bodyCollider.ClosestPoint(position);
            Vector3 away = position - closest;
            float sqrDistance = away.sqrMagnitude;
            if (sqrDistance >= collisionRadius * collisionRadius)
            {
                continue;
            }

            if (sqrDistance < 0.000001f)
            {
                away = (position - bodyCollider.bounds.center).normalized;
                if (away.sqrMagnitude < 0.000001f)
                {
                    away = Vector3.up;
                }
            }
            else
            {
                away /= Mathf.Sqrt(sqrDistance);
            }

            position = closest + away * collisionRadius;
        }
    }

    private static Vector3 ClampDirection(Vector3 restDirection, Vector3 direction, float maxDegrees)
    {
        if (maxDegrees <= 0f)
        {
            return restDirection;
        }

        float angle = Vector3.Angle(restDirection, direction);
        if (angle <= maxDegrees)
        {
            return direction;
        }

        return Vector3.Slerp(restDirection, direction, maxDegrees / angle).normalized;
    }

    private static Transform GetCapeChild(Transform parent)
    {
        if (parent == null)
        {
            return null;
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name.StartsWith(parent.name, StringComparison.OrdinalIgnoreCase) ||
                child.name.StartsWith("cape", StringComparison.OrdinalIgnoreCase))
            {
                return child;
            }
        }

        return null;
    }

    private bool IsCapeTransform(Transform candidate)
    {
        return capeRoot != null && candidate != null && candidate.IsChildOf(capeRoot);
    }

    private bool HasValidCollider()
    {
        if (colliders == null)
        {
            return false;
        }

        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
            {
                return true;
            }
        }

        return false;
    }

    private void UpdateDebugCounts()
    {
        foundRootBoneCount = CountValidTransforms(rootBones);
        foundColliderCount = CountValidColliders(colliders);
        simulatedNodeCount = _nodes.Count;
    }

    private static int CountValidTransforms(Transform[] values)
    {
        if (values == null)
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < values.Length; i++)
        {
            if (values[i] != null)
            {
                count++;
            }
        }

        return count;
    }

    private static int CountValidColliders(Collider[] values)
    {
        if (values == null)
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < values.Length; i++)
        {
            if (values[i] != null)
            {
                count++;
            }
        }

        return count;
    }

    private static Transform FindDeepChild(Transform parent, string childName)
    {
        if (parent == null)
        {
            return null;
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name.Equals(childName, StringComparison.OrdinalIgnoreCase))
            {
                return child;
            }

            Transform result = FindDeepChild(child, childName);
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }
}
