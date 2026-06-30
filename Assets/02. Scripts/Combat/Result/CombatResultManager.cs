using Fusion;
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
    [Header("Boss")]
    [SerializeField] private NetworkBossCore boss;

    [Header("Result Handlers")]
    [SerializeField] private RewardManager rewardManager;
    [SerializeField] private GameOverView gameOverView;

    [Header("Defeat")]
    [SerializeField] private float allPlayersDeadDelay = 2f;

    private CombatResultType currentResult = CombatResultType.None;
    private float allPlayersDeadTimer;

    private void Update()
    {
        if (currentResult != CombatResultType.None)
            return;

        ResolveReferences();
        CheckVictory();
        CheckDefeat();
    }

    public void RequestRetreat()
    {
        CompleteCombat(CombatResultType.Retreat);
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
        PlayerStats[] players = FindObjectsOfType<PlayerStats>();
        int playerCount = 0;
        int aliveCount = 0;

        for (int i = 0; i < players.Length; i++)
        {
            PlayerStats stats = players[i];

            if (stats == null || !stats.IsSpawnedReady)
                continue;

            playerCount++;

            if (!stats.IsDead)
                aliveCount++;
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
}