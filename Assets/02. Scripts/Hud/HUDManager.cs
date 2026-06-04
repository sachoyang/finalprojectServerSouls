using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HUDManager : MonoBehaviour
{
    [Header("Player HUD")]
    [SerializeField] private PlayerHUDView playerHUDView;

    [Header("Boss HUD")]
    [SerializeField] private BossHUDView bossHUDView;

    [Header("Skill HUD")]
    [SerializeField] private SkillSlotHUDView[] skillSlotViews;

    [Header("Party HUD")]
    [SerializeField] private PartyMemberHUDView[] partyMemberHUDViews;
    [SerializeField] private float partyRefreshInterval = 0.5f;
    [SerializeField] private float referenceSearchInterval = 0.25f;

    [Header("Player Data")]
    [SerializeField] private PlayerStats playerStats;
    [SerializeField] private PlayerAbilityInventory abilityInventory;

    [Header("Boss Data")]
    [SerializeField] private NetworkBossCore boss;

    private NetworkPlayerController localPlayerController;
    private float nextPartyRefreshTime;
    private float nextReferenceSearchTime;
    private Coroutine bindCoroutine;

    private void Start()
    {
        ClearHUD();
        bindCoroutine = StartCoroutine(BindRuntimeReferencesRoutine());
    }

    private void Update()
    {
        TryFindRuntimeReferences();
        UpdateHUD();

        if (Time.time >= nextPartyRefreshTime)
        {
            nextPartyRefreshTime = Time.time + partyRefreshInterval;
            RefreshPartyHUD();
        }
    }

    private void FindRuntimeReferences()
    {
        if (Time.time < nextReferenceSearchTime)
            return;

        nextReferenceSearchTime = Time.time + referenceSearchInterval;

        if (localPlayerController == null)
            localPlayerController = FindLocalPlayerController();

        if (localPlayerController != null)
        {
            if (playerStats == null)
                playerStats = localPlayerController.GetComponent<PlayerStats>();

            if (abilityInventory == null)
                abilityInventory = localPlayerController.GetComponent<PlayerAbilityInventory>();
        }

        if (boss == null || boss.CurrentState == BossState.Die)
            boss = FindActiveBoss();
    }

    private void TryFindRuntimeReferences()
    {
        if (localPlayerController != null &&
            playerStats != null &&
            abilityInventory != null &&
            boss != null &&
            boss.CurrentState != BossState.Die)
        {
            return;
        }

        FindRuntimeReferences();
    }

    private IEnumerator BindRuntimeReferencesRoutine()
    {
        while (localPlayerController == null || playerStats == null || abilityInventory == null)
        {
            FindRuntimeReferences();
            yield return new WaitForSeconds(referenceSearchInterval);
        }

        UpdateHUD();
        RefreshPartyHUD();
        bindCoroutine = null;
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
        if (bossHUDView == null)
            return;

        if (boss == null || boss.CurrentState == BossState.Die)
            boss = FindActiveBoss();

        if (boss == null)
        {
            bossHUDView.SetVisible(false);
            return;
        }

        float maxHp = boss.maxHP > 0f ? boss.maxHP : boss.baseMaxHP;
        float currentHp = boss.CurrentHP;

        if (currentHp <= 0f && boss.CurrentState != BossState.Die)
            currentHp = maxHp;

        bossHUDView.SetBossName(boss.bossName);
        bossHUDView.SetHp(currentHp, maxHp);
        bossHUDView.SetVisible(boss.CurrentState != BossState.Die);
    }

    private NetworkBossCore FindActiveBoss()
    {
        NetworkBossCore[] bosses = FindObjectsOfType<NetworkBossCore>();

        for (int i = 0; i < bosses.Length; i++)
        {
            NetworkBossCore targetBoss = bosses[i];

            if (targetBoss == null)
                continue;

            if (!targetBoss.gameObject.activeInHierarchy)
                continue;

            if (targetBoss.CurrentState == BossState.Die)
                continue;

            return targetBoss;
        }

        return null;
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

    private void RefreshPartyHUD()
    {
        if (partyMemberHUDViews == null || partyMemberHUDViews.Length == 0)
            return;

        List<PlayerStats> partyStats = FindPartyPlayerStats();

        for (int i = 0; i < partyMemberHUDViews.Length; i++)
        {
            if (partyMemberHUDViews[i] == null)
                continue;

            if (i >= partyStats.Count || partyStats[i] == null)
            {
                partyMemberHUDViews[i].SetVisible(false);
                continue;
            }

            partyMemberHUDViews[i].SetVisible(true);
            partyMemberHUDViews[i].SetStats(
                partyStats[i].CurrentHealth,
                partyStats[i].MaxHealth,
                partyStats[i].CurrentStamina,
                partyStats[i].MaxStamina
            );
        }
    }

    private List<PlayerStats> FindPartyPlayerStats()
    {
        List<PlayerStats> partyStats = new List<PlayerStats>();
        NetworkPlayerController[] players = FindObjectsOfType<NetworkPlayerController>();

        for (int i = 0; i < players.Length; i++)
        {
            NetworkPlayerController player = players[i];

            if (player == null || player.Object == null)
                continue;

            if (player.Object.HasInputAuthority)
                continue;

            PlayerStats stats = player.GetComponent<PlayerStats>();

            if (stats != null)
                partyStats.Add(stats);
        }

        return partyStats;
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

    private void ClearHUD()
    {
        if (playerHUDView != null)
        {
            playerHUDView.SetHp(0f, 1f);
            playerHUDView.SetSp(0f, 1f);
        }

        if (bossHUDView != null)
        {
            bossHUDView.Clear();
            bossHUDView.SetVisible(false);
        }

        ClearSkillSlots();

        if (partyMemberHUDViews == null)
            return;

        for (int i = 0; i < partyMemberHUDViews.Length; i++)
        {
            if (partyMemberHUDViews[i] != null)
                partyMemberHUDViews[i].SetVisible(false);
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