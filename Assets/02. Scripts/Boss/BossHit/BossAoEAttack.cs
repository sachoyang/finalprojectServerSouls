using UnityEngine;
using System.Collections.Generic;

public class BossAoEAttack : MonoBehaviour
{
    // 🔥 [추가됨] Inspector에서 도형을 고를 수 있게 해주는 열거형
    public enum AoEShape { Sphere, Box }

    [Header("AOE 판정 형태")]
    public AoEShape hitShape = AoEShape.Sphere;

    [Tooltip("플레이어 레이어만 선택 (지형 무시)")]
    public LayerMask targetLayer;

    [Header("형태별 크기 설정")]
    [Tooltip("Sphere를 선택했을 때의 반지름")]
    public float hitRadius = 5f;
    [Tooltip("Box를 선택했을 때의 가로, 높이, 깊이 크기 (예: 브레스 Z축 20)")]
    public Vector3 boxSize = new Vector3(5f, 5f, 5f);

    [Header("데미지 설정")]
    public float baseDamage = 20f;
    // (이하 타이밍 설정 및 다단히트 변수들은 기존과 100% 동일하므로 생략 없이 유지하세요!)
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

        // 🔥 [추가됨] 기획자가 선택한 도형에 맞춰 수학적 검사를 다르게 수행!
        if (hitShape == AoEShape.Sphere)
        {
            hits = Physics.OverlapSphere(transform.position, hitRadius, targetLayer);
        }
        else if (hitShape == AoEShape.Box)
        {
            // OverlapBox는 Extents(절반 크기)를 요구하므로 2로 나눠서 넣습니다.
            // transform.rotation을 넣어주면 보스가 대각선으로 브레스를 쏴도 박스가 기울어진 채로 검사됩니다!
            hits = Physics.OverlapBox(transform.position, boxSize / 2f, transform.rotation, targetLayer);
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

    // 🔥 [추가됨] 에디터(씬 뷰)에서 둥근 모양인지, 네모난 모양인지 직관적으로 보여줍니다.
    private void OnDrawGizmosSelected()
    {
        // 박스가 회전된 각도까지 씬 뷰에서 정확히 그려주기 위한 마법의 코드
        Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.4f);

        if (hitShape == AoEShape.Sphere)
        {
            Gizmos.DrawSphere(Vector3.zero, hitRadius); // 위치를 0,0,0으로 주는 이유는 matrix가 이미 보정했기 때문
        }
        else if (hitShape == AoEShape.Box)
        {
            Gizmos.DrawCube(Vector3.zero, boxSize);
        }
    }
}