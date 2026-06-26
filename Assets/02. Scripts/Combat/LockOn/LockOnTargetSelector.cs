using System.Collections.Generic;
using UnityEngine;

public class LockOnTargetSelector : MonoBehaviour
{
    [Header("Search")]
    [SerializeField] private float searchRadius = 80f;

    private readonly List<TargetGroup> _targetGroups = new List<TargetGroup>();
    private readonly List<TargetCandidate> _candidates = new List<TargetCandidate>();
    private readonly List<TargetCandidate> _pointCandidates = new List<TargetCandidate>();
    private readonly List<LockOnTargetPoint> _pointBuffer = new List<LockOnTargetPoint>();

    private int _currentIndex = -1;

    public void SetSearchRadius(float radius)
    {
        searchRadius = Mathf.Max(0f, radius);
    }

    public Transform SelectNextTarget(Transform owner, Transform currentTarget, Camera viewCamera)
    {
        // 컴포넌트 기반 후보만 사용한다.
        // 대상 Root를 화면 중심 기준으로 정렬하고, 각 Root 안의 Point를 priority 순서대로 모두 순회한 뒤 다음 Root로 넘어간다.
        List<TargetCandidate> targets = GetVisibleTargetsSortedByScreenCenter(owner, viewCamera);
        if (targets.Count == 0)
        {
            Clear();
            return null;
        }

        if (currentTarget == null)
        {
            _currentIndex = -1;
        }
        else
        {
            int currentIndex = FindCurrentTargetIndex(targets, currentTarget);
            if (currentIndex >= 0)
            {
                _currentIndex = currentIndex;
            }
            else
            {
                // 현재 바라보던 Point가 시야 밖으로 나가 후보 목록에서 빠졌다면 다시 첫 후보부터 선택한다.
                _currentIndex = -1;
            }
        }

        _currentIndex = (_currentIndex + 1) % targets.Count;
        return targets[_currentIndex].Point;
    }

    public bool IsCurrentTargetValid(Transform owner, Transform currentTarget)
    {
        if (owner == null || currentTarget == null || !currentTarget.gameObject.activeInHierarchy)
        {
            return false;
        }

        LockOnTargetRoot root = currentTarget.GetComponentInParent<LockOnTargetRoot>();
        return IsRootSelectable(owner, root);
    }

    public void Clear()
    {
        _currentIndex = -1;
    }

    private List<TargetCandidate> GetVisibleTargetsSortedByScreenCenter(Transform owner, Camera viewCamera)
    {
        _targetGroups.Clear();
        _candidates.Clear();

        if (owner == null)
        {
            return _candidates;
        }

        Camera camera = viewCamera != null ? viewCamera : Camera.main;
        if (camera == null)
        {
            return _candidates;
        }

        LockOnTargetRoot[] roots = FindObjectsOfType<LockOnTargetRoot>(false);
        foreach (LockOnTargetRoot root in roots)
        {
            TryAddTargetGroup(owner, camera, root);
        }

        _targetGroups.Sort(CompareTargetGroup);
        foreach (TargetGroup group in _targetGroups)
        {
            group.Points.Sort(ComparePointInsideSameTarget);
            _candidates.AddRange(group.Points);
        }

        return _candidates;
    }

    private void TryAddTargetGroup(Transform owner, Camera camera, LockOnTargetRoot root)
    {
        if (!IsRootSelectable(owner, root))
        {
            return;
        }

        _pointBuffer.Clear();
        root.GetComponentsInChildren(false, _pointBuffer);
        if (_pointBuffer.Count == 0)
        {
            return;
        }

        _pointCandidates.Clear();
        float groupCenterDistance = float.MaxValue;
        float groupSqrDistance = float.MaxValue;

        foreach (LockOnTargetPoint point in _pointBuffer)
        {
            if (!TryCreatePointCandidate(owner, camera, root.transform, point, out TargetCandidate candidate))
            {
                continue;
            }

            _pointCandidates.Add(candidate);
            groupCenterDistance = Mathf.Min(groupCenterDistance, candidate.ScreenCenterDistance);
            groupSqrDistance = Mathf.Min(groupSqrDistance, candidate.SqrDistance);
        }

        if (_pointCandidates.Count == 0)
        {
            return;
        }

        // TargetGroup은 내부 List를 소유하므로, 다음 Root 평가 때 _pointCandidates를 비워도 안전하다.
        _targetGroups.Add(new TargetGroup(root.transform, groupCenterDistance, groupSqrDistance, new List<TargetCandidate>(_pointCandidates)));
    }

