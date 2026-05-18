using UnityEngine;

public class HUDManager : MonoBehaviour
{
    [Header("Player HUD")]
    [SerializeField] private PlayerHUDView playerHUDView;

    [Header("Boss HUD")]
    [SerializeField] private BossHUDView bossHUDView;

    [Header("Player Data")]
    [SerializeField] private PlayerStats playerStats;

    [Header("Boss Data")]
    [SerializeField] private DragonBoss dragonBoss;

    private void Start()
    {
        if (playerStats == null)
            playerStats = FindLocalPlayerStats();

        if (dragonBoss == null)
            dragonBoss = FindObjectOfType<DragonBoss>();

        UpdateHUD();
    }

    private void Update()
    {
        if (playerStats == null)
            playerStats = FindLocalPlayerStats();

        if (dragonBoss == null)
            dragonBoss = FindObjectOfType<DragonBoss>();

        UpdateHUD();
    }

    private void UpdateHUD()
    {
        UpdatePlayerHUD();
        UpdateBossHUD();
    }

    private void UpdatePlayerHUD()
    {
        if (playerHUDView == null || playerStats == null)
            return;

        playerHUDView.SetHp(playerStats.CurrentHealth, playerStats.MaxHealth);
        playerHUDView.SetSp(playerStats.CurrentStamina, playerStats.MaxStamina);
    }

    private void UpdateBossHUD()
    {
        if (bossHUDView == null || dragonBoss == null)
            return;

        bossHUDView.SetHp(dragonBoss.CurrentHP, dragonBoss.maxHP);
    }

    private PlayerStats FindLocalPlayerStats()
    {
        NetworkPlayerController[] players = FindObjectsOfType<NetworkPlayerController>();

        for (int i = 0; i < players.Length; i++)
        {
            if (players[i] != null && players[i].Object != null && players[i].Object.HasInputAuthority)
                return players[i].GetComponent<PlayerStats>();
        }

        return null;
    }
}
