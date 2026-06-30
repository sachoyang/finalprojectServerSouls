using System.Collections.Generic;
using Fusion;
using UnityEngine;

/// <summary>
/// 게임 전투 판정과 대상별 피해 처리를 담당한다.
/// 입력, 애니메이션, 공격 타이밍은 각 주체가 소유하고,
/// 실제 히트 판정과 전투 결과 적용은 이 시스템에 위임한다.
/// </summary>
public class CombatSystem : MonoBehaviour
{
    [Header("기본 공격 판정")]
    [InspectorName("판정 반지름")]
    [Tooltip("기본 공격 판정 구체의 반지름입니다.")]
    [SerializeField] private float basicAttackHitRadius = 1.4f;
    [InspectorName("전방 거리")]
    [Tooltip("플레이어 기준으로 판정 중심이 앞쪽에 떨어진 거리입니다.")]
    [SerializeField] private float basicAttackHitDistance = 1.8f;
    [InspectorName("판정 높이")]
    [Tooltip("플레이어 발 위치에서 판정 중심까지의 높이입니다.")]
    [SerializeField] private float basicAttackHitHeight = 1.1f;
    [InspectorName("공격 대상 레이어")]
    [Tooltip("기본 공격으로 감지할 물리 레이어입니다.")]
    [SerializeField] private LayerMask basicAttackTargetLayers = ~0;

    [Header("부활 공격")]
    [InspectorName("기본 공격 부활 수치")]
    [Tooltip("죽은 아군을 기본 공격으로 맞혔을 때 누적할 부활 수치입니다.")]
    [SerializeField] private float basicAttackRevivePower = 34f;

    [Header("피격 연출")]
    [InspectorName("피 이펙트 관리자")]
    [Tooltip("표면 피격 위치에 피 이펙트와 타격음을 생성합니다. 비어 있으면 실행 중 자동으로 찾습니다.")]
    [SerializeField] private BloodEffectSpawner bloodEffectSpawner;

    private readonly Collider[] _hitBuffer = new Collider[128];
    private readonly Dictionary<NetworkBossCore, BossHitCandidate> _bestBossHurtboxes = new Dictionary<NetworkBossCore, BossHitCandidate>();
    private readonly HashSet<PlayerStats> _reviveHitPlayers = new HashSet<PlayerStats>();

    private readonly struct BossHitCandidate
    {
        public readonly BossHurtbox Hurtbox;
        public readonly Collider Collider;
        public readonly float SqrDistance;

        public BossHitCandidate(BossHurtbox hurtbox, Collider collider, float sqrDistance)
        {
            Hurtbox = hurtbox;
            Collider = collider;
            SqrDistance = sqrDistance;
        }
    }

    public float BasicAttackHitRadius => basicAttackHitRadius;
    public Vector3 BasicAttackHitLocalCenter => Vector3.up * basicAttackHitHeight + Vector3.forward * basicAttackHitDistance;
    public int LastAbilityRawHitCount { get; private set; }
    public int LastAbilityFilteredHitCount { get; private set; }
    public int LastAbilityBossHurtboxCount { get; private set; }

    private void Awake()
    {
        ResolveReferences();
    }

    // 기본 공격 한 번의 대상 탐색, 피해, 피격 연출을 처리한다.
    public void ProcessBasicAttackHit(
        NetworkObject attacker,
        PlayerStats attackerStats,
        Transform attackOrigin,
        float damage,
        string effectId,
        float groggyDamage = 10f)
    {
        if (attackOrigin == null || damage <= 0f)
        {
            return;
        }

        ResolveReferences();
        Vector3 hitCenter = attackOrigin.TransformPoint(BasicAttackHitLocalCenter);
        int hitCount = CollectBasicAttackHits(attackOrigin);
        ResolveHitTargets(attacker, attackerStats, damage, basicAttackRevivePower, hitCount, hitCenter);

        // 현재 콤보 타수의 효과 ID를 피격 연출까지 전달한다.
        ApplyBossHits(attackOrigin, hitCenter, attacker, damage, groggyDamage, effectId);
    }

