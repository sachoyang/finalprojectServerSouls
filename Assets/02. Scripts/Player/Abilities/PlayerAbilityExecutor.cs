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

    public void EquipModule(PlayerAbilityModule module, PlayerAbilityContext context)
    {
        if (module == null)
        {
            return;
        }

        if (!module.IsActive)
        {
            context.Stats?.ApplyPassiveStatBonus(module);
            PlayPresentation(module, context);
            ApplyEffect(module, context);
        }

        ApplySpecialEffect(module, context);
    }

    public void EquipPassive(PlayerAbilityModule module, PlayerAbilityContext context)
    {
        EquipModule(module, context);
    }

    // 씬 이동 복구에서는 PlayerStats 스냅샷에 패시브 수치가 이미 포함되어 있다.
    // 스탯/회복/애니메이션/VFX는 다시 실행하지 않고 콤보 해금 같은 영구 특수효과만 복구한다.
    public void RestoreModule(PlayerAbilityModule module, PlayerAbilityContext context)
    {
        if (module == null)
        {
            return;
        }

        ApplySpecialEffect(module, context);
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

        PlayerStats stats = context.Owner.GetComponent<PlayerStats>();
        NetworkPlayerController controller = context.Owner.GetComponent<NetworkPlayerController>();
        if ((stats != null && stats.IsDead) ||
            (controller != null && controller.IsDamageOrDeathActionActive))
        {
            // 스킬 연출은 피격/사망 연출보다 우선순위가 낮다.
            // 늦게 도착한 스킬 RPC가 호스트와 클라이언트에서 피격/사망 애니메이션을
            // 서로 다르게 덮어쓰지 못하도록 여기서 막는다.
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

    }

    private static void ApplySpecialEffect(PlayerAbilityModule module, PlayerAbilityContext context)
    {
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
        PlayerStatusController statusController = context.Owner != null ? context.Owner.GetComponent<PlayerStatusController>() : null;
        float damage = module.HitboxDamage;
        if (statusController != null)
        {
            damage *= statusController.GetOutgoingDamageMultiplier();
        }

        PlayerSkillHitbox skillHitbox = hitbox.GetComponent<PlayerSkillHitbox>();
        if (skillHitbox != null)
        {
            skillHitbox.Initialize(
                context.Owner,
                attacker,
                damage,
                module.HitboxRevivePower,
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
