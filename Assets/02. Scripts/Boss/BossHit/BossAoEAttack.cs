using UnityEngine;
using System.Collections.Generic;

public class BossAoEAttack : MonoBehaviour
{
    public enum AoEShape { Sphere, Box }

    [Header("AOE 판정 형태")]
    public AoEShape hitShape = AoEShape.Sphere;

    [Tooltip("플레이어 레이어만 선택 (지형 무시)")]
    public LayerMask targetLayer;

    [Header("형태별 크기 설정")]
    public float hitRadius = 5f;
    public Vector3 boxSize = new Vector3(5f, 5f, 5f);

    [Tooltip("판정 중심점 오프셋 (예: 브레스가 입술 앞쪽으로 뻗어나가게 할 때 Z축 조절)")]
    public Vector3 hitOffset = Vector3.zero;

    [Header("데미지 설정")]
    public float baseDamage = 20f;
    public float delayBeforeActive = 0.2f;
    public float activeDuration = 1.5f;
    public bool isMultiHit = false;
    public float hitInterval = 0.5f;

    private float _timer = 0f;
    private bool _isActive = false;
    private float _bossDamageMultiplier = 1.0f;
    private Dictionary<Collider, float> _hitTargets = new Dictionary<Collider, float>();

    public void Initialize(float bossOutgoingMultiplier)
    {
        _bossDamageMultiplier = bossOutgoingMultiplier;
    }

    private void Update()
    {
        _timer += Time.deltaTime;

        if (!_isActive && _timer >= delayBeforeActive && _timer < delayBeforeActive + activeDuration)
        {
            _isActive = true;
        }

        if (_isActive && _timer >= delayBeforeActive + activeDuration)
        {
            _isActive = false;
            this.enabled = false;
            return;
        }

        if (_isActive)
        {
            DetectAndDamage();
        }
    }

    private void DetectAndDamage()
    {
        Collider[] hits = new Collider[0];

        // 🔥 [수정됨] hitOffset을 적용한 최종 중심 좌표
        Vector3 centerPosition = transform.position + (transform.rotation * hitOffset);

        if (hitShape == AoEShape.Sphere)
        {
            // 🔥 [수정됨] transform.position 대신 centerPosition 사용
            hits = Physics.OverlapSphere(centerPosition, hitRadius, targetLayer);
        }
        else if (hitShape == AoEShape.Box)
        {
            // 🔥 [수정됨] transform.position 대신 centerPosition 사용
            hits = Physics.OverlapBox(centerPosition, boxSize / 2f, transform.rotation, targetLayer);
        }

        foreach (var hit in hits)
        {
            if (_hitTargets.TryGetValue(hit, out float lastHitTime))
            {
                if (!isMultiHit) continue;
                if (Time.time - lastHitTime < hitInterval) continue;
            }

            if (hit.CompareTag("Player"))
            {
                PlayerStats playerStats = hit.GetComponent<PlayerStats>();
                if (playerStats != null)
                {
                    float finalDamage = baseDamage * _bossDamageMultiplier;
                    playerStats.TakeDamage(finalDamage);
                    Debug.Log($"[AoE Hit] 광역 이펙트 적중! 최종 딜: {finalDamage}");
                }
                _hitTargets[hit] = Time.time;
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.4f);

        if (hitShape == AoEShape.Sphere)
        {
            // 🔥 [수정됨] Vector3.zero 대신 hitOffset을 기준으로 그립니다.
            Gizmos.DrawSphere(hitOffset, hitRadius); 
        }
        else if (hitShape == AoEShape.Box)
        {
            // 🔥 [수정됨] Vector3.zero 대신 hitOffset을 기준으로 그립니다.
            Gizmos.DrawCube(hitOffset, boxSize);
        }
    }
}