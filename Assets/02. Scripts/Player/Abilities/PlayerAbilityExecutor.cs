using System.Collections;
using Fusion;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerAbilityExecutor : MonoBehaviour
{
    public PlayerAbilityModule ActiveHitEventModule { get; private set; }
    public AbilityHitEvent ActiveHitEvent { get; private set; }

    private void OnDisable()
    {
        ActiveHitEventModule = null;
        ActiveHitEvent = null;
    }

    public bool CanEquip(PlayerAbilityModule module, PlayerAbilityContext context)
    {
        return module != null && context.Owner != null;
    }

    public bool CanActivate(PlayerAbilityModule module, PlayerAbilityContext context)
    {
        return module != null &&
               module.UsesActiveSlot &&
               context.Owner != null &&
               (context.Stats == null || !context.Stats.IsDead);
    }

    public void EquipModule(PlayerAbilityModule module, PlayerAbilityContext context)
    {
        ApplyLevelChange(module, context, 0, 1);
    }

    public void ApplyLevelChange(
        PlayerAbilityModule module,
        PlayerAbilityContext context,
        int previousLevel,
        int newLevel)
    {
        if (module == null)
        {
            return;
        }

        if (module.IsPassive)
        {
            context.Stats?.ApplyPassiveStatBonus(module, previousLevel, newLevel);
            PlayPresentation(module, context);
        }
        else if (module.IsUtility && module.SpecialEffect != PlayerAbilitySpecialEffect.None)
        {
            PlayPresentation(module, context);
            ApplySpecialEffect(module, context);
        }
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

        if (module.IsUtility && module.SpecialEffect != PlayerAbilitySpecialEffect.None)
        {
            ApplySpecialEffect(module, context);
        }
    }

    public void Activate(PlayerAbilityModule module, PlayerAbilityContext context, int level)
    {
        if (!CanActivate(module, context))
        {
            return;
        }

        if (module.IsUtility)
        {
            ApplyEffect(module, context, level);
            return;
        }

        if (module.HitEvents == null || module.HitEvents.Length == 0)
        {
            return;
        }

        StartCoroutine(ProcessHitEvents(context, module, level));
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
        PlaySound(module, context);
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

        Transform parent = parentToPlayer ? context.Transform : null;
        EffectPoolManager.SpawnPooled(prefab, position, rotation, parent);
    }

    private static void PlaySound(PlayerAbilityModule module, PlayerAbilityContext context)
    {
        if (module.SoundClip == null || context.Transform == null)
        {
            return;
        }

        if (!SoundManager.HasInstance)
        {
            Debug.LogWarning(
                $"[PlayerAbilityExecutor] '{module.AbilityId}' 사운드를 재생할 SoundManager가 없습니다.");
            return;
        }

        // 스킬 표현 RPC는 각 클라이언트에서 한 번씩 실행되므로
        // 캐릭터 위치 기준 3D 사운드도 각 클라이언트에서 한 번만 재생된다.
        SoundManager.Instance.PlaySFX_3D(
            module.SoundClip,
            context.Transform.position,
            SoundCategory.SkillEffect,
            module.SoundVolume,
            Mathf.Max(0f, module.SoundDelay));
    }

    private static void ApplyEffect(PlayerAbilityModule module, PlayerAbilityContext context, int level)
    {
        float healthRestoreAmount = module.GetHealthRestoreAmount(level);
        float staminaRestoreAmount = module.GetStaminaRestoreAmount(level);
        if (healthRestoreAmount > 0f)
        {
            context.Stats?.Heal(healthRestoreAmount);
        }

        if (staminaRestoreAmount > 0f)
        {
            context.Stats?.RestoreStamina(staminaRestoreAmount);
        }

    }

    private static void ApplySpecialEffect(PlayerAbilityModule module, PlayerAbilityContext context)
    {
        if (module.SpecialEffect == PlayerAbilitySpecialEffect.UnlockBasicAttackCombo)
        {
            context.Owner?.GetComponent<NetworkPlayerController>()?.UnlockBasicAttackCombo();
        }
    }

    private IEnumerator ProcessHitEvents(PlayerAbilityContext context, PlayerAbilityModule module, int level)
    {
        if (context.Owner == null || context.Transform == null)
        {
            yield break;
        }

        NetworkObject attacker = context.Owner.GetComponent<NetworkObject>();
        if (attacker != null && !attacker.HasStateAuthority)
        {
            yield break;
        }

        CombatSystem combatSystem = FindFirstObjectByType<CombatSystem>();
        if (combatSystem == null)
        {
            // Path/Lobby 같은 비전투 씬에서는 스킬 연출만 재생하고 히트 판정은 생략한다.
            yield break;
        }

        PlayerStatusController statusController = context.Owner.GetComponent<PlayerStatusController>();
        float outgoingMultiplier = statusController != null ? statusController.GetOutgoingDamageMultiplier() : 1f;
        float duration = GetAbilityRuntimeDuration(module);
        float previousTime = 0f;

        foreach (AbilityHitEvent hitEvent in module.HitEvents)
        {
            if (hitEvent == null)
            {
                continue;
            }

            float eventTime = Mathf.Clamp01(hitEvent.StartNormalizedTime) * duration;
            float waitTime = Mathf.Max(0f, eventTime - previousTime);
            if (waitTime > 0f)
            {
                yield return new WaitForSeconds(waitTime);
            }

            previousTime = eventTime;
            float eventEndTime = Mathf.Clamp01(hitEvent.EndNormalizedTime) * duration;
            float sampleTime = eventTime;
            bool hitBoss = false;
            int maxRawHits = 0;
            int maxFilteredHits = 0;
            int maxBossHurtboxes = 0;
            ActiveHitEventModule = module;
            ActiveHitEvent = hitEvent;
            do
            {
                if (!hitBoss)
                {
                    hitBoss = combatSystem.ProcessAbilityHitEvent(
                        attacker,
                        context.Stats,
                        context.Transform,
                        hitEvent,
                        module.AbilityId,
                        module.GetDamageMultiplier(level),
                        outgoingMultiplier);
                }

                maxRawHits = Mathf.Max(maxRawHits, combatSystem.LastAbilityRawHitCount);
                maxFilteredHits = Mathf.Max(maxFilteredHits, combatSystem.LastAbilityFilteredHitCount);
                maxBossHurtboxes = Mathf.Max(maxBossHurtboxes, combatSystem.LastAbilityBossHurtboxCount);
                yield return new WaitForFixedUpdate();
                sampleTime += Time.fixedDeltaTime;
            }
            while (sampleTime <= eventEndTime);

            Debug.Log(
                $"[SkillHitEvent] {module.AbilityId}/{hitEvent.Label} " +
                $"level={level}, damageRate={module.GetDamageMultiplier(level)}, " +
                $"attackPower={context.Stats?.AttackPower ?? 0f}, " +
                $"raw={maxRawHits}, cylinder={maxFilteredHits}, " +
                $"bossHurtbox={maxBossHurtboxes}, applied={hitBoss}",
                context.Owner);
            ActiveHitEventModule = null;
            ActiveHitEvent = null;
            previousTime = Mathf.Min(sampleTime, eventEndTime);
        }

        ActiveHitEventModule = null;
        ActiveHitEvent = null;
    }

    private static float GetAbilityRuntimeDuration(PlayerAbilityModule module)
    {
        if (module.AnimationClip == null)
        {
            return 1f;
        }

        return module.AnimationClip.length / Mathf.Max(0.01f, module.AnimationSpeed);
    }

}
