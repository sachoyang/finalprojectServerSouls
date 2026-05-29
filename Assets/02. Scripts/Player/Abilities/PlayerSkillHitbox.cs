using System.Collections.Generic;
using Fusion;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(Rigidbody))]
public class PlayerSkillHitbox : NetworkBehaviour
{
    [SerializeField] private float defaultDamage;
    [SerializeField] private float defaultDelay;
    [SerializeField] private float defaultLifetime = 0.3f;

    private readonly HashSet<NetworkBossCore> _hitBosses = new HashSet<NetworkBossCore>();
    private readonly HashSet<BossHitbox> _hitboxesWithoutBoss = new HashSet<BossHitbox>();

    private GameObject _owner;
    private NetworkObject _attacker;
    private float _damage;
    private float _delay;
    private float _lifetime;
    private Collider _hitboxCollider;

    private void Awake()
    {
        _hitboxCollider = GetComponent<Collider>();
        _hitboxCollider.isTrigger = true;
        _hitboxCollider.enabled = false;

        Rigidbody body = GetComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;

        _damage = defaultDamage;
        _delay = defaultDelay;
        _lifetime = defaultLifetime;
    }

    public override void Spawned()
    {
        if (Object.HasStateAuthority)
        {
            ScheduleActivation();
        }
    }

    public void Initialize(GameObject owner, NetworkObject attacker, float damage, float delay, float lifetime)
    {
        _owner = owner;
        _attacker = attacker;
        _damage = damage > 0f ? damage : defaultDamage;
        _delay = Mathf.Max(0f, delay > 0f ? delay : defaultDelay);
        _lifetime = lifetime > 0f ? lifetime : defaultLifetime;

        if (Object == null || Object.HasStateAuthority)
        {
            ScheduleActivation();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (Object != null && !Object.HasStateAuthority)
        {
            return;
        }

        if (_owner != null && other.transform.IsChildOf(_owner.transform))
        {
            return;
        }

        BossHitbox bossHitbox = other.GetComponentInParent<BossHitbox>();
        if (bossHitbox == null)
        {
            return;
        }

        NetworkBossCore boss = bossHitbox.GetComponentInParent<NetworkBossCore>();
        if (boss != null)
        {
            if (!_hitBosses.Add(boss))
            {
                return;
            }
        }
        else if (!_hitboxesWithoutBoss.Add(bossHitbox))
        {
            return;
        }

        bossHitbox.OnHitByPlayer(_damage, _attacker);
    }

    private void ScheduleActivation()
    {
        CancelInvoke(nameof(EnableHitbox));
        CancelInvoke(nameof(DespawnSelf));

        if (_hitboxCollider != null)
        {
            _hitboxCollider.enabled = false;
        }

        if (_delay <= 0f)
        {
            EnableHitbox();
            return;
        }

        Invoke(nameof(EnableHitbox), _delay);
    }

    private void EnableHitbox()
    {
        if (_hitboxCollider != null)
        {
            _hitboxCollider.enabled = true;
        }

        Invoke(nameof(DespawnSelf), _lifetime);
    }

    private void DespawnSelf()
    {
        if (Object != null && Runner != null && Object.HasStateAuthority)
        {
            Runner.Despawn(Object);
            return;
        }

        if (Object == null)
        {
            Destroy(gameObject);
        }
    }
}
