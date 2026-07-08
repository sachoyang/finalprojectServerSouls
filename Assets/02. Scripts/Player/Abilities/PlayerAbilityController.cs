using Fusion;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerAbilityInventory))]
// 액티브 능력 키 입력을 읽고 실제 능력 사용을 요청하는 컴포넌트.
// Fusion 네트워크 게임에서는 입력 권한이 있는 클라이언트가 키를 감지하고,
// 상태 권한을 가진 쪽에서 최종 스태미나/쿨다운 검사와 효과 실행을 처리한다.
public class PlayerAbilityController : NetworkBehaviour
{
    private PlayerAbilityInventory _inventory;
    private PlayerStats _stats;
    private NetworkPlayerController _playerController;
    private PlayerAbilityExecutor _executor;

    private void Awake()
    {
        _inventory = GetComponent<PlayerAbilityInventory>();
        _stats = GetComponent<PlayerStats>();
        _playerController = GetComponent<NetworkPlayerController>();
        _executor = GetComponent<PlayerAbilityExecutor>();
        if (_executor == null)
        {
            _executor = gameObject.AddComponent<PlayerAbilityExecutor>();
        }
    }

    private void Update()
    {
        // 내 플레이어가 아니면 로컬 키 입력을 읽지 않는다.
        // 이렇게 해야 다른 플레이어의 액티브 스킬이 내 키 입력으로 실행되지 않는다.
        if (Object == null ||
            !Object.HasInputAuthority ||
            _inventory == null ||
            (_stats != null && _stats.IsDead) ||
            (_playerController != null &&
             (_playerController.IsActionAnimationLocked || _playerController.HasControlLock(PlayerControlLockFlags.Skill))))
        {
            return;
        }

        for (int i = 0; i < _inventory.ActiveSlots.Count; i++)
        {
            PlayerAbilitySlot slot = _inventory.ActiveSlots[i];
            // 슬롯이 비어 있거나 키가 배정되지 않았으면 건너뛴다.
            if (slot?.Module == null || slot.KeyCode == KeyCode.None)
            {
                continue;
            }

            if (Input.GetKeyDown(slot.KeyCode))
            {
                // 키 입력은 로컬에서 감지하되, 비호스트는 예측 재생 없이 StateAuthority에 사용 요청만 보낸다.
                // 예측 재생을 하지 않는 이유는 서버 확정 RPC와 겹쳐 스킬 모션이 두 번 재생되는 문제를 막기 위해서다.
                if (HasStateAuthority)
                {
                    TryActivateAbility(i);
                }
                else
                {
                    RPC_RequestActivateAbility(i);
                }
            }
        }
    }

    // 액티브 슬롯 번호에 해당하는 능력을 실제로 실행한다.
    // 테스트나 AI용으로 직접 호출할 수도 있지만, 네트워크 플레이에서는 RPC를 통해 호출되는 흐름이 기본이다.
    public bool TryActivateAbility(int activeSlotIndex)
    {
        // 스킬은 공격/피격/구르기 같은 액션락 중에는 시작하지 않는다.
        // 이 검사는 서버 권한에서 최종 적용되며, 클라이언트 로컬 입력은 RPC 요청까지만 담당한다.
        if (_inventory == null ||
            (_playerController != null &&
             (_playerController.IsActionAnimationLocked || _playerController.HasControlLock(PlayerControlLockFlags.Skill))))
        {
            return false;
        }

        PlayerAbilitySlot slot = _inventory.GetActiveSlot(activeSlotIndex);
        PlayerAbilityModule module = slot?.Module;
        // 슬롯이 비어 있거나 패시브/보상용 모듈이면 액티브 스킬로 실행하지 않는다.
        if (module == null || !module.UsesActiveSlot)
        {
            return false;
        }

        float currentTime = Runner != null ? Runner.SimulationTime : Time.time;
        PlayerAbilityContext context = _inventory.CreateContext();
        int abilityLevel = slot.Level;

        // 공통 사용 조건: 쿨다운 완료 + 모듈별 사용 가능 조건.
        if (!slot.IsReady(currentTime) || _executor == null || !_executor.CanActivate(module, context))
        {
            return false;
        }

        // 스태미나 소모는 모든 액티브 모듈에 공통으로 적용한다.
        // 스태미나를 쓰지 않는 능력은 모듈의 staminaCost를 0으로 두면 된다.
        bool waitsForSkillAnimationEnd = !string.IsNullOrWhiteSpace(module.AnimationTrigger);
        float staminaCost = module.StaminaCost;
        if (_stats != null &&
            !(waitsForSkillAnimationEnd
                ? _stats.TryUseActionStamina(staminaCost)
                : _stats.TryUseStamina(staminaCost)))
        {
            return false;
        }

        // 실제 효과와 히트박스는 서버 권한에서 확정하고, 표현 재생은 아래 RPC로 모든 클라이언트에 알린다.
        // Activate는 게임 결과, PlayPresentation은 애니메이션/VFX 표현이라는 식으로 역할을 나눈다.
        _executor.Activate(module, context, abilityLevel);
        slot.StartCooldown(currentTime);
        RPC_PlayAbilityPresentation(module.AbilityId, slot.NextReadyTime);
        return true;
    }

    // 입력 권한을 가진 클라이언트가 StateAuthority에게 "이 슬롯 능력을 사용하고 싶다"고 요청한다.
    // 최종 성공/실패 판정은 TryActivateAbility에서 서버/호스트 기준으로 처리된다.
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_RequestActivateAbility(int activeSlotIndex)
    {
        // 이 RPC는 "요청"일 뿐 성공 보장이 아니다.
        // 쿨다운, 스태미나, 액션락 검사는 TryActivateAbility에서 서버 기준으로 다시 수행된다.
        TryActivateAbility(activeSlotIndex);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayAbilityPresentation(string abilityId, float cooldownEndTime)
    {
        // 서버가 사용 성공을 확정한 뒤에만 애니메이션/VFX를 재생한다.
        // 로컬 예측 재생을 하지 않으므로 입력권한 클라이언트도 이 RPC에서 한 번만 재생한다.
        if (_inventory == null)
        {
            _inventory = GetComponent<PlayerAbilityInventory>();
        }

        if (_executor == null)
        {
            _executor = GetComponent<PlayerAbilityExecutor>();
        }

        PlayerAbilityModule module = _inventory != null ? _inventory.FindModuleById(abilityId) : null;
        if (module == null || _executor == null)
        {
            return;
        }

        // 입력권한 클라이언트도 서버 확정 쿨다운 시간을 그대로 받는다.
        // 이렇게 해야 호스트/비호스트 UI의 쿨다운 표시가 서로 다른 시간으로 흐르지 않는다.
        ApplyCooldownToLocalSlot(abilityId, cooldownEndTime);

        // 표현 재생은 서버가 사용 성공을 확정한 뒤 모든 클라이언트가 동일하게 수행한다.
        // 로컬에서 먼저 재생하지 않으므로 같은 스킬 모션이 두 번 호출되는 경로가 없다.
        _executor.PlayPresentation(module, _inventory.CreateContext());
    }

    private void ApplyCooldownToLocalSlot(string abilityId, float cooldownEndTime)
    {
        if (_inventory == null || string.IsNullOrWhiteSpace(abilityId))
        {
            return;
        }

        for (int i = 0; i < _inventory.ActiveSlots.Count; i++)
        {
            PlayerAbilitySlot slot = _inventory.ActiveSlots[i];
            if (slot?.Module != null && slot.Module.AbilityId == abilityId)
            {
                slot.SetCooldownEndTime(cooldownEndTime);
                return;
            }
        }
    }
}
