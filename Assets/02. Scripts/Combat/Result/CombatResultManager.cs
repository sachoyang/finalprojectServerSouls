using Fusion;
using System.Collections.Generic;
using UnityEngine;

public enum CombatResultType
{
    None,
    Victory,
    Defeat,
    Retreat
}

public class CombatResultManager : MonoBehaviour
{
    public static CombatResultManager Instance { get; private set; }

    [Header("Boss")]
    [SerializeField] private NetworkBossCore boss;

    [Header("Result Handlers")]
    [SerializeField] private RewardManager rewardManager;
    [SerializeField] private GameOverView gameOverView;

    [Header("Defeat")]
    [SerializeField] private float allPlayersDeadDelay = 2f;

    private CombatResultType currentResult = CombatResultType.None;
    private float allPlayersDeadTimer;
    private bool _combatResultAbsorbed;
    private readonly Dictionary<int, float> _bossDamageByPlayer = new Dictionary<int, float>();

    public bool HasResult => currentResult != CombatResultType.None;

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        if (currentResult != CombatResultType.None)
            return;

        ResolveReferences();
        CheckVictory();
        CheckDefeat();
    }

    public void RequestDefeat()
    {
        ResolveReferences();
        CompleteCombat(CombatResultType.Defeat);
    }

    public void RequestRetreat()
    {
        ResolveReferences();
        CompleteCombat(CombatResultType.Retreat);
    }

    public void RecordBossDamage(NetworkObject attacker, float appliedDamage)
    {
        if (attacker == null || appliedDamage <= 0f)
            return;

        int key = attacker.InputAuthority.RawEncoded;
        _bossDamageByPlayer.TryGetValue(key, out float savedDamage);
        _bossDamageByPlayer[key] = savedDamage + appliedDamage;
    }

    private void CheckVictory()
    {
        if (boss == null || !boss.IsSpawnedReady)
            return;

        if (boss.CurrentState == BossState.Die ||
            (boss.CurrentHP <= 0f && boss.CurrentState != BossState.Sleep))
        {
            CompleteCombat(CombatResultType.Victory);
        }
    }

    private void CheckDefeat()
    {
        NetworkRunner runner = GetRunner();
        int playerCount = 0;
        int aliveCount = 0;

        if (runner != null)
        {
            foreach (PlayerRef player in runner.ActivePlayers)
            {
                if (!runner.TryGetPlayerObject(player, out NetworkObject playerObject) ||
                    !PlayerRegistry.TryGetStats(playerObject, out PlayerStats stats) ||
                    !stats.IsSpawnedReady)
                {
                    continue;
                }

                playerCount++;
                if (!stats.IsDead)
                    aliveCount++;
            }
        }
        else
        {
            IReadOnlyList<NetworkPlayerController> players = PlayerRegistry.All;
            for (int i = 0; i < players.Count; i++)
            {
                NetworkPlayerController player = players[i];
                if (player == null ||
                    !PlayerRegistry.TryGetStats(player.Object, out PlayerStats stats) ||
                    !stats.IsSpawnedReady)
                {
                    continue;
                }

                playerCount++;
                if (!stats.IsDead)
                    aliveCount++;
            }
        }


        if (playerCount <= 0)
            return;

        if (aliveCount > 0)
        {
            allPlayersDeadTimer = 0f;
            return;
        }

        allPlayersDeadTimer += Time.deltaTime;

        if (allPlayersDeadTimer >= allPlayersDeadDelay)
        {
            CompleteCombat(CombatResultType.Defeat);
        }
    }

    private void CompleteCombat(CombatResultType result)
    {
        if (currentResult != CombatResultType.None)
            return;

        currentResult = result;
        if (result == CombatResultType.Victory)
        {
            AbsorbStageResult();
        }

        switch (result)
        {
            case CombatResultType.Victory:
                if (rewardManager != null)
                    rewardManager.BeginReward();
                break;

            case CombatResultType.Defeat:
                if (gameOverView != null)
                    gameOverView.PlayDefeat();
                break;

            case CombatResultType.Retreat:
                if (gameOverView != null)
                    gameOverView.PlayRetreat();
                break;
        }
    }

    private void ResolveReferences()
    {
        if (boss == null)
            boss = FindObjectOfType<NetworkBossCore>();

        if (rewardManager == null)
            rewardManager = FindObjectOfType<RewardManager>();

        if (gameOverView == null)
            gameOverView = FindObjectOfType<GameOverView>(true);
    }

    private void AbsorbStageResult()
    {
        if (_combatResultAbsorbed || GameProgressionManager.Instance == null)
            return;

        _combatResultAbsorbed = true;
        GameProgressionManager.Instance.AbsorbCombatResult(BuildStageResults());
    }

    private List<GameProgressionManager.StagePlayerCombatResult> BuildStageResults()
    {
        List<GameProgressionManager.StagePlayerCombatResult> results =
            new List<GameProgressionManager.StagePlayerCombatResult>();
        NetworkRunner runner = GetRunner();

        if (runner != null)
        {
            foreach (PlayerRef player in runner.ActivePlayers)
            {
                if (!runner.TryGetPlayerObject(player, out NetworkObject playerObject))
                    continue;

                AddPlayerResult(results, player, playerObject);
            }
        }
        else
        {
            IReadOnlyList<NetworkPlayerController> players = PlayerRegistry.All;
            for (int i = 0; i < players.Count; i++)
            {
                NetworkPlayerController player = players[i];
                if (player == null || player.Object == null)
                    continue;

                AddPlayerResult(results, player.Object.InputAuthority, player.Object);
            }
        }

        return results;
    }

    private void AddPlayerResult(
        List<GameProgressionManager.StagePlayerCombatResult> results,
        PlayerRef player,
        NetworkObject playerObject)
    {
        int key = player.RawEncoded;
        _bossDamageByPlayer.TryGetValue(key, out float bossDamage);
        int deathCount = 0;

        if (PlayerRegistry.TryGetStats(playerObject, out PlayerStats stats) &&
            stats.IsSpawnedReady)
        {
            deathCount = stats.DeathCount;
        }

        results.Add(new GameProgressionManager.StagePlayerCombatResult(
            player,
            ResolvePlayerNickname(player),
            Mathf.FloorToInt(Mathf.Max(0f, bossDamage)),
            Mathf.Max(0, deathCount)));
    }

    private static string ResolvePlayerNickname(PlayerRef player)
    {
        return $"Player {player.RawEncoded}";
    }

    private NetworkRunner GetRunner()
    {
        if (boss != null && boss.Runner != null)
            return boss.Runner;

        return NetworkManager.HasInstance ? NetworkManager.Instance.Runner : null;
    }
}
