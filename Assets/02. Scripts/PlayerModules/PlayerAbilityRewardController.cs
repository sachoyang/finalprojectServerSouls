using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerAbilityInventory))]
public class PlayerAbilityRewardController : MonoBehaviour
{
    [SerializeField, Range(1, 8)] private int lastClearedBossStage;
    [SerializeField] private List<PlayerAbilityModule> pendingOptions = new List<PlayerAbilityModule>();

    private PlayerAbilityInventory _inventory;

    public int LastClearedBossStage => lastClearedBossStage;
    public IReadOnlyList<PlayerAbilityModule> PendingOptions => pendingOptions;

    public event Action<int, IReadOnlyList<PlayerAbilityModule>> BossRewardOffered;
    public event Action<PlayerAbilityModule> BossRewardSelected;

    private void Awake()
    {
        _inventory = GetComponent<PlayerAbilityInventory>();
    }

    public IReadOnlyList<PlayerAbilityModule> OfferBossReward(int bossStage)
    {
        if (_inventory == null)
        {
            return pendingOptions;
        }

        lastClearedBossStage = Mathf.Clamp(bossStage, 1, 8);
        pendingOptions = _inventory.GenerateRewardOptions(lastClearedBossStage, 3);
        BossRewardOffered?.Invoke(lastClearedBossStage, pendingOptions);
        return pendingOptions;
    }

    public bool SelectPendingOption(int optionIndex)
    {
        if (_inventory == null || optionIndex < 0 || optionIndex >= pendingOptions.Count)
        {
            return false;
        }

        PlayerAbilityModule selected = pendingOptions[optionIndex];
        if (!_inventory.SelectRewardOption(selected))
        {
            return false;
        }

        pendingOptions.Clear();
        BossRewardSelected?.Invoke(selected);
        return true;
    }
}
