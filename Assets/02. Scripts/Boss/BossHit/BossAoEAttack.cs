using UnityEngine;
using System.Collections.Generic;

public class BossAoEAttack : MonoBehaviour
{
    public enum AoEShape { Sphere, Box }

    [System.Serializable]
    public class AoEHitZone
    {
        [Header("형태 및 크기")]
        public AoEShape hitShape = AoEShape.Sphere;
        public float hitRadius = 5f;
        public Vector3 boxSize = new Vector3(5f, 5f, 5f);
        public Vector3 hitOffset = Vector3.zero;

        [Header("타이밍 (초 단위)")]
        public float startTime = 0f;
        public float duration = 0.5f;
    }

    [Header("연쇄 판정(Cascading) 구역 리스트")]
    public List<AoEHitZone> hitZones = new List<AoEHitZone>();

    // 🔥 [핵심 추가] 파티클 시스템을 끌어다 넣을 칸
    [Header("파티클 동기화 (선택사항)")]
    [Tooltip("파티클을 넣으면, 이 판정 스크립트는 파티클의 실제 재생 시간과 100% 동기화됩니다!")]
    public ParticleSystem syncParticle;

    [Header("공통 데미지 설정")]
    public float baseDamage = 20f;
    public bool isMultiHit = false;
    public float hitInterval = 0.5f;
    public LayerMask targetLayer;

    private float _timer = 0f;
    private float _bossDamageMultiplier = 1.0f;
    private float _maxEndTime = 0f; 
    
    // 자식 콜라이더가 여러 개 있어도 PlayerStats 기준으로 다단 히트 간격을 관리한다.
    private Dictionary<PlayerStats, float> _hitTargets = new Dictionary<PlayerStats, float>();

    // 판정 종료 표시. this.enabled를 끄면 풀 재사용 시 OnEnable이 안 와서 초기화가 누락되므로,
    // 컴포넌트는 켜둔 채 이 플래그로 멈춘다.
    private bool _finished;

    // 풀에서 재활성화될 때마다 호출되어 상태를 초기화한다. (컴포넌트를 끄지 않으므로 매 재활성화마다 옴)
    private void OnEnable()
    {
        _timer = 0f;
        _finished = false;
        _hitTargets.Clear();
    }

    public void Initialize(float bossOutgoingMultiplier)
    {
        _bossDamageMultiplier = bossOutgoingMultiplier;

        // [풀 재사용 대비] 상태 초기화 (OnEnable과 중복돼도 무해)
        _timer = 0f;
        _finished = false;
        _hitTargets.Clear();

        _maxEndTime = 0f;
        foreach (var zone in hitZones)
        {
            float endTime = zone.startTime + zone.duration;
            if (endTime > _maxEndTime) _maxEndTime = endTime;
        }
    }

    private void Update()
    {
        if (_finished) return;

        // 🔥 [동기화 로직] 파티클이 연결되어 있다면 파티클의 시계를, 없다면 아날로그 시계를 씁니다.
        if (syncParticle != null)
        {
            _timer = syncParticle.time; // 파티클의 현재 재생 시간을 그대로 가져옴!

            // 파티클 수명이 완전히 끝났다면 판정 정지
            if (!syncParticle.IsAlive(true))
            {
                _finished = true;
                return;
            }
        }
        else
        {
            _timer += Time.deltaTime; // 기존 방식

            if (_timer > _maxEndTime && _maxEndTime > 0f)
            {
                _finished = true;
                return;
            }
        }

        DetectAndDamage();
    }

    private void DetectAndDamage()
    {
        foreach (var zone in hitZones)
        {
            if (_timer < zone.startTime || _timer > zone.startTime + zone.duration) continue;

            Vector3 centerPosition = transform.position + (transform.rotation * zone.hitOffset);
            Collider[] hits = new Collider[0];

            if (zone.hitShape == AoEShape.Sphere)
            {
                hits = Physics.OverlapSphere(centerPosition, zone.hitRadius, targetLayer);
            }
            else if (zone.hitShape == AoEShape.Box)
            {
                hits = Physics.OverlapBox(centerPosition, zone.boxSize / 2f, transform.rotation, targetLayer);
            }

            SortBySurfaceDistance(hits, centerPosition);
            foreach (var hit in hits)
            {
                PlayerHitbox playerHitbox = hit.GetComponentInParent<PlayerHitbox>();
                if (playerHitbox == null || !playerHitbox.Matches(hit))
                {
                    continue;
                }

                PlayerStats playerStats = hit.GetComponentInParent<PlayerStats>();
                if (playerStats == null || playerStats.IsDead)
                {
                    continue;
                }

                if (_hitTargets.TryGetValue(playerStats, out float lastHitTime))
                {
                    if (!isMultiHit) continue;
                    if (Time.time - lastHitTime < hitInterval) continue;
                }

                float finalDamage = baseDamage * _bossDamageMultiplier;
                GetHitSurface(
                    hit,
                    centerPosition,
                    out Vector3 hitPoint,
                    out Vector3 hitNormal);
                playerStats.TakeDamage(finalDamage, hitPoint, hitNormal);
                Debug.Log($"[AoE Hit] 광역 이펙트 연쇄 적중! 파티클 동기화 딜: {finalDamage}");
                _hitTargets[playerStats] = Time.time;
            }
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
        Vector3 sourcePosition)
    {
        for (int i = 1; i < colliders.Length; i++)
        {
            Collider value = colliders[i];
            float valueDistance =
                (value.ClosestPoint(sourcePosition) - sourcePosition).sqrMagnitude;
            int j = i - 1;
            while (j >= 0)
            {
                float currentDistance =
                    (colliders[j].ClosestPoint(sourcePosition) - sourcePosition)
                    .sqrMagnitude;
                if (currentDistance <= valueDistance)
                {
                    break;
                }

                colliders[j + 1] = colliders[j];
                j--;
            }

            colliders[j + 1] = value;
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);

        foreach (var zone in hitZones)
        {
            bool isActiveNow = false;

            if (Application.isPlaying)
            {
                if (_timer >= zone.startTime && _timer <= zone.startTime + zone.duration)
                {
                    isActiveNow = true;
                }
            }
            else
            {
                isActiveNow = true;
            }

            if (isActiveNow)
            {
                Gizmos.color = new Color(1f, 0.2f, 0f, 0.6f); 
            }
            else
            {
                Gizmos.color = new Color(0.5f, 0.5f, 0.5f, 0.1f); 
            }

            if (zone.hitShape == AoEShape.Sphere)
            {
                Gizmos.DrawSphere(zone.hitOffset, zone.hitRadius);
                if (isActiveNow) { Gizmos.color = Color.red; Gizmos.DrawWireSphere(zone.hitOffset, zone.hitRadius); }
            }
            else if (zone.hitShape == AoEShape.Box)
            {
                Gizmos.DrawCube(zone.hitOffset, zone.boxSize);
                if (isActiveNow) { Gizmos.color = Color.red; Gizmos.DrawWireCube(zone.hitOffset, zone.boxSize); }
            }
        }
    }
}
