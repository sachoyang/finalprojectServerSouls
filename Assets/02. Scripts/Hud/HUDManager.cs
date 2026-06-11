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
    [SerializeField] private PlayerStatusController playerStatusController;

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

            if (playerStatusController == null)
                playerStatusController = localPlayerController.GetComponent<PlayerStatusController>();
        }

        if (boss == null || !boss.IsSpawnedReady || boss.CurrentState == BossState.Die)
            boss = FindActiveBoss();
    }

    private void TryFindRuntimeReferences()
    {
        if (localPlayerController != null &&
            playerStats != null &&
            abilityInventory != null &&
            playerStatusController != null &&
            boss != null &&
            boss.IsSpawnedReady &&
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
        if (playerHUDView == null || playerStats == null || !playerStats.IsSpawnedReady)
            return;

        PlayerHUDData hudData = playerStats.GetHUDData();

        playerHUDView.SetHp(hudData.CurrentHealth, hudData.MaxHealth);
        playerHUDView.SetSp(hudData.CurrentStamina, hudData.MaxStamina);

        if (playerStatusController != null)
            playerHUDView.SetStatuses(playerStatusController.GetActiveStatusesForUI());
        else
            playerHUDView.ClearStatuses();
    }

    private void UpdateBossHUD()
    {
        if (bossHUDView == null)
            return;

        if (boss == null || !boss.IsSpawnedReady || boss.CurrentState == BossState.Die)
            boss = FindActiveBoss();

        if (boss == null || !boss.IsSpawnedReady)
        {
            bossHUDView.ClearStatuses();
            bossHUDView.SetVisible(false);
            return;
        }

        float maxHp = boss.maxHP > 0f ? boss.maxHP : boss.baseMaxHP;
        float currentHp = boss.CurrentHP;

        if (currentHp <= 0f && boss.CurrentState != BossState.Die)
            currentHp = maxHp;

        bossHUDView.SetBossName(boss.bossName);
        bossHUDView.SetHp(currentHp, maxHp);
        bossHUDView.SetStatuses(boss.GetActiveStatusesForUI());
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

            if (!targetBoss.IsSpawnedReady)
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

        List<SkillSlotUIData> slots = abilityInventory.GetSkillSlotUIData(currentTime);

        for (int i = 0; i < skillSlotViews.Length; i++)
        {
            if (skillSlotViews[i] == null)
                continue;

            if (i >= slots.Count)
            {
                skillSlotViews[i].Clear();
                continue;
            }

            skillSlotViews[i].SetData(slots[i]);
        }
    }

    private void RefreshPartyHUD()
    {
        if (partyMemberHUDViews == null || partyMemberHUDViews.Length == 0)
            return;

        List<PartyMemberRuntimeData> partyMembers = FindPartyMemberRuntimeData();

        for (int i = 0; i < partyMemberHUDViews.Length; i++)
        {
            if (partyMemberHUDViews[i] == null)
                continue;

            if (i >= partyMembers.Count)
            {
                partyMemberHUDViews[i].ClearSkills();
                partyMemberHUDViews[i].SetVisible(false);
                continue;
            }

            partyMemberHUDViews[i].SetData(partyMembers[i].UIData);
            partyMemberHUDViews[i].SetSkills(partyMembers[i].Skills);
        }
    }

    private List<PartyMemberRuntimeData> FindPartyMemberRuntimeData()
    {
        List<PartyMemberRuntimeData> partyMembers = new List<PartyMemberRuntimeData>();
        NetworkPlayerController[] players = FindObjectsOfType<NetworkPlayerController>();

        for (int i = 0; i < players.Length; i++)
        {
            NetworkPlayerController player = players[i];

            if (player == null || player.Object == null)
                continue;

            if (player.Object.HasInputAuthority)
                continue;

            PlayerStats stats = player.GetComponent<PlayerStats>();
            if (stats == null || !stats.IsSpawnedReady)
                continue;

            PlayerHUDData hudData = stats.GetHUDData();
            int playerKey = player.Object.InputAuthority.RawEncoded;

            PartyMemberUIData uiData = new PartyMemberUIData(
                playerKey,
                $"Player {playerKey}",
                hudData.CurrentHealth,
                hudData.MaxHealth,
                hudData.CurrentStamina,
                hudData.MaxStamina,
                !hudData.IsDead,
                hudData.IsDead,
                false);

            List<PartyMemberSkillUIData> skills = BuildPartyMemberSkillUIData(player);

            partyMembers.Add(new PartyMemberRuntimeData(uiData, skills));
        }

        return partyMembers;
    }

    private List<PartyMemberSkillUIData> BuildPartyMemberSkillUIData(NetworkPlayerController player)
    {
        List<PartyMemberSkillUIData> skills = new List<PartyMemberSkillUIData>();

        if (player == null)
            return skills;

        PlayerAbilityInventory inventory = player.GetComponent<PlayerAbilityInventory>();
        NetworkPlayerData playerData = player.GetComponent<NetworkPlayerData>();

        if (inventory == null)
            return skills;

        if (playerData != null)
        {
            for (int i = 0; i < playerData.SavedAbilityCount; i++)
            {
                string abilityId = playerData.GetAbilityId(i);
                PlayerAbilityModule module = inventory.FindModuleById(abilityId);

                if (module == null || module.Icon == null)
                    continue;

                skills.Add(new PartyMemberSkillUIData(
                    module.DisplayName,
                    module.Icon,
                    module.IsActive));
            }

            return skills;
        }

        IReadOnlyList<PlayerAbilityModule> equippedModules = inventory.EquippedModules;

        for (int i = 0; i < equippedModules.Count; i++)
        {
            PlayerAbilityModule module = equippedModules[i];

            if (module == null || module.Icon == null)
                continue;

            skills.Add(new PartyMemberSkillUIData(
                module.DisplayName,
                module.Icon,
                module.IsActive));
        }

        return skills;
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
            playerHUDView.ClearStatuses();
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
            {
                partyMemberHUDViews[i].ClearSkills();
                partyMemberHUDViews[i].SetVisible(false);
            }
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

    private readonly struct PartyMemberRuntimeData
    {
        public readonly PartyMemberUIData UIData;
        public readonly List<PartyMemberSkillUIData> Skills;

        public PartyMemberRuntimeData(PartyMemberUIData uiData, List<PartyMemberSkillUIData> skills)
        {
            UIData = uiData;
            Skills = skills;
        }
    }
}