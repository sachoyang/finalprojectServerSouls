using System.Collections.Generic;
using UnityEngine;

public class LockOnTargetSelector : MonoBehaviour
{
    [Header("Search")]
    [SerializeField] private float searchRadius = 80f;
    [SerializeField] private string headTag = "LockOnHead";
    [SerializeField] private string bodyTag = "LockOnBody";
    [SerializeField] private string deadPlayerTag = "DeadPlayer";

    private readonly List<TargetCandidate> _candidates = new List<TargetCandidate>();
    private readonly Dictionary<Transform, TargetCandidate> _bestCandidateByRoot = new Dictionary<Transform, TargetCandidate>();

    private int _currentIndex = -1;

    public void SetSearchRadius(float radius)
    {
        searchRadius = Mathf.Max(0f, radius);
    }

    public Transform SelectNextTarget(Transform owner, Transform currentTarget, Camera viewCamera)
    {
        // Q 최초 입력은 화면 중심과 가장 가까운 대상을 잡고, 이후 Q는 같은 정렬 목록에서 다음 대상으로 순회한다.
        List<TargetCandidate> targets = GetVisibleTargetsSortedByScreenCenter(owner, viewCamera);
        if (targets.Count == 0)
        {
            Clear();
            return null;
        }

        if (currentTarget != null)
        {
            int currentIndex = FindCurrentTargetIndex(targets, currentTarget);
            if (currentIndex >= 0)
            {
                _currentIndex = currentIndex;
            }
        }

        _currentIndex = (_currentIndex + 1) % targets.Count;
        return targets[_currentIndex].Point;
    }

    public void Clear()
    {
        _currentIndex = -1;
    }

    private List<TargetCandidate> GetVisibleTargetsSortedByScreenCenter(Transform owner, Camera viewCamera)
    {
        _candidates.Clear();
        _bestCandidateByRoot.Clear();

        if (owner == null)
        {
            return _candidates;
        }

        Camera camera = viewCamera != null ? viewCamera : Camera.main;
        if (camera == null)
        {
            return _candidates;
        }

        // 1순위: 전투 공통 컴포넌트 기반 후보. 앞으로는 보스/몹/플레이어 모두 이 방식으로 통일한다.
        AddComponentLockOnPoints(owner, camera);
        // 2순위: 기존 프리팹 호환용 태그 기반 후보. 아직 LockOnTargetPoint가 없는 프리팹을 살려두는 fallback이다.
        AddTaggedLockOnPoints(owner, camera, headTag);
        AddTaggedLockOnPoints(owner, camera, bodyTag);
        // 죽은 플레이어는 내부 포인트가 없을 수도 있으므로 DeadPlayer 루트 자체도 후보로 추가한다.
        AddDeadPlayerRoots(owner, camera);

        _candidates.AddRange(_bestCandidateByRoot.Values);
        _candidates.Sort(CompareTargetCandidate);
        return _candidates;
    }

    private void AddComponentLockOnPoints(Transform owner, Camera camera)
    {
        LockOnTargetPoint[] points = FindObjectsOfType<LockOnTargetPoint>(true);
        foreach (LockOnTargetPoint point in points)
        {
            if (point == null || !point.isActiveAndEnabled || !point.gameObject.activeInHierarchy)
            {
                continue;
            }

            TryAddCandidate(owner, camera, point.transform, point.Priority);
        }
    }

    private void AddTaggedLockOnPoints(Transform owner, Camera camera, string targetTag)
    {
        if (string.IsNullOrWhiteSpace(targetTag))
        {
            return;
        }

        GameObject[] objects;
        try
        {
            objects = GameObject.FindGameObjectsWithTag(targetTag);
        }
        catch (UnityException)
        {
            // 태그가 프로젝트에 없으면 에디터/런타임에서 예외가 나므로, 선택 후보만 조용히 건너뛴다.
            return;
        }

        foreach (GameObject targetObject in objects)
        {
            if (targetObject == null || !targetObject.activeInHierarchy)
            {
                continue;
            }

            TryAddCandidate(owner, camera, targetObject.transform, GetTagPriority(targetObject.transform));
        }
    }

    private void AddDeadPlayerRoots(Transform owner, Camera camera)
    {
        if (string.IsNullOrWhiteSpace(deadPlayerTag))
        {
            return;
        }

        GameObject[] deadPlayers;
        try
        {
            deadPlayers = GameObject.FindGameObjectsWithTag(deadPlayerTag);
        }
        catch (UnityException)
        {
            // DeadPlayer 태그가 아직 등록되지 않은 프로젝트 상태에서도 락온 시스템이 죽지 않게 보호한다.
            return;
        }

        foreach (GameObject deadPlayer in deadPlayers)
        {
            if (deadPlayer == null || !deadPlayer.activeInHierarchy)
            {
                continue;
            }

            Transform lockOnPoint = FindPreferredPointInside(deadPlayer.transform);
            TryAddCandidate(owner, camera, lockOnPoint != null ? lockOnPoint : deadPlayer.transform, GetPointPriority(lockOnPoint));
        }
    }

