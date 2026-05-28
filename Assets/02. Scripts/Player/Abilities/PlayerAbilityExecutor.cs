using Fusion;
using UnityEngine;

[DisallowMultipleComponent]
// PlayerAbilityModule에 들어있는 데이터 값을 읽어서 실제 게임 효과로 실행하는 컴포넌트다.
// Module은 "무슨 능력인지"를 설명하고, Executor는 "그 능력을 어떻게 실행할지"를 담당한다.
public class PlayerAbilityExecutor : MonoBehaviour
{
    // 보상을 선택했을 때 이 능력을 현재 플레이어가 획득할 수 있는지 검사한다.
    public bool CanEquip(PlayerAbilityModule module, PlayerAbilityContext context)
    {
        return module != null && context.Owner != null;
    }

    // 액티브 능력을 지금 사용할 수 있는지 검사한다.
    // 입력, 쿨타임, 스태미나 소모는 PlayerAbilityController에서 처리하고,
    // 여기서는 모듈 타입과 플레이어 상태처럼 실행 자체에 필요한 조건만 확인한다.
    public bool CanActivate(PlayerAbilityModule module, PlayerAbilityContext context)
    {
        return module != null &&
               module.IsActive &&
               context.Owner != null &&
               (context.Stats == null || !context.Stats.IsDead);
    }

    // 패시브 능력을 획득한 순간 적용한다.
    // 스탯 보너스는 PlayerStats에 등록하고, 회복/해금/연출 같은 즉시 효과는 Executor가 실행한다.
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

    // 액티브 능력을 실제로 실행한다.
    public void Activate(PlayerAbilityModule module, PlayerAbilityContext context)
    {
        if (!CanActivate(module, context))
        {
            return;
        }

        PlayPresentation(module, context);
        ApplyEffect(module, context);
    }

    // 에셋에 설정된 즉시 효과 값을 실제 플레이어 상태에 적용한다.
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

    // 애니메이션, 파티클, 히트박스처럼 눈에 보이는 연출/판정을 실행한다.
    private static void PlayPresentation(PlayerAbilityModule module, PlayerAbilityContext context)
    {
        if (context.Owner == null)
        {
            return;
        }

        Animator animator = context.Owner.GetComponentInChildren<Animator>();
        if (animator != null && !string.IsNullOrWhiteSpace(module.AnimationTrigger))
        {
            animator.ResetTrigger(module.AnimationTrigger);
            animator.SetTrigger(module.AnimationTrigger);
        }

        SpawnPrefab(context, module.EffectPrefab, module.EffectLocalOffset, module.ParentEffectToPlayer);
        SpawnPrefab(context, module.HitboxPrefab, module.HitboxLocalOffset, false);
    }

    // 네트워크 프리팹이면 Fusion Runner로 생성하고, 일반 프리팹이면 Unity Instantiate로 생성한다.
    private static void SpawnPrefab(PlayerAbilityContext context, GameObject prefab, Vector3 localOffset, bool parentToPlayer)
    {
        if (prefab == null || context.Transform == null)
        {
            return;
        }

        Vector3 position = context.Transform.TransformPoint(localOffset);
        Quaternion rotation = context.Transform.rotation;

        NetworkRunner runner = context.Runner;
        NetworkObject networkPrefab = prefab.GetComponent<NetworkObject>();
        if (runner != null && networkPrefab != null)
        {
            runner.Spawn(networkPrefab, position, rotation, null);
            return;
        }

        GameObject instance = Instantiate(prefab, position, rotation);
        if (parentToPlayer)
        {
            instance.transform.SetParent(context.Transform, true);
        }
    }
}
