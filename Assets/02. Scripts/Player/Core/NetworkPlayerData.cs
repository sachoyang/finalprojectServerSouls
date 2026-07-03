using Fusion;
using UnityEngine;

[DisallowMultipleComponent]
public class NetworkPlayerData : NetworkBehaviour
{
    public const int MaxSavedAbilities = 16;

    [Networked] public PlayerRef Owner { get; private set; }
    [Networked] public long UnlockedSkillsBitmask { get; private set; }
    [Networked] public int SavedAbilityCount { get; private set; }
    [Networked] public int LastSelectedRewardStage { get; private set; }
    [Networked, Capacity(MaxSavedAbilities)] public NetworkArray<NetworkString<_64>> SavedAbilityIds => default;

    public override void Spawned()
    {
        if (HasStateAuthority)
        {
            Owner = Object.InputAuthority;
        }

        SyncUnlockedSkillsBitmask();
        GetComponent<PlayerAbilityInventory>()?.RestoreFromSessionData(Object.InputAuthority);
    }

    private void SyncUnlockedSkillsBitmask()
    {
        long localBitmask = GetLocalUnlockedSkillsBitmask();
        if (HasStateAuthority && Object != null && Object.HasInputAuthority)
        {
            UnlockedSkillsBitmask = localBitmask;
            return;
        }

        if (Object != null && Object.HasInputAuthority)
        {
            RPC_SetUnlockedSkillsBitmask(localBitmask);
        }
    }

    private long GetLocalUnlockedSkillsBitmask()
    {
        // AbilityManager에게 기본 마스크를 물어볼 필요가 없습니다. 
        // 그냥 BackendManager가 로그인 시 서버로부터 받은 값(0이 절대 아님)을 그대로 던져주면 끝납니다!
        return BackendManager.HasInstance ? BackendManager.Instance.CurrentSkillsBitmask : 0L;
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
            // 방장 플레이어는 보상 선택 시 같은 오브젝트에서 이미 획득 효과를 적용했다.
            AddAbilityId(abilityId, false);
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
        // 일반 참가자의 로컬 PlayerStats에는 StateAuthority가 없으므로
        // 기존 보상 기록 RPC가 도착한 권한 오브젝트에서 획득 효과를 적용한다.
        AddAbilityId(abilityId, true);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_SetUnlockedSkillsBitmask(long unlockedSkillsBitmask)
    {
        UnlockedSkillsBitmask = unlockedSkillsBitmask;
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_MarkRewardSelected(int bossStage)
    {
        PlayerSessionStore.MarkRewardSelected(Owner, bossStage);
        LastSelectedRewardStage = Mathf.Max(LastSelectedRewardStage, bossStage);
    }

    private void AddAbilityId(string abilityId, bool applyAcquireEffects)
    {
        if (HasAbilityId(abilityId) || SavedAbilityCount >= MaxSavedAbilities)
        {
            return;
        }

        SavedAbilityIds.Set(SavedAbilityCount, abilityId);
        SavedAbilityCount++;

        PlayerSessionStore.SaveAbility(Owner, abilityId);
        PlayerAbilityInventory inventory = GetComponent<PlayerAbilityInventory>();
        inventory?.RestoreFromSessionData(Owner);

        if (!applyAcquireEffects || inventory == null)
        {
            return;
        }

        PlayerAbilityModule module = inventory.FindModuleById(abilityId);
        PlayerAbilityExecutor executor = GetComponent<PlayerAbilityExecutor>();
        if (module != null && executor != null)
        {
            executor.EquipModule(module, inventory.CreateContext());
        }
    }
}
