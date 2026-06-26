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
        ResolveBasicAttackTargets(attacker, attackerStats, damage, hitCount);
        ApplyBossHits(attackOrigin, attacker, damage, groggyDamage);
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

    private void ResolveBasicAttackTargets(
        NetworkObject attacker,
        PlayerStats attackerStats,
        float damage,
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

            if (TryApplyReviveHit(hit, attacker, attackerStats))
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

    private bool TryApplyReviveHit(Collider hit, NetworkObject attacker, PlayerStats attackerStats)
    {
        PlayerStats hitPlayerStats = hit.GetComponentInParent<PlayerStats>();
        if (hitPlayerStats == null || hitPlayerStats == attackerStats || !hitPlayerStats.IsDead)
        {
            return false;
        }

        if (_reviveHitPlayers.Add(hitPlayerStats))
        {
            hitPlayerStats.RegisterReviveHit(attacker, basicAttackRevivePower);
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