    // 스킬의 시간 구간별 범위 판정과 피해를 처리한다.
    public bool ProcessAbilityHitEvent(
        NetworkObject attacker,
        PlayerStats attackerStats,
        Transform attackOrigin,
        AbilityHitEvent hitEvent,
        string abilityId,
        float fallbackDamage,
        float outgoingDamageMultiplier = 1f)
    {
        if (attackOrigin == null || hitEvent == null)
        {
            return false;
        }

        float damageMultiplier = hitEvent.DamageRate > 0f ? hitEvent.DamageRate : fallbackDamage;
        float attackPower = attackerStats != null ? attackerStats.AttackPower : 0f;
        float damage = attackPower * damageMultiplier * Mathf.Max(0f, outgoingDamageMultiplier);
        if (damage <= 0f)
        {
            return false;
        }

        ResolveReferences();
        LastAbilityRawHitCount = 0;
        LastAbilityFilteredHitCount = 0;
        LastAbilityBossHurtboxCount = 0;
        Vector3 hitCenter = GetAbilityHitCenter(attackOrigin, hitEvent);
        int hitCount = CollectCylinderHits(attackOrigin, hitEvent);
        ResolveHitTargets(attacker, attackerStats, damage, hitEvent.RevivePower, hitCount, hitCenter);
        LastAbilityFilteredHitCount = hitCount;
        LastAbilityBossHurtboxCount = _bestBossHurtboxes.Count;
        bool hitBoss = _bestBossHurtboxes.Count > 0;

        // PlayerAbilityModule의 AbilityId를 피격 연출까지 전달한다.
        ApplyBossHits(attackOrigin, hitCenter, attacker, damage, hitEvent.GroggyDamage, abilityId);
        return hitBoss;
    }

    private int CollectBasicAttackHits(Transform attackOrigin)
    {
        Vector3 hitCenter = attackOrigin.TransformPoint(BasicAttackHitLocalCenter);
        return Physics.OverlapSphereNonAlloc(
            hitCenter,
            basicAttackHitRadius,
            _hitBuffer,
            basicAttackTargetLayers,
            QueryTriggerInteraction.Collide);
    }

    private int CollectCylinderHits(Transform attackOrigin, AbilityHitEvent hitEvent)
    {
        float halfHeight = Mathf.Max(0.01f, hitEvent.Height) * 0.5f;
        Vector3 center = GetAbilityHitCenter(attackOrigin, hitEvent);
        Vector3 halfExtents = new Vector3(
            hitEvent.Radius,
            halfHeight,
            hitEvent.Radius);

        int rawCount = Physics.OverlapBoxNonAlloc(
            center,
            halfExtents,
            _hitBuffer,
            Quaternion.identity,
            basicAttackTargetLayers,
            QueryTriggerInteraction.Collide);
        LastAbilityRawHitCount = rawCount;

        int filteredCount = 0;
        float radiusSqr = hitEvent.Radius * hitEvent.Radius;
        for (int i = 0; i < rawCount; i++)
        {
            Collider hit = _hitBuffer[i];
            if (hit == null)
            {
                continue;
            }

            Vector3 closest = GetClosestPointSafe(hit, center);
            Vector3 delta = closest - center;
            if (Mathf.Abs(delta.y) > halfHeight)
            {
                continue;
            }

            Vector2 horizontal = new Vector2(delta.x, delta.z);
            if (horizontal.sqrMagnitude > radiusSqr)
            {
                continue;
            }

            _hitBuffer[filteredCount++] = hit;
        }

        return filteredCount;
    }

    private static Vector3 GetClosestPointSafe(Collider collider, Vector3 position)
    {
        if (collider is BoxCollider ||
            collider is SphereCollider ||
            collider is CapsuleCollider ||
            collider is MeshCollider meshCollider && meshCollider.convex)
        {
            return collider.ClosestPoint(position);
        }

        return collider.bounds.ClosestPoint(position);
    }

    private static Vector3 GetAbilityHitCenter(Transform attackOrigin, AbilityHitEvent hitEvent)
    {
        float halfHeight = Mathf.Max(0.01f, hitEvent.Height) * 0.5f;
        return attackOrigin.position + Vector3.up * (hitEvent.CenterHeight + halfHeight);
    }

