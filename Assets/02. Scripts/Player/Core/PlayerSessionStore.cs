using System.Collections.Generic;
using Fusion;

public static class PlayerSessionStore
{
    private static readonly Dictionary<int, List<string>> AbilityIdsByPlayer = new Dictionary<int, List<string>>();
    private static readonly Dictionary<int, int> SelectedRewardStageByPlayer = new Dictionary<int, int>();
    private static readonly Dictionary<int, PlayerStats.SessionSnapshot> StatsByPlayer = new Dictionary<int, PlayerStats.SessionSnapshot>();

    public static void SaveAbility(PlayerRef player, string abilityId)
    {
        if (string.IsNullOrWhiteSpace(abilityId))
        {
            return;
        }

        int key = GetKey(player);
        if (!AbilityIdsByPlayer.TryGetValue(key, out List<string> abilityIds))
        {
            abilityIds = new List<string>();
            AbilityIdsByPlayer[key] = abilityIds;
        }

        if (!abilityIds.Contains(abilityId))
        {
            abilityIds.Add(abilityId);
        }
    }

    public static IReadOnlyList<string> GetAbilityIds(PlayerRef player)
    {
        return AbilityIdsByPlayer.TryGetValue(GetKey(player), out List<string> abilityIds)
            ? abilityIds
            : System.Array.Empty<string>();
    }

    public static void MarkRewardSelected(PlayerRef player, int bossStage)
    {
        if (bossStage <= 0)
        {
            return;
        }

        int key = GetKey(player);
        int currentStage = SelectedRewardStageByPlayer.TryGetValue(key, out int savedStage) ? savedStage : 0;
        if (bossStage > currentStage)
        {
            SelectedRewardStageByPlayer[key] = bossStage;
        }
    }

    public static bool HasSelectedReward(PlayerRef player, int bossStage)
    {
        return SelectedRewardStageByPlayer.TryGetValue(GetKey(player), out int savedStage) &&
               savedStage >= bossStage;
    }

    public static void SaveStats(PlayerRef player, PlayerStats.SessionSnapshot snapshot)
    {
        StatsByPlayer[GetKey(player)] = snapshot;
    }

    public static bool TryGetStats(PlayerRef player, out PlayerStats.SessionSnapshot snapshot)
    {
        return StatsByPlayer.TryGetValue(GetKey(player), out snapshot);
    }

    private static int GetKey(PlayerRef player)
    {
        return player.RawEncoded;
    }
}
