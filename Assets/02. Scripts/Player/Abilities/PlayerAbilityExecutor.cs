using Fusion;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerAbilityExecutor : MonoBehaviour
{
    public bool CanEquip(PlayerAbilityModule module, PlayerAbilityContext context)
    {
        return module != null && context.Owner != null;
    }

    public bool CanActivate(PlayerAbilityModule module, PlayerAbilityContext context)
    {
        return module != null &&
               module.IsActive &&
               context.Owner != null &&
               (context.Stats == null || !context.Stats.IsDead);
    }

    public void EquipPassive(PlayerAbilityModule module, PlayerAbilityContext context)
    {
        if (module == null || module.IsActive)
        {
            return;
        }

        context.Stats?.ApplyPassiveStatBonus(module);
        PlayPresentation(module, context);
        ApplyEffect(module, context);
    }

    public void Activate(PlayerAbilityModule module, PlayerAbilityContext context)
    {
        if (!CanActivate(module, context))
        {
            return;
        }

        ApplyEffect(module, context);
        SpawnHitbox(context, module);
    }

    public void PlayPresentation(PlayerAbilityModule module, PlayerAbilityContext context)
    {
        if (module == null || context.Owner == null)
        {
            return;
        }

        PlayAnimation(module, context);
        SpawnLocalPrefab(context, module.EffectPrefab, module.EffectLocalOffset, module.ParentEffectToPlayer);
    }

    private static void PlayAnimation(PlayerAbilityModule module, PlayerAbilityContext context)
    {
        Animator animator = context.Owner.GetComponentInChildren<Animator>();
        if (animator != null && !string.IsNullOrWhiteSpace(module.AnimationTrigger))
        {
            animator.ResetTrigger(module.AnimationTrigger);
            animator.SetTrigger(module.AnimationTrigger);
        }
    }

    private static void SpawnLocalPrefab(PlayerAbilityContext context, GameObject prefab, Vector3 localOffset, bool parentToPlayer)
    {
        if (prefab == null || context.Transform == null)
        {
            return;
        }

        Vector3 position = context.Transform.TransformPoint(localOffset);
        Quaternion rotation = context.Transform.rotation;

        GameObject instance = Instantiate(prefab, position, rotation);
        if (parentToPlayer)
        {
            instance.transform.SetParent(context.Transform, true);
        }
    }

    private static void ApplyEffect(PlayerAbilityModule module, PlayerAbilityContext context)
    {
        if (module.HealthRestoreAmount > 0f)
        {
            context.Stats?.Heal(module.HealthRestoreAmount);
        }

        if (module.StaminaRestoreAmount > 0f)
        {
            context.Stats?.RestoreStamina(module.StaminaRestoreAmount);
        }

        if (module.SpecialEffect == PlayerAbilitySpecialEffect.UnlockBasicAttackCombo)
        {
            context.Owner?.GetComponent<NetworkPlayerController>()?.UnlockBasicAttackCombo();
        }
    }

    private static void SpawnHitbox(PlayerAbilityContext context, PlayerAbilityModule module)
    {
        GameObject hitbox = SpawnPrefab(context, module.HitboxPrefab, module.HitboxLocalOffset, false);
        if (hitbox == null)
        {
            return;
        }

        NetworkObject attacker = context.Owner != null ? context.Owner.GetComponent<NetworkObject>() : null;
        PlayerSkillHitbox skillHitbox = hitbox.GetComponent<PlayerSkillHitbox>();
        if (skillHitbox != null)
        {
            skillHitbox.Initialize(
                context.Owner,
                attacker,
                module.HitboxDamage,
                module.HitboxDelay,
                module.HitboxLifetime);
        }
    }

    private static GameObject SpawnPrefab(PlayerAbilityContext context, GameObject prefab, Vector3 localOffset, bool parentToPlayer)
    {
        if (prefab == null || context.Transform == null)
        {
            return null;
        }

        Vector3 position = context.Transform.TransformPoint(localOffset);
        Quaternion rotation = context.Transform.rotation;

        NetworkRunner runner = context.Runner;
        NetworkObject networkPrefab = prefab.GetComponent<NetworkObject>();
        if (runner != null && networkPrefab != null)
        {
            NetworkObject networkInstance = runner.Spawn(networkPrefab, position, rotation, null);
            return networkInstance != null ? networkInstance.gameObject : null;
        }

        GameObject instance = Instantiate(prefab, position, rotation);
        if (parentToPlayer)
        {
            instance.transform.SetParent(context.Transform, true);
        }

        return instance;
    }
}
