using Fusion;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerAbilityInventory))]
public class PlayerAbilityController : NetworkBehaviour
{
    private PlayerAbilityInventory _inventory;
    private PlayerStats _stats;

    private void Awake()
    {
        _inventory = GetComponent<PlayerAbilityInventory>();
        _stats = GetComponent<PlayerStats>();
    }

    private void Update()
    {
        if (!Object.HasInputAuthority || _inventory == null || (_stats != null && _stats.IsDead))
        {
            return;
        }

        for (int i = 0; i < _inventory.ActiveSlots.Count; i++)
        {
            PlayerAbilitySlot slot = _inventory.ActiveSlots[i];
            if (slot?.Module == null || slot.KeyCode == KeyCode.None)
            {
                continue;
            }

            if (Input.GetKeyDown(slot.KeyCode))
            {
                RPC_RequestActivateAbility(i);
            }
        }
    }

    public bool TryActivateAbility(int activeSlotIndex)
    {
        if (_inventory == null)
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

        if (!slot.IsReady(currentTime) || !module.CanActivate(context))
        {
            return false;
        }

        if (_stats != null && !_stats.TryUseStamina(module.StaminaCost))
        {
            return false;
        }

        module.Activate(context);
        slot.StartCooldown(currentTime);
        return true;
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_RequestActivateAbility(int activeSlotIndex)
    {
        TryActivateAbility(activeSlotIndex);
    }
}
