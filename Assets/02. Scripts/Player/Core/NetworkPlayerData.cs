using System.Collections.Generic;
using Fusion;
using UnityEngine;

[DisallowMultipleComponent]
public class NetworkPlayerData : NetworkBehaviour
{
    public const int MaxSavedAbilities = 16;
    private const string CurrentNicknameKey = "CurrentNickname";

    [Networked] public PlayerRef Owner { get; private set; }
    [Networked] public NetworkString<_32> Nickname { get; private set; }
    [Networked] public long UnlockedSkillsBitmask { get; private set; }
    [Networked] public int SavedAbilityCount { get; private set; }
    [Networked] public int LastSelectedRewardStage { get; private set; }
    [Networked, Capacity(MaxSavedAbilities)] public NetworkArray<NetworkString<_64>> SavedAbilityIds => default;
    [Networked, Capacity(MaxSavedAbilities)] public NetworkArray<byte> SavedAbilityLevels => default;

    public string DisplayNickname
    {
        get
        {
            string nickname = Nickname.ToString();
            return string.IsNullOrWhiteSpace(nickname)
                ? $"Player {Object.InputAuthority.RawEncoded}"
                : nickname;
        }
    }

    public override void Spawned()
    {
        if (HasStateAuthority)
        {
            Owner = Object.InputAuthority;
            RestoreNetworkAbilitiesFromSession(Owner);
        }

        SyncUnlockedSkillsBitmask();
        SyncNickname();
        GetComponent<PlayerAbilityInventory>()?.RestoreFromSessionData(Object.InputAuthority);
    }

    private void SyncNickname()
    {
        if (Object == null || !Object.HasInputAuthority)
        {
            return;
        }

        string nickname = ResolveLocalNickname();
        if (HasStateAuthority)
        {
            Nickname = nickname;
            return;
        }

        RPC_SetNickname(nickname);
    }

    private static string ResolveLocalNickname()
    {
        if (BackendManager.HasInstance && !string.IsNullOrWhiteSpace(BackendManager.Instance.CurrentNickname))
        {
            return BackendManager.Instance.CurrentNickname;
        }

        string savedNickname = PlayerPrefs.GetString(CurrentNicknameKey, string.Empty);
        return string.IsNullOrWhiteSpace(savedNickname)
            ? string.Empty
            : savedNickname;
    }

    private void RestoreNetworkAbilitiesFromSession(PlayerRef owner)
    {
        IReadOnlyList<PlayerSessionStore.AbilityState> abilities = PlayerSessionStore.GetAbilities(owner);
        int count = Mathf.Min(abilities.Count, MaxSavedAbilities);
        for (int i = 0; i < count; i++)
        {
            SavedAbilityIds.Set(i, abilities[i].AbilityId);
            SavedAbilityLevels.Set(i, (byte)Mathf.Clamp(
                abilities[i].Level,
                1,
                GetAbilityMaxLevel(abilities[i].AbilityId)));
        }

        SavedAbilityCount = count;
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

    public void RecordAbility(PlayerAbilityModule module, int level)
    {
        if (module == null)
        {
            return;
        }

        RecordAbilityId(module.AbilityId, level);
    }

    public void RecordAbilityId(string abilityId, int localLevel)
    {
        if (string.IsNullOrWhiteSpace(abilityId))
        {
            return;
        }

        if (HasStateAuthority)
        {
            // 방장 플레이어는 보상 선택 시 같은 오브젝트에서 이미 획득 효과를 적용했다.
            AddOrLevelAbility(abilityId, false);
            return;
        }

        if (Object != null && Object.HasInputAuthority)
        {
            PlayerSessionStore.SetAbilityLevel(Object.InputAuthority, abilityId, localLevel);
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

    public int GetAbilityLevel(int index)
    {
        if (index < 0 || index >= SavedAbilityCount || index >= MaxSavedAbilities)
        {
            return 0;
        }

        return Mathf.Clamp(
            SavedAbilityLevels[index],
            1,
            GetAbilityMaxLevel(SavedAbilityIds[index].ToString()));
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
        // 요청자가 임의의 레벨을 보내지 못하도록 서버의 현재 레벨에서 정확히 1만 올린다.
        AddOrLevelAbility(abilityId, true);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_SetUnlockedSkillsBitmask(long unlockedSkillsBitmask)
    {
        UnlockedSkillsBitmask = unlockedSkillsBitmask;
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_SetNickname(string nickname)
    {
        Nickname = string.IsNullOrWhiteSpace(nickname)
            ? string.Empty
            : nickname.Trim();
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_MarkRewardSelected(int bossStage)
    {
        PlayerSessionStore.MarkRewardSelected(Owner, bossStage);
        LastSelectedRewardStage = Mathf.Max(LastSelectedRewardStage, bossStage);
    }

    private void AddOrLevelAbility(string abilityId, bool applyAcquireEffects)
    {
        PlayerAbilityInventory inventory = GetComponent<PlayerAbilityInventory>();
        PlayerAbilityModule module = inventory != null ? inventory.FindModuleById(abilityId) : null;
        if (module == null)
        {
            return;
        }

        int index = FindAbilityIndex(abilityId);
        if (index < 0 && SavedAbilityCount >= MaxSavedAbilities)
        {
            return;
        }

        int currentLevel = index >= 0 ? SavedAbilityLevels[index] : 0;
        if (currentLevel >= module.MaxLevel)
        {
            return;
        }

        int newLevel = currentLevel + 1;
        if (index < 0)
        {
            index = SavedAbilityCount;
            SavedAbilityIds.Set(index, abilityId);
            SavedAbilityCount++;
        }

        SavedAbilityLevels.Set(index, (byte)newLevel);
        PlayerSessionStore.SetAbilityLevel(Owner, abilityId, newLevel);

        if (!applyAcquireEffects || inventory == null)
        {
            return;
        }

        inventory.ApplyServerReward(module);
    }

    private int FindAbilityIndex(string abilityId)
    {
        if (string.IsNullOrWhiteSpace(abilityId))
        {
            return -1;
        }

        for (int i = 0; i < SavedAbilityCount && i < MaxSavedAbilities; i++)
        {
            if (SavedAbilityIds[i].ToString() == abilityId)
            {
                return i;
            }
        }

        return -1;
    }

    private int GetAbilityMaxLevel(string abilityId)
    {
        PlayerAbilityInventory inventory = GetComponent<PlayerAbilityInventory>();
        PlayerAbilityModule module = inventory != null ? inventory.FindModuleById(abilityId) : null;
        return module != null ? module.MaxLevel : byte.MaxValue;
    }
}
