using UnityEngine;

public class HUDManager : MonoBehaviour
{
    [Header("Player HUD")]
    [SerializeField] private PlayerHUDView playerHUDView;

    [Header("Player Data")]
    [SerializeField] private PlayerStats playerStats;

    private void Start()
    {
        if (playerStats == null)
            playerStats = FindLocalPlayerStats();

        UpdatePlayerHUD();
    }

    private void Update()
    {
        if (playerStats == null)
        {
            playerStats = FindLocalPlayerStats();
            return;
        }

        UpdatePlayerHUD();
    }

    private void UpdatePlayerHUD()
    {
        if (playerHUDView == null || playerStats == null)
            return;

        playerHUDView.SetHp(playerStats.CurrentHealth, playerStats.MaxHealth);
        playerHUDView.SetSp(playerStats.CurrentStamina, playerStats.MaxStamina);
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
