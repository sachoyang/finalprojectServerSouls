using Fusion;
using UnityEngine;
using System.Collections.Generic;

// [역할] 보스의 무기(발톱, 꼬리 등)에 붙어, 휘두르는 궤적을 추적해 플레이어를 타격합니다.
public class BossMeleeAttack : MonoBehaviour
{
    [Header("데미지 설정")]
    public float baseDamage = 20f;
    public float knockbackPower = 10f;
    
    [Tooltip("플레이어의 레이어만 선택하세요! (환경 지형 무시용)")]
    public LayerMask targetLayer; 
    
    [Tooltip("궤적을 검사할 구체의 두께 (발톱 크기에 맞게 조절)")]
    public float hitRadius = 1f;

    [Header("궤적 추적 노드")]
    [Tooltip("발톱의 뿌리부터 끝부분까지 배치한 빈 오브젝트들을 넣어주세요.")]
    public Transform[] weaponNodes;

    private bool _isAttacking = false;
    private Vector3[] _previousPositions;

    // OverlapCapsuleNonAlloc용 고정 크기 버퍼 (매 틱 새 배열 할당으로 인한 GC 스파이크 방지)
    // 플레이어 3명 + 자식 콜라이더 여러 개를 감안해도 16이면 충분하다.
    private const int MaxHitsPerSweep = 16;
    private readonly Collider[] _hitBuffer = new Collider[MaxHitsPerSweep];
    private readonly float[] _hitDistances = new float[MaxHitsPerSweep];
    
    // 한 번 휘두를 때(다단히트 방지) 이미 맞은 대상을 기억하는 장부
    private HashSet<Collider> _alreadyHitTargets = new HashSet<Collider>();
    // 자식 콜라이더가 여러 개 맞아도 같은 플레이어는 한 번만 피해를 받게 막는다.
    private HashSet<PlayerStats> _alreadyHitPlayers = new HashSet<PlayerStats>();

    // 데미지 배율 등을 가져오기 위한 코어
    private NetworkBossCore _bossScript;

    private void Awake()
    {
        _bossScript = GetComponentInParent<NetworkBossCore>();
    }

    // 애니메이션 이벤트(StartAttack)가 호출될 때 실행
    public void StartAttack()
    {
        if (weaponNodes == null || weaponNodes.Length == 0) return;

        _isAttacking = true;
        _alreadyHitTargets.Clear(); // 때린 대상 초기화
        _alreadyHitPlayers.Clear(); // 플레이어 단위 중복 타격 초기화

        // 배열 크기 초기화 및 첫 프레임의 노드 위치 저장
        if (_previousPositions == null || _previousPositions.Length != weaponNodes.Length)
        {
            _previousPositions = new Vector3[weaponNodes.Length];
        }

        for (int i = 0; i < weaponNodes.Length; i++)
        {
            _previousPositions[i] = weaponNodes[i].position;
        }
    }

    // 애니메이션 이벤트(StopAttack)가 호출될 때 실행
    public void StopAttack()
    {
        _isAttacking = false;
    }