    private bool TryCreatePointCandidate(
        Transform owner,
        Camera camera,
        Transform root,
        LockOnTargetPoint point,
        out TargetCandidate candidate)
    {
        candidate = default;
        if (point == null || !point.isActiveAndEnabled || !point.gameObject.activeInHierarchy)
        {
            return false;
        }

        float sqrDistance = (point.transform.position - owner.position).sqrMagnitude;
        if (sqrDistance > searchRadius * searchRadius)
        {
            return false;
        }

        Vector3 viewportPoint = camera.WorldToViewportPoint(point.transform.position);
        if (viewportPoint.z <= 0f ||
            viewportPoint.x < 0f || viewportPoint.x > 1f ||
            viewportPoint.y < 0f || viewportPoint.y > 1f)
        {
            // "시야에 있는 대상"만 잡기 위해 카메라 프러스텀 밖 후보는 제외한다.
            return false;
        }

        float centerDistance = GetScreenCenterDistance(viewportPoint);
        candidate = new TargetCandidate(point.transform, root, centerDistance, sqrDistance, point.Priority);
        return true;
    }

    private bool IsRootSelectable(Transform owner, LockOnTargetRoot root)
    {
        if (owner == null || root == null || !root.IsTargetable || !root.gameObject.activeInHierarchy)
        {
            return false;
        }

        if (root.transform == owner.root)
        {
            // 자기 자신이 락온 후보에 들어가지 않게 막는다.
            return false;
        }

        PlayerStats player = root.GetComponentInParent<PlayerStats>();
        if (player != null && !player.IsDead)
        {
            // 플레이어는 죽은 상태에서만 부활/지원 타겟으로 락온할 수 있다.
            return false;
        }

        return true;
    }

    private int FindCurrentTargetIndex(List<TargetCandidate> targets, Transform currentTarget)
    {
        for (int i = 0; i < targets.Count; i++)
        {
            if (targets[i].Point == currentTarget)
            {
                return i;
            }
        }

        LockOnTargetRoot currentRoot = currentTarget.GetComponentInParent<LockOnTargetRoot>();
        if (currentRoot == null)
        {
            return -1;
        }

        for (int i = 0; i < targets.Count; i++)
        {
            if (targets[i].Root == currentRoot.transform)
            {
                return i;
            }
        }

        return -1;
    }

    private int CompareTargetGroup(TargetGroup left, TargetGroup right)
    {
        // 대상 Root 순서는 화면 중심에 가장 가까운 Point를 가진 대상이 우선이다.
        int centerCompare = left.ScreenCenterDistance.CompareTo(right.ScreenCenterDistance);
        if (centerCompare != 0)
        {
            return centerCompare;
        }

        int distanceCompare = left.SqrDistance.CompareTo(right.SqrDistance);
        if (distanceCompare != 0)
        {
            return distanceCompare;
        }

        return string.CompareOrdinal(left.Root.name, right.Root.name);
    }

    private int ComparePointInsideSameTarget(TargetCandidate left, TargetCandidate right)
    {
        // 같은 대상 안에서는 priority가 낮은 Point부터 순회한다.
        int priorityCompare = left.PointPriority.CompareTo(right.PointPriority);
        if (priorityCompare != 0)
        {
            return priorityCompare;
        }

        int centerCompare = left.ScreenCenterDistance.CompareTo(right.ScreenCenterDistance);
        if (centerCompare != 0)
        {
            return centerCompare;
        }

        return left.SqrDistance.CompareTo(right.SqrDistance);
    }

    private static float GetScreenCenterDistance(Vector3 viewportPoint)
    {
        float x = viewportPoint.x - 0.5f;
        float y = viewportPoint.y - 0.5f;
        return x * x + y * y;
    }

    private sealed class TargetGroup
    {
        public TargetGroup(Transform root, float screenCenterDistance, float sqrDistance, List<TargetCandidate> points)
        {
            Root = root;
            ScreenCenterDistance = screenCenterDistance;
            SqrDistance = sqrDistance;
            Points = points;
        }

        public Transform Root { get; }
        public float ScreenCenterDistance { get; }
        public float SqrDistance { get; }
        public List<TargetCandidate> Points { get; }
    }

    private readonly struct TargetCandidate
    {
        public TargetCandidate(Transform point, Transform root, float screenCenterDistance, float sqrDistance, int pointPriority)
        {
            Point = point;
            Root = root;
            ScreenCenterDistance = screenCenterDistance;
            SqrDistance = sqrDistance;
            PointPriority = pointPriority;
        }

        public Transform Point { get; }
        public Transform Root { get; }
        public float ScreenCenterDistance { get; }
        public float SqrDistance { get; }
        public int PointPriority { get; }
    }
}
