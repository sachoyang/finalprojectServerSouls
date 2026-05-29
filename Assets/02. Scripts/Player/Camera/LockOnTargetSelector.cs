using System.Collections.Generic;
using UnityEngine;

public class LockOnTargetSelector : MonoBehaviour
{
    [Header("Search")]
    [SerializeField] private float searchRadius = 80f;
    [SerializeField] private string headTag = "LockOnHead";
    [SerializeField] private string bodyTag = "LockOnBody";

    private Transform _currentBossRoot;
    private int _currentIndex = -1;

    public void SetSearchRadius(float radius)
    {
        searchRadius = Mathf.Max(0f, radius);
    }

    public Transform SelectNextTarget(Transform owner, Transform currentTarget)
    {
        List<Transform> points = GetNearestBossLockOnPoints(owner);
        if (points.Count == 0)
        {
            Clear();
            return null;
        }

        Transform bossRoot = GetBossRoot(points[0]);
        if (_currentBossRoot != bossRoot)
        {
            _currentIndex = -1;
        }

        if (currentTarget != null)
        {
            int currentIndex = points.IndexOf(currentTarget);
            if (currentIndex >= 0)
            {
                _currentIndex = currentIndex;
            }
        }

        _currentBossRoot = bossRoot;
        _currentIndex = (_currentIndex + 1) % points.Count;
        return points[_currentIndex];
    }

    public void Clear()
    {
        _currentBossRoot = null;
        _currentIndex = -1;
    }

    private List<Transform> GetNearestBossLockOnPoints(Transform owner)
    {
        var nearestPoints = new List<Transform>();
        if (owner == null)
        {
            return nearestPoints;
        }

        GameObject[] heads = GameObject.FindGameObjectsWithTag(headTag);
        GameObject[] bodies = GameObject.FindGameObjectsWithTag(bodyTag);
        Transform nearestRoot = null;
        float nearestDistance = float.MaxValue;

        EvaluateNearestRoot(owner, heads, ref nearestRoot, ref nearestDistance);
        EvaluateNearestRoot(owner, bodies, ref nearestRoot, ref nearestDistance);

        if (nearestRoot == null || nearestDistance > searchRadius * searchRadius)
        {
            return nearestPoints;
        }

        AddRootLockOnPoints(heads, nearestRoot, nearestPoints);
        AddRootLockOnPoints(bodies, nearestRoot, nearestPoints);
        nearestPoints.Sort(CompareLockOnPoint);
        return nearestPoints;
    }

    private void EvaluateNearestRoot(
        Transform owner,
        GameObject[] candidates,
        ref Transform nearestRoot,
        ref float nearestDistance)
    {
        foreach (GameObject candidate in candidates)
        {
            if (candidate == null || !candidate.activeInHierarchy)
            {
                continue;
            }

            float sqrDistance = (candidate.transform.position - owner.position).sqrMagnitude;
            if (sqrDistance >= nearestDistance)
            {
                continue;
            }

            nearestRoot = GetBossRoot(candidate.transform);
            nearestDistance = sqrDistance;
        }
    }

    private static void AddRootLockOnPoints(GameObject[] candidates, Transform root, List<Transform> points)
    {
        foreach (GameObject candidate in candidates)
        {
            if (candidate == null || !candidate.activeInHierarchy)
            {
                continue;
            }

            if (GetBossRoot(candidate.transform) == root)
            {
                points.Add(candidate.transform);
            }
        }
    }

    private static Transform GetBossRoot(Transform point)
    {
        NetworkBossCore boss = point.GetComponentInParent<NetworkBossCore>();
        return boss != null ? boss.transform : point.root;
    }

    private int CompareLockOnPoint(Transform left, Transform right)
    {
        int leftPriority = left.CompareTag(headTag) ? 0 : 1;
        int rightPriority = right.CompareTag(headTag) ? 0 : 1;
        int priorityCompare = leftPriority.CompareTo(rightPriority);
        return priorityCompare != 0 ? priorityCompare : string.CompareOrdinal(left.name, right.name);
    }
}
