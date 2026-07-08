using System.Collections.Generic;
using Fusion;

public static class PlayerSessionStore
{
    public readonly struct AbilityState
    {
        public readonly string AbilityId;
        public readonly int Level;

        public AbilityState(string abilityId, int level)
        {
            AbilityId = abilityId;
            Level = level;
        }
    }

    private static readonly Dictionary<int, List<AbilityState>> AbilitiesByPlayer =
        new Dictionary<int, List<AbilityState>>();
    private static readonly Dictionary<int, int> SelectedRewardStageByPlayer = new Dictionary<int, int>();
    private static readonly Dictionary<int, PlayerStats.SessionSnapshot> StatsByPlayer = new Dictionary<int, PlayerStats.SessionSnapshot>();

    public static void SaveAbility(PlayerRef player, string abilityId)
    {
        SetAbilityLevel(player, abilityId, 1);
    }

    public static void SetAbilityLevel(PlayerRef player, string abilityId, int level)
    {
        if (string.IsNullOrWhiteSpace(abilityId))
        {
            return;
        }

        int key = GetKey(player);
        if (!AbilitiesByPlayer.TryGetValue(key, out List<AbilityState> abilities))
        {
            abilities = new List<AbilityState>();
            AbilitiesByPlayer[key] = abilities;
        }

        int clampedLevel = UnityEngine.Mathf.Clamp(level, 1, byte.MaxValue);
        for (int i = 0; i < abilities.Count; i++)
        {
            if (abilities[i].AbilityId == abilityId)
            {
                abilities[i] = new AbilityState(abilityId, clampedLevel);
                return;
            }
        }

        abilities.Add(new AbilityState(abilityId, clampedLevel));
    }

    public static IReadOnlyList<AbilityState> GetAbilities(PlayerRef player)
    {
        return AbilitiesByPlayer.TryGetValue(GetKey(player), out List<AbilityState> abilities)
            ? abilities
            : System.Array.Empty<AbilityState>();
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

    public static void SaveActivePlayerStats(NetworkRunner runner)
    {
        if (runner == null)
        {
            return;
        }

        foreach (PlayerRef player in runner.ActivePlayers)
        {
            if (!runner.TryGetPlayerObject(player, out NetworkObject playerObject) || playerObject == null)
            {
                continue;
            }

            PlayerStats stats = playerObject.GetComponent<PlayerStats>();
            if (stats == null || !stats.IsSpawnedReady)
            {
                continue;
            }

            // 씬 이동 직전의 서버 확정 체력/스태미나를 저장해야 다음 씬 스폰 때 이전 값으로 되돌아가지 않는다.
            SaveStats(player, stats.CreateSessionSnapshot());
        }
    }

    public static bool TryGetStats(PlayerRef player, out PlayerStats.SessionSnapshot snapshot)
    {
        return StatsByPlayer.TryGetValue(GetKey(player), out snapshot);
    }

    public static void ClearAll()
    {
        AbilitiesByPlayer.Clear();
        SelectedRewardStageByPlayer.Clear();
        StatsByPlayer.Clear();
    }

    private static int GetKey(PlayerRef player)
    {
        return player.RawEncoded;
    }
}
