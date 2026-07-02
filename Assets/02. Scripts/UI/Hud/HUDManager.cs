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
    [SerializeField] private PlayerAbilityInventory abilityInventory;
    [SerializeField] private PlayerStatusController playerStatusController;

    [Header("Boss Data")]
    [SerializeField] private NetworkBossCore boss;

    private NetworkPlayerController localPlayerController;
    private PlayerStats subscribedStaminaStats;
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
        BindLocalStaminaEvents();
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

        if (PlayerRegistry.TryGetLocalHUDReferences(
                out NetworkPlayerController localPlayer,
                out PlayerAbilityInventory localInventory,
                out PlayerStatusController localStatusController,
                out _))
        {
            if (localPlayerController == null)
                localPlayerController = localPlayer;

            if (abilityInventory == null)
                abilityInventory = localInventory;

            if (playerStatusController == null)
                playerStatusController = localStatusController;
        }

        if (!IsBossReadable(boss) || boss.CurrentState == BossState.Die)
            boss = FindActiveBoss();
    }

    // 🔥 [버그 픽스] 보스의 네트워크 변수를 읽어도 안전한지 검사.
    //    IsSpawnedReady만으로는 부족하다 — 씬 전환(로비 복귀)으로 '디스폰된 뒤'에도 파괴 전까지
    //    한두 프레임 Update가 돌면서 CurrentState 접근 시 InvalidOperationException이 터졌다.
    //    Object.IsValid로 디스폰 이후 상태를 걸러낸다.
    private static bool IsBossReadable(NetworkBossCore target)
    {
        return target != null &&
               target.IsSpawnedReady &&
               target.Object != null &&
               target.Object.IsValid;
    }

    private void TryFindRuntimeReferences()
    {
        if (localPlayerController != null &&
            abilityInventory != null &&
            playerStatusController != null &&
            IsBossReadable(boss) &&
            boss.CurrentState != BossState.Die)
        {
            return;
        }

        FindRuntimeReferences();
    }

    private IEnumerator BindRuntimeReferencesRoutine()
    {
        while (localPlayerController == null || abilityInventory == null)
        {
            FindRuntimeReferences();
            BindLocalStaminaEvents();
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
        if (playerHUDView == null ||
            !PlayerRegistry.TryGetHUDData(localPlayerController, out PlayerHUDData hudData))
        {
            return;
        }

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

        if (!IsBossReadable(boss) || boss.CurrentState == BossState.Die)
            boss = FindActiveBoss();

        if (!IsBossReadable(boss))
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

            if (!IsBossReadable(targetBoss))
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
        IReadOnlyList<NetworkPlayerController> players = PlayerRegistry.All;

        for (int i = 0; i < players.Count; i++)
        {
            NetworkPlayerController player = players[i];

            if (!PlayerRegistry.TryGetPartyMemberUIData(player, out PartyMemberUIData uiData))
                continue;

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

        if (!PlayerRegistry.TryGetAbilityInventory(player, out PlayerAbilityInventory inventory))
            return skills;

        if (PlayerRegistry.TryGetNetworkPlayerData(player, out NetworkPlayerData playerData))
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

    private void BindLocalStaminaEvents()
    {
        PlayerRegistry.TryGetLocalHUDReferences(
            out _,
            out _,
            out _,
            out PlayerStats stats);
        if (subscribedStaminaStats == stats)
        {
            return;
        }

        if (subscribedStaminaStats != null)
        {
            subscribedStaminaStats.StaminaUseFailed -= OnLocalStaminaUseFailed;
        }

        subscribedStaminaStats = stats;
        if (subscribedStaminaStats != null)
        {
            subscribedStaminaStats.StaminaUseFailed += OnLocalStaminaUseFailed;
        }
    }

    private void OnDisable()
    {
        if (subscribedStaminaStats != null)
        {
            subscribedStaminaStats.StaminaUseFailed -= OnLocalStaminaUseFailed;
            subscribedStaminaStats = null;
        }
    }

    private void OnLocalStaminaUseFailed(PlayerStats stats, float requiredStamina, float currentStamina)
    {
        playerHUDView?.ShowStaminaUseFailed();
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