    private void ResolveHitTargets(
        NetworkObject attacker,
        PlayerStats attackerStats,
        float damage,
        float revivePower,
        int hitCount,
        Vector3 hitCenter)
    {
        _bestBossHurtboxes.Clear();
        _reviveHitPlayers.Clear();

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = _hitBuffer[i];
            if (hit == null)
            {
                continue;
            }

            if (TryApplyReviveHit(hit, attacker, attackerStats, revivePower))
            {
                continue;
            }

            if (TryApplyAltarHit(hit, damage))
            {
                continue;
            }

            TryCollectBestBossHurtbox(hit, hitCenter);
        }
    }

    private void ApplyBossHits(
        Transform attackOrigin,
        Vector3 hitCenter,
        NetworkObject attacker,
        float damage,
        float groggyDamage,
        string effectId)
    {
        foreach (BossHitCandidate candidate in _bestBossHurtboxes.Values)
        {
            BossHurtbox bossHurtbox = candidate.Hurtbox;
            bossHurtbox.OnHitByPlayer(damage, groggyDamage, attacker);

            // 범위 판정에서 실제로 검출된 가장 가까운 콜라이더 표면에 피 연출을 생성한다.
            SpawnBloodOnHitExtended(attackOrigin, hitCenter, candidate.Collider, bossHurtbox, effectId);
        }
    }

    private bool TryApplyReviveHit(Collider hit, NetworkObject attacker, PlayerStats attackerStats, float revivePower)
    {
        PlayerStats hitPlayerStats = hit.GetComponentInParent<PlayerStats>();
        if (hitPlayerStats == null || hitPlayerStats == attackerStats || !hitPlayerStats.IsDead)
        {
            return false;
        }

        if (_reviveHitPlayers.Add(hitPlayerStats))
        {
            hitPlayerStats.RegisterReviveHit(attacker, revivePower);
        }

        return true;
    }

    private static bool TryApplyAltarHit(Collider hit, float damage)
    {
        GimmickAltar altar = hit.GetComponentInParent<GimmickAltar>();
        if (altar == null)
        {
            return false;
        }

        altar.RPC_TakeDamage(damage);
        return true;
    }

    private void TryCollectBestBossHurtbox(Collider hit, Vector3 hitCenter)
    {
        BossHurtbox bossHurtbox = hit.GetComponentInParent<BossHurtbox>();
        if (bossHurtbox == null)
        {
            return;
        }

        NetworkBossCore boss = bossHurtbox.GetComponentInParent<NetworkBossCore>();
        if (boss == null)
        {
            return;
        }

        Vector3 closestPoint = GetClosestPointSafe(hit, hitCenter);
        float sqrDistance = (closestPoint - hitCenter).sqrMagnitude;
        if (!_bestBossHurtboxes.TryGetValue(boss, out BossHitCandidate best) ||
            sqrDistance < best.SqrDistance ||
            (Mathf.Approximately(sqrDistance, best.SqrDistance) &&
             bossHurtbox.damageMultiplier > best.Hurtbox.damageMultiplier))
        {
            _bestBossHurtboxes[boss] = new BossHitCandidate(bossHurtbox, hit, sqrDistance);
        }
    }

    // 공격자에서 피격 콜라이더로 레이를 쏴 정확한 표면 위치와 법선을 구한다.
    private void SpawnBloodOnHitExtended(Transform attackOrigin, Vector3 hitCenter, Collider hitCollider, BossHurtbox bossHurtbox, string effectId)
    {
        if (attackOrigin == null || bloodEffectSpawner == null || hitCollider == null)
        {
            return;
        }

        // 기본 레이 시작점은 캐릭터의 공격 판정 높이로 잡는다.
        Vector3 rayStart = attackOrigin.position + Vector3.up * basicAttackHitHeight;

        // 머리 위치가 등록되어 있으면 더 자연스러운 공격 방향을 사용한다.
        if (attackOrigin.TryGetComponent<NetworkPlayerController>(out var attacker))
        {
            if (attacker.PlayerHeadTransform != null)
            {
                rayStart = attacker.PlayerHeadTransform.position;
            }
        }

        // 선택된 피격 콜라이더 중심을 향해 레이를 만든다.
        Vector3 targetPoint = hitCollider.bounds.center;
        Vector3 rayDirection = (targetPoint - rayStart).normalized;

        Vector3 hitPoint;
        Vector3 hitNormal;

        // 충돌하면 실제 표면 좌표와 바깥쪽 법선을 사용한다.
        Ray ray = new Ray(rayStart, rayDirection);
        if (hitCollider.Raycast(ray, out RaycastHit hitInfo, 15f))
        {
            hitPoint = hitInfo.point;
            hitNormal = hitInfo.normal;
        }
        else
        {
            hitPoint = GetClosestPointSafe(hitCollider, hitCenter);
            hitNormal = (hitPoint - attackOrigin.position).normalized;
            hitNormal.y = 0f;
        }

        if (hitNormal.sqrMagnitude <= 0.001f)
        {
            hitNormal = attackOrigin.forward;
        }

        // 피 분출과 타격음은 같은 호출에서 처리한다.
        bloodEffectSpawner.SpawnBlood(effectId, hitPoint, hitNormal);
    }

    private void ResolveReferences()
    {
        if (bloodEffectSpawner == null)
        {
            bloodEffectSpawner = FindFirstObjectByType<BloodEffectSpawner>();
        }
    }
}
