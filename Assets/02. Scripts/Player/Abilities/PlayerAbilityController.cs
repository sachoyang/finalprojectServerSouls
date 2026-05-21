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

    private void Awake()
    {
        _inventory = GetComponent<PlayerAbilityInventory>();
        _stats = GetComponent<PlayerStats>();
        _playerController = GetComponent<NetworkPlayerController>();
    }

    private void Update()
    {
        // 내 플레이어가 아니면 로컬 키 입력을 읽지 않는다.
        // 이렇게 해야 다른 플레이어의 액티브 스킬이 내 키 입력으로 실행되지 않는다.
        if (Object == null ||
            !Object.HasInputAuthority ||
            _inventory == null ||
            (_stats != null && _stats.IsDead) ||
            (_playerController != null && _playerController.IsActionAnimationLocked))
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
                // 키 입력은 로컬에서 감지하지만, 실제 실행은 StateAuthority에 RPC로 요청한다.
                RPC_RequestActivateAbility(i);
            }
        }
    }

    // 액티브 슬롯 번호에 해당하는 능력을 실제로 실행한다.
    // 테스트나 AI용으로 직접 호출할 수도 있지만, 네트워크 플레이에서는 RPC를 통해 호출되는 흐름이 기본이다.
    public bool TryActivateAbility(int activeSlotIndex)
    {
        if (_inventory == null || (_playerController != null && _playerController.IsActionAnimationLocked))
        {
            return false;
        }

        PlayerAbilitySlot slot = _inventory.GetActiveSlot(activeSlotIndex);
        PlayerAbilityModule module = slot?.Module;
        if (module == null || !module.IsActive)
        {
            return false;
        }

        float currentTime = Runner != null ? Runner.SimulationTime : Time.time;
        PlayerAbilityContext context = _inventory.CreateContext();

        // 공통 사용 조건: 쿨다운 완료 + 모듈별 사용 가능 조건.
        if (!slot.IsReady(currentTime) || !module.CanActivate(context))
        {
            return false;
        }

        // 스태미나 소모는 모든 액티브 모듈에 공통으로 적용한다.
        // 스태미나를 쓰지 않는 능력은 모듈의 staminaCost를 0으로 두면 된다.
        if (_stats != null && !_stats.TryUseStamina(module.StaminaCost))
        {
            return false;
        }

        // 여기서 각 모듈의 Activate 구현이 실행된다.
        module.Activate(context);
        slot.StartCooldown(currentTime);
        return true;
    }

    // 입력 권한을 가진 클라이언트가 StateAuthority에게 "이 슬롯 능력을 사용하고 싶다"고 요청한다.
    // 최종 성공/실패 판정은 TryActivateAbility에서 서버/호스트 기준으로 처리된다.
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_RequestActivateAbility(int activeSlotIndex)
    {
        TryActivateAbility(activeSlotIndex);
    }
}
