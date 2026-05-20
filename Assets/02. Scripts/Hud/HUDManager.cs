using UnityEngine;

public class HUDManager : MonoBehaviour
{
    [Header("Player HUD")]
    [SerializeField] private PlayerHUDView playerHUDView;

    [Header("Boss HUD")]
    [SerializeField] private BossHUDView bossHUDView;

    [Header("Skill HUD")]
    [SerializeField] private SkillSlotHUDView[] skillSlotViews;

    [Header("Player Data")]
    [SerializeField] private PlayerStats playerStats;
    [SerializeField] private PlayerAbilityInventory abilityInventory;

    [Header("Boss Data")]
    [SerializeField] private DragonBoss dragonBoss;

    private NetworkPlayerController localPlayerController;

    private void Start()
    {
        FindRuntimeReferences();
        UpdateHUD();
    }

    private void Update()
    {
        FindRuntimeReferences();
        UpdateHUD();
    }

    private void FindRuntimeReferences()
    {
        if (localPlayerController == null)
            localPlayerController = FindLocalPlayerController();

        if (localPlayerController != null)
        {
            if (playerStats == null)
                playerStats = localPlayerController.GetComponent<PlayerStats>();

            if (abilityInventory == null)
                abilityInventory = localPlayerController.GetComponent<PlayerAbilityInventory>();
        }

        if (dragonBoss == null)
            dragonBoss = FindObjectOfType<DragonBoss>();
    }

    private void UpdateHUD()
    {
        UpdatePlayerHUD();
        UpdateBossHUD();
        UpdateSkillHUD();
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

    private void UpdateSkillHUD()
    {
        if (skillSlotViews == null || skillSlotViews.Length == 0)
            return;

        if (abilityInventory == null)
        {
            ClearSkillSlots();
            return;
        }

        float currentTime = localPlayerController != null && localPlayerController.Runner != null
            ? localPlayerController.Runner.SimulationTime
            : Time.time;

        for (int i = 0; i < skillSlotViews.Length; i++)
        {
            if (skillSlotViews[i] == null)
                continue;

            if (i >= abilityInventory.ActiveSlots.Count)
            {
                skillSlotViews[i].Clear();
                continue;
            }

            PlayerAbilitySlot slot = abilityInventory.ActiveSlots[i];

            if (slot == null || slot.Module == null)
            {
                skillSlotViews[i].Clear();
                continue;
            }

            float remainingCooldown = Mathf.Max(0f, slot.NextReadyTime - currentTime);
            skillSlotViews[i].SetSlot(slot.Module, slot.KeyCode, remainingCooldown);
        }
    }

    private void ClearSkillSlots()
    {
        for (int i = 0; i < skillSlotViews.Length; i++)
        {
            if (skillSlotViews[i] != null)
                skillSlotViews[i].Clear();
        }
    }

    private NetworkPlayerController FindLocalPlayerController()
    {
        NetworkPlayerController[] players = FindObjectsOfType<NetworkPlayerController>();

        for (int i = 0; i < players.Length; i++)
        {
            if (players[i] != null && players[i].Object != null && players[i].Object.HasInputAuthority)
                return players[i];
        }

        return null;
    }
}