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

        for (int i = 0; i < weaponNodes.Length; i++)
        {
            Vector3 currentPos = weaponNodes[i].position;
            Vector3 previousPos = _previousPositions[i];
            
            // SphereCast 대신 OverlapCapsule 사용!
            // 이전 위치(previousPos)와 현재 위치(currentPos)를 양 끝점으로 하는 두께(hitRadius)의 알약 형태 공간을 싹 긁어옵니다.
            // 거리가 가깝든 멀든, 안에서 출발하든 밖에서 출발하든 100% 잡아냅니다.
            Collider[] hits = Physics.OverlapCapsule(previousPos, currentPos, hitRadius, targetLayer);

            foreach (var hitCol in hits)
            {
                PlayerHitbox playerHitbox = hitCol.GetComponentInParent<PlayerHitbox>();
                if (playerHitbox == null || !playerHitbox.Matches(hitCol)) continue;

                // 이미 이번 휘두르기에 맞은 녀석이면 무시 (더블 히트 방지!)
                if (_alreadyHitTargets.Contains(hitCol)) continue;

                PlayerStats playerStats = hitCol.GetComponentInParent<PlayerStats>();
                if (playerStats != null && !playerStats.IsDead && !_alreadyHitPlayers.Contains(playerStats))
                {
                    // 보스 버프 배율 적용
                    float finalDamage = baseDamage * _bossScript.GetOutgoingDamageMultiplier();

                    playerStats.TakeDamage(finalDamage);
                    Debug.Log($"[Melee Attack] 무기 궤적 타격 성공! 데미지: {finalDamage}");

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
