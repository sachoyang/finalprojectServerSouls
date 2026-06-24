using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(10050)]
public class CapeSpringBone : MonoBehaviour, IStateResetReceiver
{
    [Serializable]
    private sealed class Particle
    {
        public Transform joint;
        public Vector3 position;
        public Vector3 previousPosition;
        public Vector3 restPosition;
        public int chainIndex;
        public int rowIndex;
        public bool pinned;
    }

    [Serializable]
    private sealed class BoneSegment
    {
        public Transform bone;
        public Transform child;
        public Quaternion restLocalRotation;
        public Particle head;
        public Particle tail;
    }

    [Header("Roots")]
    [SerializeField] private Transform capeRoot;
    [SerializeField] private Transform[] rootBones;
    [SerializeField] private bool autoFindRootBones = true;
    [SerializeField] private bool autoMappedOnce;

    [Header("Dynamics")]
    [SerializeField] private Vector3 gravity = new Vector3(0f, -9.81f, 0f);
    [SerializeField, Min(0f)] private float damping = 8f;
    [SerializeField, Min(0f)] private float shapeReturn = 3f;
    [SerializeField, Range(0f, 1f)] private float translationFollow = 0.85f;

    [Header("Grid Constraints")]
    [SerializeField, Range(0f, 1f)] private float verticalStiffness = 0.9f;
    [SerializeField, Range(0f, 1f)] private float horizontalStiffness = 0.4f;
    [SerializeField, Range(0f, 1f)] private float shearStiffness = 0.2f;
    [SerializeField, Range(0f, 1f)] private float bendStiffness = 0.08f;
    [SerializeField, Range(1, 12)] private int solverIterations = 4;
    [SerializeField, Range(1f, 2f)] private float maximumWidthStretch = 1.2f;

    [Header("Recovery")]
    [SerializeField, Min(0f)] private float crawlingResetDelay = 0.1f;
    [SerializeField, Min(0f)] private float allowedForwardOffset = 0.05f;
    [SerializeField, Min(0f)] private float forwardRecovery = 25f;

    [Header("Collision")]
    [SerializeField] private float collisionRadius = 0.02f;
    [SerializeField] private bool autoFindBodyColliders = true;
    [SerializeField] private Transform colliderSearchRoot;
    [SerializeField] private Collider[] colliders;

    [Header("Debug")]
    [SerializeField] private int foundRootBoneCount;
    [SerializeField] private int foundColliderCount;
    [SerializeField] private int simulatedNodeCount;

