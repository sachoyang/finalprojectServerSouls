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

    private readonly Collider[] _hitBuffer = new Collider[16];
    private readonly Dictionary<NetworkBossCore, BossHurtbox> _bestBossHurtboxes = new Dictionary<NetworkBossCore, BossHurtbox>();
    private readonly HashSet<PlayerStats> _reviveHitPlayers = new HashSet<PlayerStats>();

    public float BasicAttackHitRadius => basicAttackHitRadius;
    public Vector3 BasicAttackHitLocalCenter => Vector3.up * basicAttackHitHeight + Vector3.forward * basicAttackHitDistance;

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
        ApplyBossHits(attackOrigin, attacker, damage, groggyDamage);
    }

    public void ProcessAbilityHitEvent(
        NetworkObject attacker,
        PlayerStats attackerStats,
        Transform attackOrigin,
        AbilityHitEvent hitEvent,
        float fallbackDamage,
        float outgoingDamageMultiplier = 1f)
    {
        if (attackOrigin == null || hitEvent == null)
        {
            return;
        }

        float baseDamage = hitEvent.Damage > 0f ? hitEvent.Damage : fallbackDamage;
        float damage = baseDamage * Mathf.Max(0f, outgoingDamageMultiplier);
        if (damage <= 0f)
        {
            return;
        }

        ResolveReferences();
        int hitCount = CollectCylinderHits(attackOrigin, hitEvent);
        ResolveHitTargets(attacker, attackerStats, damage, hitEvent.RevivePower, hitCount);
        ApplyBossHits(attackOrigin, attacker, damage, hitEvent.GroggyDamage);
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
        Vector3 center = attackOrigin.position + Vector3.up * (hitEvent.CenterHeight + halfHeight);
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

        int filteredCount = 0;
        float radiusSqr = hitEvent.Radius * hitEvent.Radius;
        for (int i = 0; i < rawCount; i++)
        {
            Collider hit = _hitBuffer[i];
            if (hit == null)
            {
                continue;
            }

            Vector3 closest = hit.ClosestPoint(center);
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

    private void ApplyBossHits(Transform attackOrigin, NetworkObject attacker, float damage, float groggyDamage)
    {
        foreach (BossHurtbox bossHurtbox in _bestBossHurtboxes.Values)
        {
            bossHurtbox.OnHitByPlayer(damage, groggyDamage, attacker);
            SpawnBloodOnHit(attackOrigin, bossHurtbox.GetComponent<Collider>());
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

    private void SpawnBloodOnHit(Transform attackOrigin, Collider hitCollider)
    {
        if (attackOrigin == null || bloodEffectSpawner == null || hitCollider == null)
        {
            return;
        }

        Vector3 hitCenter = attackOrigin.TransformPoint(BasicAttackHitLocalCenter);
        Vector3 hitPoint = hitCollider.ClosestPoint(hitCenter);

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