    // 매 프레임마다 이전 위치와 현재 위치 사이를 캡슐 형태로 훑습니다 (가장 완벽한 Sweep 검사)
    private void FixedUpdate()
    {
        // 서버(방장)가 아니거나, 공격 중이 아니면 무시
        if (!_isAttacking || _bossScript == null || !_bossScript.HasStateAuthority) return;

        // 🔥 [버그 픽스] 패턴 도중 그로기/사망으로 끊기면 StopAttack 애니메이션 이벤트가 씹혀서
        //    _isAttacking이 true로 남는다. 그 상태로 그로기 중 무기 판정이 살아있으면 안 되므로,
        //    보스가 '패턴 실행 중'일 때만 판정을 돌린다.
        if (_bossScript.CurrentState != BossState.ExecutingPattern)
        {
            _isAttacking = false;
            return;
        }

        for (int i = 0; i < weaponNodes.Length; i++)
        {
            Vector3 currentPos = weaponNodes[i].position;
            Vector3 previousPos = _previousPositions[i];
            
            // SphereCast 대신 OverlapCapsule 사용!
            // 이전 위치(previousPos)와 현재 위치(currentPos)를 양 끝점으로 하는 두께(hitRadius)의 알약 형태 공간을 싹 긁어옵니다.
            // 거리가 가깝든 멀든, 안에서 출발하든 밖에서 출발하든 100% 잡아냅니다.
            // NonAlloc 버전 + 고정 버퍼로 매 틱 배열 할당(GC)을 제거.
            int hitCount = Physics.OverlapCapsuleNonAlloc(previousPos, currentPos, hitRadius, _hitBuffer, targetLayer);
            SortBySurfaceDistance(_hitBuffer, _hitDistances, hitCount, currentPos);

            for (int h = 0; h < hitCount; h++)
            {
                Collider hitCol = _hitBuffer[h];
                PlayerHitbox playerHitbox = hitCol.GetComponentInParent<PlayerHitbox>();
                if (playerHitbox == null || !playerHitbox.Matches(hitCol)) continue;

                // 이미 이번 휘두르기에 맞은 녀석이면 무시 (더블 히트 방지!)
                if (_alreadyHitTargets.Contains(hitCol)) continue;

                PlayerStats playerStats = hitCol.GetComponentInParent<PlayerStats>();
                if (playerStats != null && !playerStats.IsDead && !_alreadyHitPlayers.Contains(playerStats))
                {
                    // 보스 버프 배율 적용
                    float finalDamage = baseDamage * _bossScript.GetOutgoingDamageMultiplier();

                    GetHitSurface(
                        hitCol,
                        currentPos,
                        out Vector3 hitPoint,
                        out Vector3 hitNormal);
                    playerStats.TakeDamage(finalDamage, hitPoint, hitNormal);
                    BossLog.Info($"[Melee Attack] 무기 궤적 타격 성공! 데미지: {finalDamage}");

                    // 타격음 재생 (원한다면 여기에 사운드 코드 추가)

                    // 명단에 등록해서 한 번 더 맞는 것을 방지
                    _alreadyHitTargets.Add(hitCol);
                    _alreadyHitPlayers.Add(playerStats);
                }
            }

            // 다음 프레임을 위해 현재 위치를 이전 위치로 갱신
            _previousPositions[i] = currentPos;
        }
    }

    private static void GetHitSurface(
        Collider hitCollider,
        Vector3 sourcePosition,
        out Vector3 hitPoint,
        out Vector3 hitNormal)
    {
        Vector3 toCenter = hitCollider.bounds.center - sourcePosition;
        float distance = toCenter.magnitude;
        if (distance > 0.0001f &&
            hitCollider.Raycast(
                new Ray(sourcePosition, toCenter / distance),
                out RaycastHit hit,
                distance + hitCollider.bounds.extents.magnitude))
        {
            hitPoint = hit.point;
            hitNormal = hit.normal;
            return;
        }

        hitPoint = hitCollider.ClosestPoint(sourcePosition);
        hitNormal = (hitPoint - hitCollider.bounds.center).normalized;
    }

    private static void SortBySurfaceDistance(
        Collider[] colliders,
        float[] distances,
        int count,
        Vector3 sourcePosition)
    {
        // ClosestPoint(물리 연산)는 콜라이더당 딱 한 번만 계산해서 캐싱.
        // (기존에는 삽입정렬 내부 루프에서 반복 호출되어 O(n²)번 물리 연산이 돌았음)
        for (int i = 0; i < count; i++)
        {
            distances[i] =
                (colliders[i].ClosestPoint(sourcePosition) - sourcePosition).sqrMagnitude;
        }

        // 캐싱된 거리 기준 삽입 정렬 (count가 작아 충분히 빠름)
        for (int i = 1; i < count; i++)
        {
            Collider value = colliders[i];
            float valueDistance = distances[i];
            int j = i - 1;
            while (j >= 0 && distances[j] > valueDistance)
            {
                colliders[j + 1] = colliders[j];
                distances[j + 1] = distances[j];
                j--;
            }

            colliders[j + 1] = value;
            distances[j + 1] = valueDistance;
        }
    }

    // 에디터에서 궤적의 굵기(hitRadius)를 직관적으로 볼 수 있게 그려줍니다.
    private void OnDrawGizmosSelected()
    {
        if (weaponNodes == null) return;
        Gizmos.color = new Color(1f, 0f, 0f, 0.5f);
        foreach (var node in weaponNodes)
        {
            if (node != null) Gizmos.DrawWireSphere(node.position, hitRadius);
        }
    }
}