    private readonly List<List<Particle>> _chains = new List<List<Particle>>();
    private readonly List<BoneSegment> _segments = new List<BoneSegment>();
    private Vector3 _lastRootPosition;
    private bool _initialized;
    private int _maximumRowCount;
    private Coroutine _crawlingResetRoutine;

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
    }

    private void OnDisable()
    {
        if (_crawlingResetRoutine != null)
        {
            StopCoroutine(_crawlingResetRoutine);
            _crawlingResetRoutine = null;
        }
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
        if (!Application.isPlaying || !_initialized || _segments.Count == 0)
        {
            return;
        }

        float deltaTime = Mathf.Clamp(Time.deltaTime, 0.0001f, 0.0333f);
        Vector3 rootPosition = transform.position;
        Vector3 rootDelta = rootPosition - _lastRootPosition;
        _lastRootPosition = rootPosition;

        RestoreRestPose();
        CaptureRestPositions();

        if (!IsFinite(rootDelta) || rootDelta.sqrMagnitude > 4f)
        {
            ResetSimulation();
            return;
        }

        FollowRootTranslation(rootDelta);
        IntegrateParticles(deltaTime);

        int iterations = Mathf.Max(1, solverIterations);
        for (int i = 0; i < iterations; i++)
        {
            PinTopRow();
            SolveVerticalConstraints();
            SolveWidthConstraints();
            SolveShearConstraints();
            SolveBendConstraints();
            ResolveParticleCollisions();
        }

        PinTopRow();
        ApplyBoneRotations();
    }

    [ContextMenu("Rebuild")]
    public void Rebuild()
    {
        _chains.Clear();
        _segments.Clear();
        _maximumRowCount = 0;

        if (rootBones != null)
        {
            for (int chainIndex = 0; chainIndex < rootBones.Length; chainIndex++)
            {
                AddChain(rootBones[chainIndex], chainIndex);
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
        if (!_initialized)
        {
            return;
        }

        RestoreRestPose();
        _lastRootPosition = transform.position;
        for (int chain = 0; chain < _chains.Count; chain++)
        {
            List<Particle> particles = _chains[chain];
            for (int row = 0; row < particles.Count; row++)
            {
                Particle particle = particles[row];
                Vector3 position = particle.joint != null ? particle.joint.position : transform.position;
                particle.position = position;
                particle.previousPosition = position;
                particle.restPosition = position;
            }
        }
    }

    public void ResetForCrawlingState()
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        if (_crawlingResetRoutine != null)
        {
            StopCoroutine(_crawlingResetRoutine);
        }

        _crawlingResetRoutine = StartCoroutine(ResetForCrawlingPose());
    }

    public void ResetForAnimatorState(string resetKey)
    {
        if (string.Equals(resetKey, "Crawling", StringComparison.OrdinalIgnoreCase))
        {
            ResetForCrawlingState();
        }
    }

    private IEnumerator ResetForCrawlingPose()
    {
        yield return new WaitForSeconds(crawlingResetDelay);
        ResetSimulation();
        _crawlingResetRoutine = null;
    }

    private void AddChain(Transform root, int chainIndex)
    {
        if (root == null)
        {
            return;
        }

        List<Transform> joints = new List<Transform>();
        Transform current = root;
        while (current != null)
        {
            joints.Add(current);
            current = GetCapeChild(current);
        }

        if (joints.Count < 2)
        {
            return;
        }

        List<Particle> particles = new List<Particle>(joints.Count);
        for (int row = 0; row < joints.Count; row++)
        {
            Vector3 position = joints[row].position;
            particles.Add(new Particle
            {
                joint = joints[row],
                position = position,
                previousPosition = position,
                restPosition = position,
                chainIndex = chainIndex,
                rowIndex = row,
                pinned = row == 0
            });
        }

        _chains.Add(particles);
        _maximumRowCount = Mathf.Max(_maximumRowCount, particles.Count);
        for (int row = 0; row < particles.Count - 1; row++)
        {
            _segments.Add(new BoneSegment
            {
                bone = joints[row],
                child = joints[row + 1],
                restLocalRotation = joints[row].localRotation,
                head = particles[row],
                tail = particles[row + 1]
            });
        }
    }

    private void RestoreRestPose()
    {
        for (int i = 0; i < _segments.Count; i++)
        {
            BoneSegment segment = _segments[i];
            if (segment.bone != null)
            {
                segment.bone.localRotation = segment.restLocalRotation;
            }
        }
    }

    private void CaptureRestPositions()
    {
        for (int chain = 0; chain < _chains.Count; chain++)
        {
            List<Particle> particles = _chains[chain];
            for (int row = 0; row < particles.Count; row++)
            {
                if (particles[row].joint != null)
                {
                    particles[row].restPosition = particles[row].joint.position;
                }
            }
        }
    }

    private void FollowRootTranslation(Vector3 rootDelta)
    {
        for (int chain = 0; chain < _chains.Count; chain++)
        {
            List<Particle> particles = _chains[chain];
            for (int row = 1; row < particles.Count; row++)
            {
                Particle particle = particles[row];
                Vector3 followDelta = rootDelta * translationFollow;
                particle.position += followDelta;
                particle.previousPosition += followDelta;
            }
        }
    }

    private void IntegrateParticles(float deltaTime)
    {
        for (int chain = 0; chain < _chains.Count; chain++)
        {
            List<Particle> particles = _chains[chain];
            for (int row = 1; row < particles.Count; row++)
            {
                Particle particle = particles[row];
                float retention = Mathf.Exp(-damping * deltaTime);
                Vector3 velocity = (particle.position - particle.previousPosition) * retention;
                particle.previousPosition = particle.position;
                Vector3 acceleration = gravity +
                                       (particle.restPosition - particle.position) * shapeReturn;
                float forwardDistance = Vector3.Dot(
                    particle.position - particle.restPosition,
                    transform.forward);
                if (forwardDistance > allowedForwardOffset)
                {
                    acceleration -= transform.forward *
                                    ((forwardDistance - allowedForwardOffset) * forwardRecovery);
                }

                particle.position += velocity + acceleration * (deltaTime * deltaTime);
            }
        }
    }

    private void PinTopRow()
    {
        for (int chain = 0; chain < _chains.Count; chain++)
        {
            if (_chains[chain].Count == 0)
            {
                continue;
            }

            Particle top = _chains[chain][0];
            top.position = top.restPosition;
            top.previousPosition = top.restPosition;
        }
    }

    private void SolveVerticalConstraints()
    {
        for (int chain = 0; chain < _chains.Count; chain++)
        {
            List<Particle> particles = _chains[chain];
            for (int row = 0; row < particles.Count - 1; row++)
            {
                SolveDistance(
                    particles[row],
                    particles[row + 1],
                    RestDistance(particles[row], particles[row + 1]),
                    verticalStiffness,
                    1f);
            }
        }
    }

    private void SolveWidthConstraints()
    {
        for (int chain = 0; chain < _chains.Count - 1; chain++)
        {
            int rows = Mathf.Min(_chains[chain].Count, _chains[chain + 1].Count);
            for (int row = 0; row < rows; row++)
            {
                Particle left = _chains[chain][row];
                Particle right = _chains[chain + 1][row];
                SolveDistance(
                    left,
                    right,
                    RestDistance(left, right),
                    horizontalStiffness,
                    maximumWidthStretch);
            }
        }
    }

    private void SolveShearConstraints()
    {
        for (int chain = 0; chain < _chains.Count - 1; chain++)
        {
            int rows = Mathf.Min(_chains[chain].Count, _chains[chain + 1].Count) - 1;
            for (int row = 0; row < rows; row++)
            {
                Particle leftTop = _chains[chain][row];
                Particle leftBottom = _chains[chain][row + 1];
                Particle rightTop = _chains[chain + 1][row];
                Particle rightBottom = _chains[chain + 1][row + 1];
                SolveDistance(leftTop, rightBottom, RestDistance(leftTop, rightBottom), shearStiffness, 1.2f);
                SolveDistance(rightTop, leftBottom, RestDistance(rightTop, leftBottom), shearStiffness, 1.2f);
            }
        }
    }

    private void SolveBendConstraints()
    {
        for (int chain = 0; chain < _chains.Count; chain++)
        {
            List<Particle> particles = _chains[chain];
            for (int row = 0; row < particles.Count - 2; row++)
            {
                SolveDistance(
                    particles[row],
                    particles[row + 2],
                    RestDistance(particles[row], particles[row + 2]),
                    bendStiffness,
                    1.1f);
            }
        }

        for (int chain = 0; chain < _chains.Count - 2; chain++)
        {
            int rows = Mathf.Min(_chains[chain].Count, _chains[chain + 2].Count);
            for (int row = 0; row < rows; row++)
            {
                Particle left = _chains[chain][row];
                Particle right = _chains[chain + 2][row];
                SolveDistance(left, right, RestDistance(left, right), bendStiffness, 1.1f);
            }
        }
    }

    private static void SolveDistance(
        Particle first,
        Particle second,
        float restDistance,
        float constraintStrength,
        float maximumStretchRatio)
    {
        if (first == null || second == null || restDistance <= 0.0001f || constraintStrength <= 0f)
        {
            return;
        }

        Vector3 delta = second.position - first.position;
        float distance = delta.magnitude;
        if (distance <= 0.0001f)
        {
            return;
        }

        float targetDistance = Mathf.Min(distance, restDistance * maximumStretchRatio);
        targetDistance = Mathf.Lerp(targetDistance, restDistance, constraintStrength);
        float error = distance - targetDistance;
        Vector3 correction = delta / distance * error;
        float firstWeight = first.pinned ? 0f : 1f;
        float secondWeight = second.pinned ? 0f : 1f;
        float totalWeight = firstWeight + secondWeight;
        if (totalWeight <= 0f)
        {
            return;
        }

        Vector3 firstCorrection = correction * (firstWeight / totalWeight);
        Vector3 secondCorrection = -correction * (secondWeight / totalWeight);
        first.position += firstCorrection;
        first.previousPosition += firstCorrection;
        second.position += secondCorrection;
        second.previousPosition += secondCorrection;
    }

    private void ResolveParticleCollisions()
    {
        if (colliders == null || collisionRadius <= 0f)
        {
            return;
        }

        for (int chain = 0; chain < _chains.Count; chain++)
        {
            List<Particle> particles = _chains[chain];
            for (int row = 1; row < particles.Count; row++)
            {
                Particle particle = particles[row];
                Vector3 positionBeforeCollision = particle.position;
                ResolveCollisions(particle);
                particle.previousPosition += particle.position - positionBeforeCollision;
            }
        }
    }

    private void ResolveCollisions(Particle particle)
    {
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider bodyCollider = colliders[i];
            if (bodyCollider == null)
            {
                continue;
            }

            Vector3 closest = bodyCollider.ClosestPoint(particle.position);
            Vector3 away = particle.position - closest;
            float sqrDistance = away.sqrMagnitude;
            if (sqrDistance >= collisionRadius * collisionRadius)
            {
                continue;
            }

            if (sqrDistance <= 0.000001f)
            {
                away = particle.restPosition - bodyCollider.bounds.center;
                if (away.sqrMagnitude <= 0.000001f)
                {
                    away = -transform.forward;
                }
            }

            particle.position = closest + away.normalized * collisionRadius;
        }
    }

    private void ApplyBoneRotations()
    {
        for (int row = 0; row < _maximumRowCount - 1; row++)
        {
            for (int chain = 0; chain < _chains.Count; chain++)
            {
                BoneSegment segment = FindSegment(chain, row);
                if (segment == null || segment.bone == null || segment.child == null)
                {
                    continue;
                }

                Vector3 currentDirection = segment.child.position - segment.bone.position;
                Vector3 desiredDirection = segment.tail.position - segment.bone.position;
                if (currentDirection.sqrMagnitude <= 0.000001f || desiredDirection.sqrMagnitude <= 0.000001f)
                {
                    continue;
                }

                Vector3 restDirection = currentDirection.normalized;
                segment.bone.rotation = Quaternion.FromToRotation(
                    restDirection,
                    desiredDirection.normalized) * segment.bone.rotation;
                Vector3 projectedPosition = segment.child.position;
                Vector3 projectionCorrection = projectedPosition - segment.tail.position;
                segment.tail.position = projectedPosition;
                segment.tail.previousPosition += projectionCorrection;
            }
        }
    }

    private BoneSegment FindSegment(int chainIndex, int rowIndex)
    {
        for (int i = 0; i < _segments.Count; i++)
        {
            BoneSegment segment = _segments[i];
            if (segment.head.chainIndex == chainIndex && segment.head.rowIndex == rowIndex)
            {
                return segment;
            }
        }

        return null;
    }

    private static float RestDistance(Particle first, Particle second)
    {
        return Vector3.Distance(first.restPosition, second.restPosition);
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

    private void UpdateDebugCounts()
    {
        foundRootBoneCount = CountValidTransforms(rootBones);
        foundColliderCount = CountValidColliders(colliders);
        simulatedNodeCount = _segments.Count;
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

    private static bool IsFinite(Vector3 value)
    {
        return !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
               !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
               !float.IsNaN(value.z) && !float.IsInfinity(value.z);
    }
}
