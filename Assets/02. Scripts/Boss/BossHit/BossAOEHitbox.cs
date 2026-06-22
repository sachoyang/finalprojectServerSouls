using UnityEngine;
using System.Collections.Generic;

public class BossAOEHitbox : MonoBehaviour
{
    [Header("AOE(장판) 설정")]
    public float damage = 20f;
    [Tooltip("데미지가 들어가는 구체의 반지름")]
    public float radius = 5f;
    [Tooltip("이펙트 발생 후 데미지 판정이 지속되는 시간 (초)")]
    public float activeDuration = 1.5f; 
    
    [Tooltip("플레이어 레이어만 선택하세요 (쓸데없는 맵 지형 긁기 방지)")]
    public LayerMask targetLayer; 

    [Header("다단히트(틱) 설정")]
    [Tooltip("체크하면 독장판처럼 지속 데미지를 주고, 끄면 쾅! 하고 한 번만 데미지를 줍니다.")]
    public bool isMultiHit = false;
    [Tooltip("다단히트일 경우 데미지가 들어가는 간격")]
    public float hitInterval = 0.5f; 

    private float _timer = 0f;
    
    // 한 번 맞은 대상의 콜라이더와 마지막 피격 시간을 기록해서 중복/과다 데미지 방지
    private Dictionary<Collider, float> _hitTargets = new Dictionary<Collider, float>();

    private void Update()
    {
        _timer += Time.deltaTime;
        
        // 지정된 판정 시간이 지나면 데미지 판정 로직만 끕니다. 
        // (this.enabled = false를 쓰면 파티클 이펙트 자체는 안 꺼지고 자연스럽게 사라질 때까지 유지됨)
        if (_timer > activeDuration)
        {
            this.enabled = false;
            return;
        }

        DetectAndDamage();
    }

    private void DetectAndDamage()
    {
        // 지정한 반경 내에 있는 targetLayer(플레이어)를 모두 긁어옴
        Collider[] hits = Physics.OverlapSphere(transform.position, radius, targetLayer);

        foreach (var hit in hits)
        {
            // 이미 때린 대상인지 확인
            if (_hitTargets.TryGetValue(hit, out float lastHitTime))
            {
                // 단발성이면 한 번 맞았으니 이후 무시
                if (!isMultiHit) continue; 
                
                // 다단히트라도 틱(간격) 쿨타임이 안 지났으면 무시
                if (Time.time - lastHitTime < hitInterval) continue; 
            }

            if (hit.CompareTag("Player"))
            {
                // 플레이어 스크립트 연동 (기존 BossHitbox에서 쓰던 방식 그대로)
                PlayerStats playerStats = hit.GetComponent<PlayerStats>();
                if (playerStats != null)
                {
                    playerStats.TakeDamage(damage);
                    Debug.Log($"[AOE] 광역 이펙트가 {hit.name}에게 {damage} 데미지 줌!");
                }
                
                // 맞은 시간 기록 (단발성이면 영구 밴, 다단히트면 쿨타임 갱신용)
                _hitTargets[hit] = Time.time;
            }
        }
    }

    // ==========================================
    // [에디터 시각화] 씬 뷰에서 데미지 영역(Gizmo) 그리기
    // ==========================================
    private void OnDrawGizmos()
    {
        // 영역 내부 반투명 빨간색 칠하기
        Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
        Gizmos.DrawSphere(transform.position, radius);

        // 경계선 선명한 빨간색 테두리 그리기
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}