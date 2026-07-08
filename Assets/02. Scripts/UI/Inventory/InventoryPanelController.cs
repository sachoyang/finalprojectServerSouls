using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InventoryPanelController : MonoBehaviour
{
    [Header("Canvas")]
    [SerializeField] private CanvasGroup inventoryCanvasGroup;

    [Header("Card List")]
    [SerializeField] private Transform cardSlotParent;
    [SerializeField] private InventoryCardSlotView cardSlotPrefab;
    [SerializeField] private GameObject passiveGroupSpacingPrefab;
    [SerializeField] private GameObject emptySkillTextObject;

    [Header("Tooltip")]
    [SerializeField] private InventoryTooltipView tooltipView;

    [Header("Player Data")]
    [SerializeField] private PlayerAbilityInventory abilityInventory;
    [SerializeField] private float referenceSearchInterval = 0.25f;

    private readonly List<GameObject> spawnedObjects = new List<GameObject>();
    private NetworkPlayerController localPlayerController;
    private bool isRewardSelectOpen;
    private bool wasVisible;
    private int lastEquippedModuleCount = -1;
    private Coroutine bindCoroutine;

    private void OnDestroy()
    {
        if (abilityInventory != null)
            abilityInventory.AbilityLevelChanged -= OnAbilityLevelChanged;
    }

    private void Start()
    {
        SetInventoryVisible(false);
        SetEmptyTextVisible(false);

        if (tooltipView != null)
            tooltipView.Hide();

        bindCoroutine = StartCoroutine(BindLocalInventoryRoutine());
    }

    private void Update()
    {
        if (abilityInventory == null)
            return;

        RefreshIfNeeded();

        bool shouldShow = IsInventoryHoldKeyPressed() || isRewardSelectOpen;

        SetInventoryVisible(shouldShow);
    }

    public void SetRewardSelectOpen(bool isOpen)
    {
        isRewardSelectOpen = isOpen;
        SetInventoryVisible(IsInventoryHoldKeyPressed() || isRewardSelectOpen);
    }

    public void RefreshCardList()
    {
        ClearSpawnedObjects();

        if (abilityInventory == null || cardSlotPrefab == null || cardSlotParent == null)
        {
            SetEmptyTextVisible(true);
            return;
        }

        IReadOnlyList<PlayerAbilityModule> equippedModules = abilityInventory.EquippedModules;
        lastEquippedModuleCount = equippedModules.Count;

        List<PlayerAbilityModule> activeModules = new List<PlayerAbilityModule>();
        List<PlayerAbilityModule> passiveModules = new List<PlayerAbilityModule>();
        List<PlayerAbilityModule> utilityModules = new List<PlayerAbilityModule>();

        for (int i = 0; i < equippedModules.Count; i++)
        {
            PlayerAbilityModule module = equippedModules[i];

            if (module == null)
                continue;

            if (module.IsActive)
                activeModules.Add(module);
            else if (module.IsPassive)
                passiveModules.Add(module);
            else
                utilityModules.Add(module);
        }

        bool hasAnySkill =
            activeModules.Count > 0 ||
            passiveModules.Count > 0 ||
            utilityModules.Count > 0;
        SetEmptyTextVisible(!hasAnySkill);

        if (!hasAnySkill)
            return;

        CreateSlots(activeModules);

        if (activeModules.Count > 0 && (passiveModules.Count > 0 || utilityModules.Count > 0))
            CreatePassiveGroupSpacing();

        CreateSlots(passiveModules);

        if (passiveModules.Count > 0 && utilityModules.Count > 0)
            CreatePassiveGroupSpacing();

        CreateSlots(utilityModules);
    }

    private bool IsInventoryHoldKeyPressed()
    {
        return Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
    }

    private void SetInventoryVisible(bool isVisible)
    {
        if (inventoryCanvasGroup == null)
            return;

        inventoryCanvasGroup.alpha = isVisible ? 1f : 0f;
        inventoryCanvasGroup.interactable = isVisible;
        inventoryCanvasGroup.blocksRaycasts = isVisible;

        if (wasVisible && !isVisible && tooltipView != null)
            tooltipView.Hide();

        wasVisible = isVisible;
    }

    private void CreateSlots(List<PlayerAbilityModule> modules)
    {
        for (int i = 0; i < modules.Count; i++)
        {
            InventoryCardSlotView slot = Instantiate(cardSlotPrefab, cardSlotParent);
            slot.SetModule(modules[i], abilityInventory.GetAbilityLevel(modules[i]), tooltipView);
            spawnedObjects.Add(slot.gameObject);
        }
    }

    private void CreatePassiveGroupSpacing()
    {
        if (passiveGroupSpacingPrefab == null)
            return;

        GameObject spacing = Instantiate(passiveGroupSpacingPrefab, cardSlotParent);
        spawnedObjects.Add(spacing);
    }

    private void RefreshIfNeeded()
    {
        if (abilityInventory == null)
            return;

        if (abilityInventory.EquippedModules.Count != lastEquippedModuleCount)
            RefreshCardList();
    }

    private void FindRuntimeReferences()
    {
        if (abilityInventory != null)
            return;

        if (PlayerRegistry.TryGetLocalHUDReferences(
                out NetworkPlayerController localPlayer,
                out PlayerAbilityInventory localInventory,
                out _,
                out _))
        {
            localPlayerController = localPlayer;
            abilityInventory = localInventory;
        }
    }

    private IEnumerator BindLocalInventoryRoutine()
    {
        while (abilityInventory == null)
        {
            FindRuntimeReferences();
            yield return new WaitForSeconds(referenceSearchInterval);
        }

        abilityInventory.AbilityLevelChanged -= OnAbilityLevelChanged;
        abilityInventory.AbilityLevelChanged += OnAbilityLevelChanged;
        RefreshCardList();
        bindCoroutine = null;
    }

    private void OnAbilityLevelChanged(PlayerAbilityModule module, int level)
    {
        RefreshCardList();
    }

    private void SetEmptyTextVisible(bool isVisible)
    {
        if (emptySkillTextObject != null)
            emptySkillTextObject.SetActive(isVisible);
    }

    private void ClearSpawnedObjects()
    {
        for (int i = 0; i < spawnedObjects.Count; i++)
        {
            if (spawnedObjects[i] != null)
                Destroy(spawnedObjects[i]);
        }

        spawnedObjects.Clear();
    }
}