    private void TryAddCandidate(Transform owner, Camera camera, Transform point, int pointPriority)
    {
        if (point == null)
        {
            return;
        }

        Transform root = GetTargetRoot(point);
        if (root == null || root == owner.root || !IsRootTargetable(root))
        {
            // 자기 자신이 DeadPlayer/Player 태그 상태가 되더라도 락온 후보에 들어가지 않게 막는다.
            return;
        }

        float sqrDistance = (point.position - owner.position).sqrMagnitude;
        if (sqrDistance > searchRadius * searchRadius)
        {
            return;
        }

        Vector3 viewportPoint = camera.WorldToViewportPoint(point.position);
        if (viewportPoint.z <= 0f ||
            viewportPoint.x < 0f || viewportPoint.x > 1f ||
            viewportPoint.y < 0f || viewportPoint.y > 1f)
        {
            // "시야에 있는 대상"만 잡기 위해 카메라 프러스텀 밖 후보는 제외한다.
            return;
        }

        float centerDistance = GetScreenCenterDistance(viewportPoint);
        var candidate = new TargetCandidate(point, root, centerDistance, sqrDistance, pointPriority);

        if (!_bestCandidateByRoot.TryGetValue(root, out TargetCandidate previous) ||
            ComparePointInsideSameTarget(candidate, previous) < 0)
        {
            // 한 몬스터/플레이어 안에 Head/Body가 여러 개 있어도 대표 포인트 하나만 남긴다.
            _bestCandidateByRoot[root] = candidate;
        }
    }

    private Transform FindPreferredPointInside(Transform root)
    {
        LockOnTargetPoint componentPoint = FindComponentPointInside(root);
        if (componentPoint != null)
        {
            return componentPoint.transform;
        }

        Transform head = FindChildWithTag(root, headTag);
        if (head != null)
        {
            return head;
        }

        return FindChildWithTag(root, bodyTag);
    }

    private LockOnTargetPoint FindComponentPointInside(Transform root)
    {
        if (root == null)
        {
            return null;
        }

        LockOnTargetPoint bestPoint = null;
        int bestPriority = int.MaxValue;
        foreach (LockOnTargetPoint point in root.GetComponentsInChildren<LockOnTargetPoint>(true))
        {
            if (point == null || point.Priority >= bestPriority)
            {
                continue;
            }

            bestPoint = point;
            bestPriority = point.Priority;
        }

        return bestPoint;
    }

    private Transform FindChildWithTag(Transform root, string targetTag)
    {
        if (root == null || string.IsNullOrWhiteSpace(targetTag))
        {
            return null;
        }

        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child != null && child.CompareTag(targetTag))
            {
                return child;
            }
        }

        return null;
    }

    private Transform GetTargetRoot(Transform point)
    {
        LockOnTargetRoot targetRoot = point.GetComponentInParent<LockOnTargetRoot>();
        if (targetRoot != null)
        {
            return targetRoot.transform;
        }

        // 아래 두 타입은 기존 프리팹을 바로 깨지 않기 위한 임시 fallback이다.
        // 보스/죽은 플레이어 프리팹에 LockOnTargetRoot를 붙이면 전투 시스템의 구체 타입 의존을 줄일 수 있다.
        NetworkBossCore boss = point.GetComponentInParent<NetworkBossCore>();
        if (boss != null)
        {
            return boss.transform;
        }

        PlayerStats player = point.GetComponentInParent<PlayerStats>();
        if (player != null)
        {
            return player.transform;
        }

        return point.root;
    }

    private bool IsRootTargetable(Transform root)
    {
        LockOnTargetRoot targetRoot = root != null ? root.GetComponent<LockOnTargetRoot>() : null;
        return targetRoot == null || targetRoot.IsTargetable;
    }

    private int FindCurrentTargetIndex(List<TargetCandidate> targets, Transform currentTarget)
    {
        Transform currentRoot = GetTargetRoot(currentTarget);
        for (int i = 0; i < targets.Count; i++)
        {
            if (targets[i].Root == currentRoot)
            {
                return i;
            }
        }

        return -1;
    }

    private int CompareTargetCandidate(TargetCandidate left, TargetCandidate right)
    {
        // 대상 순서는 화면 중심에 가까운 순서가 최우선이고, 거리가 같으면 실제 거리가 가까운 대상을 먼저 둔다.
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
        // 같은 대상 안에서는 컴포넌트/태그 우선순위가 높은 포인트를 고른다.
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

    private int GetPointPriority(Transform point)
    {
        if (point == null)
        {
            return 20;
        }

        LockOnTargetPoint lockOnPoint = point.GetComponent<LockOnTargetPoint>();
        if (lockOnPoint != null)
        {
            return lockOnPoint.Priority;
        }

        return GetTagPriority(point);
    }

    private int GetTagPriority(Transform point)
    {
        if (point.CompareTag(headTag))
        {
            return 0;
        }

        if (point.CompareTag(bodyTag))
        {
            return 10;
        }

        return 20;
    }

    private static float GetScreenCenterDistance(Vector3 viewportPoint)
    {
        float x = viewportPoint.x - 0.5f;
        float y = viewportPoint.y - 0.5f;
        return x * x + y * y;
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
