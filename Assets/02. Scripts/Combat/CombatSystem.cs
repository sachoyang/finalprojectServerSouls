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
    [Header("Basic Attack Hit")]
    [SerializeField] private float basicAttackHitRadius = 1.4f;
    [SerializeField] private float basicAttackHitDistance = 1.8f;
    [SerializeField] private float basicAttackHitHeight = 1.1f;
    [SerializeField] private LayerMask basicAttackTargetLayers = ~0;

    [Header("Revive")]
    [SerializeField] private float basicAttackRevivePower = 34f;

    [Header("Effects")]
    [SerializeField] private BloodEffectSpawner bloodEffectSpawner;

    private readonly Collider[] _hitBuffer = new Collider[128];
    private readonly Dictionary<NetworkBossCore, BossHurtbox> _bestBossHurtboxes = new Dictionary<NetworkBossCore, BossHurtbox>();
    private readonly HashSet<PlayerStats> _reviveHitPlayers = new HashSet<PlayerStats>();

    public float BasicAttackHitRadius => basicAttackHitRadius;
    public Vector3 BasicAttackHitLocalCenter => Vector3.up * basicAttackHitHeight + Vector3.forward * basicAttackHitDistance;
    public int LastAbilityRawHitCount { get; private set; }
    public int LastAbilityFilteredHitCount { get; private set; }
    public int LastAbilityBossHurtboxCount { get; private set; }

    private void Awake()
    {
        ResolveReferences();
    }

    public void ProcessBasicAttackHit(
        NetworkObject attacker,
        PlayerStats attackerStats,
        Transform attackOrigin,
        float damage,
        float groggyDamage = 10f)
    {
        if (attackOrigin == null || damage <= 0f)
        {
            return;
        }

        ResolveReferences();
        int hitCount = CollectBasicAttackHits(attackOrigin);
        ResolveHitTargets(attacker, attackerStats, damage, basicAttackRevivePower, hitCount);
        ApplyBossHits(attackOrigin, attackOrigin.TransformPoint(BasicAttackHitLocalCenter), attacker, damage, groggyDamage);
    }

    public bool ProcessAbilityHitEvent(
        NetworkObject attacker,
        PlayerStats attackerStats,
        Transform attackOrigin,
        AbilityHitEvent hitEvent,
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
        int hitCount = CollectCylinderHits(attackOrigin, hitEvent);
        ResolveHitTargets(attacker, attackerStats, damage, hitEvent.RevivePower, hitCount);
        LastAbilityFilteredHitCount = hitCount;
        LastAbilityBossHurtboxCount = _bestBossHurtboxes.Count;
        bool hitBoss = _bestBossHurtboxes.Count > 0;
        ApplyBossHits(attackOrigin, GetAbilityHitCenter(attackOrigin, hitEvent), attacker, damage, hitEvent.GroggyDamage);
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
        int hitCount)
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

            TryCollectBestBossHurtbox(hit);
        }
    }

    private void ApplyBossHits(
        Transform attackOrigin,
        Vector3 hitCenter,
        NetworkObject attacker,
        float damage,
        float groggyDamage)
    {
        foreach (BossHurtbox bossHurtbox in _bestBossHurtboxes.Values)
        {
            bossHurtbox.OnHitByPlayer(damage, groggyDamage, attacker);
            SpawnBloodOnHit(attackOrigin, hitCenter, bossHurtbox.GetComponentInChildren<Collider>());
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

    private void TryCollectBestBossHurtbox(Collider hit)
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

        if (!_bestBossHurtboxes.TryGetValue(boss, out BossHurtbox bestHurtbox) ||
            bossHurtbox.damageMultiplier > bestHurtbox.damageMultiplier)
        {
            _bestBossHurtboxes[boss] = bossHurtbox;
        }
    }

    private void SpawnBloodOnHit(Transform attackOrigin, Vector3 hitCenter, Collider hitCollider)
    {
        if (attackOrigin == null || bloodEffectSpawner == null || hitCollider == null)
        {
            return;
        }

        Vector3 hitPoint = GetClosestPointSafe(hitCollider, hitCenter);

        Vector3 direction = hitPoint - attackOrigin.position;
        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.001f)
        {
            direction = attackOrigin.forward;
        }

        bloodEffectSpawner.SpawnBlood(hitPoint, direction.normalized);
    }

    private void ResolveReferences()
    {
        if (bloodEffectSpawner == null)
        {
            bloodEffectSpawner = FindFirstObjectByType<BloodEffectSpawner>();
        }
    }
}
