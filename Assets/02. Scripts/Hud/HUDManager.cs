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

    private void Awake()
    {
        // HUD 프리팹을 씬에 올려놓기만 해도 자식 View들을 자동으로 연결합니다.
        FindHUDViews();
    }

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
        FindHUDViews();

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

    private void FindHUDViews()
    {
        // 인스펙터에 직접 연결되어 있으면 그 값을 유지하고, 비어 있을 때만 자식에서 찾습니다.
        if (playerHUDView == null)
            playerHUDView = GetComponentInChildren<PlayerHUDView>(true);

        if (bossHUDView == null)
            bossHUDView = GetComponentInChildren<BossHUDView>(true);

        if (skillSlotViews == null || skillSlotViews.Length == 0)
            skillSlotViews = GetComponentsInChildren<SkillSlotHUDView>(true);
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
        // DragonBoss가 Fusion Spawn을 끝내기 전에는 Networked HP를 읽으면 예외가 발생합니다.
        if (bossHUDView == null || dragonBoss == null || !dragonBoss.IsSpawnedReady)
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
        if (skillSlotViews == null)
            return;

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
