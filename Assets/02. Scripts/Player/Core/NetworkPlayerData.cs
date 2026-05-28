using Fusion;
using UnityEngine;

[DisallowMultipleComponent]
public class NetworkPlayerData : NetworkBehaviour
{
    public const int MaxSavedAbilities = 16;

    [Networked] public PlayerRef Owner { get; private set; }
    [Networked] public int SavedAbilityCount { get; private set; }
    [Networked] public int LastSelectedRewardStage { get; private set; }
    [Networked, Capacity(MaxSavedAbilities)] public NetworkArray<NetworkString<_64>> SavedAbilityIds => default;

    public override void Spawned()
    {
        if (HasStateAuthority)
        {
            Owner = Object.InputAuthority;
        }

        GetComponent<PlayerAbilityInventory>()?.RestoreFromSessionData(Object.InputAuthority);
    }

    public void RecordAbility(PlayerAbilityModule module)
    {
        if (module == null)
        {
            return;
        }

        RecordAbilityId(module.AbilityId);
    }

    public void RecordAbilityId(string abilityId)
    {
        if (string.IsNullOrWhiteSpace(abilityId))
        {
            return;
        }

        if (HasStateAuthority)
        {
            PlayerSessionStore.SaveAbility(Owner, abilityId);
            AddAbilityId(abilityId);
            return;
        }

        if (Object != null && Object.HasInputAuthority)
        {
            PlayerSessionStore.SaveAbility(Object.InputAuthority, abilityId);
            RPC_RecordAbilityId(abilityId);
        }
    }

    public string GetAbilityId(int index)
    {
        if (index < 0 || index >= SavedAbilityCount || index >= MaxSavedAbilities)
        {
            return string.Empty;
        }

        return SavedAbilityIds[index].ToString();
    }

    public bool HasAbilityId(string abilityId)
    {
        if (string.IsNullOrWhiteSpace(abilityId))
        {
            return false;
        }

        for (int i = 0; i < SavedAbilityCount && i < MaxSavedAbilities; i++)
        {
            if (SavedAbilityIds[i].ToString() == abilityId)
            {
                return true;
            }
        }

        return false;
    }

    public void MarkRewardSelected(int bossStage)
    {
        if (bossStage <= 0)
        {
            return;
        }

        if (HasStateAuthority)
        {
            PlayerSessionStore.MarkRewardSelected(Owner, bossStage);
            LastSelectedRewardStage = Mathf.Max(LastSelectedRewardStage, bossStage);
            return;
        }

        if (Object != null && Object.HasInputAuthority)
        {
            PlayerSessionStore.MarkRewardSelected(Object.InputAuthority, bossStage);
            RPC_MarkRewardSelected(bossStage);
        }
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_RecordAbilityId(string abilityId)
    {
        AddAbilityId(abilityId);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_MarkRewardSelected(int bossStage)
    {
        PlayerSessionStore.MarkRewardSelected(Owner, bossStage);
        LastSelectedRewardStage = Mathf.Max(LastSelectedRewardStage, bossStage);
    }

    private void AddAbilityId(string abilityId)
    {
        if (HasAbilityId(abilityId) || SavedAbilityCount >= MaxSavedAbilities)
        {
            return;
        }

        SavedAbilityIds.Set(SavedAbilityCount, abilityId);
        SavedAbilityCount++;

        PlayerSessionStore.SaveAbility(Owner, abilityId);
        GetComponent<PlayerAbilityInventory>()?.RestoreFromSessionData(Owner);
    }
}
