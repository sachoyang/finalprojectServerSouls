using System;
using System.Collections;
using System.Collections.Generic;
using Fusion;
using UnityEngine;
using UnityEngine.UI;

public class RewardSelectView : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private GameObject rootObject;

    [Header("Card List")]
    [SerializeField] private Transform cardContentParent;
    [SerializeField] private RewardCardView rewardCardPrefab;

    [Header("Confirm")]
    [SerializeField] private Button confirmButton;
    [SerializeField] private Text messageText;
    [SerializeField] private float messageDuration = 2f;
    [SerializeField] private Color confirmDisabledColor = Color.gray;
    [SerializeField] private Color confirmEnabledColor = Color.green;
    [SerializeField] private bool autoBindLocalRewardController = true;

    private readonly List<RewardCardView> spawnedCards = new List<RewardCardView>();
    private Func<PlayerAbilityModule, bool> onConfirmed;
    private PlayerAbilityModule selectedModule;
    private RewardCardView selectedCard;
    private PlayerAbilityRewardController rewardController;
    private InventoryPanelController inventoryPanel;
    private Coroutine bindCoroutine;
    private Coroutine messageCoroutine;

    private void Awake()
    {
        if (confirmButton != null)
        {
            confirmButton.onClick.RemoveAllListeners();
            confirmButton.onClick.AddListener(OnClickConfirm);
        }

        Hide();
    }

    private void OnEnable()
    {
        if (!autoBindLocalRewardController)
            return;

        if (bindCoroutine == null)
            bindCoroutine = StartCoroutine(BindLocalRewardControllerRoutine());
    }

    private void OnDisable()
    {
        if (bindCoroutine != null)
        {
            StopCoroutine(bindCoroutine);
            bindCoroutine = null;
        }

        UnbindRewardController();
    }

    public void Show(
        IReadOnlyList<PlayerAbilityModule> modules,
        Func<PlayerAbilityModule, int> getLevelFunc,
        Func<PlayerAbilityModule, bool> confirmCallback)
    {
        ClearCards();

        selectedModule = null;
        selectedCard = null;
        onConfirmed = confirmCallback;

        if (rootObject != null)
            rootObject.SetActive(true);
        else
            gameObject.SetActive(true);

        SetConfirmButtonState(false);
        HideMessage();

        if (modules == null)
            return;

        for (int i = 0; i < modules.Count; i++)
        {
            PlayerAbilityModule module = modules[i];

            if (module == null)
                continue;

            int level = getLevelFunc != null ? getLevelFunc(module) : 1;

            RewardCardView card = Instantiate(rewardCardPrefab, cardContentParent);
            card.Setup(module, level, OnCardSelected);
            spawnedCards.Add(card);
        }
    }

    public void Hide()
    {
        ClearCards();

        selectedModule = null;
        selectedCard = null;
        onConfirmed = null;

        HideMessage();

        if (rootObject != null)
            rootObject.SetActive(false);
        else
            gameObject.SetActive(false);
    }

    private void OnCardSelected(RewardCardView card, PlayerAbilityModule module)
    {
        if (selectedCard != null)
            selectedCard.SetSelected(false);

        selectedCard = card;
        selectedModule = module;

        if (selectedCard != null)
            selectedCard.SetSelected(true);

        SetConfirmButtonState(true);
        HideMessage();
    }

    private void OnClickConfirm()
    {
        if (selectedModule == null)
        {
            ShowMessage("카드를 선택하세요");
            return;
        }

        if (onConfirmed != null && !onConfirmed.Invoke(selectedModule))
            return;

        Hide();
    }

    private IEnumerator BindLocalRewardControllerRoutine()
    {
        while (rewardController == null)
        {
            BindRewardController(FindLocalRewardController());

            if (rewardController != null)
            {
                bindCoroutine = null;
                yield break;
            }

            yield return new WaitForSeconds(0.25f);
        }

        bindCoroutine = null;
    }

    private void BindRewardController(PlayerAbilityRewardController controller)
    {
        if (rewardController == controller)
            return;

        UnbindRewardController();
        rewardController = controller;

        if (rewardController == null)
            return;

        rewardController.BossRewardOffered += OnBossRewardOffered;
        rewardController.BossRewardSelected += OnBossRewardSelected;
    }

    private void UnbindRewardController()
    {
        if (rewardController == null)
            return;

        rewardController.BossRewardOffered -= OnBossRewardOffered;
        rewardController.BossRewardSelected -= OnBossRewardSelected;
        rewardController = null;
    }

    private void OnBossRewardOffered(int bossStage, IReadOnlyList<PlayerAbilityModule> modules)
    {
        inventoryPanel ??= FindObjectOfType<InventoryPanelController>(true);
        if (inventoryPanel != null)
            inventoryPanel.SetRewardSelectOpen(true);

        Show(modules, GetRewardLevel, ConfirmRewardSelection);
    }

    private void OnBossRewardSelected(PlayerAbilityModule module)
    {
        if (inventoryPanel != null)
            inventoryPanel.SetRewardSelectOpen(false);
    }

    private int GetRewardLevel(PlayerAbilityModule module)
    {
        return 1;
    }

    private bool ConfirmRewardSelection(PlayerAbilityModule module)
    {
        if (rewardController == null)
        {
            ShowMessage("보상 컨트롤러를 찾을 수 없습니다");
            return false;
        }

        int optionIndex = GetPendingOptionIndex(module);
        if (optionIndex < 0)
        {
            ShowMessage("선택할 수 없는 보상입니다");
            return false;
        }

        if (!rewardController.SelectPendingOption(optionIndex))
        {
            ShowMessage("보상을 적용할 수 없습니다");
            return false;
        }

        if (inventoryPanel != null)
            inventoryPanel.SetRewardSelectOpen(false);

        return true;
    }

    private int GetPendingOptionIndex(PlayerAbilityModule module)
    {
        if (rewardController == null || rewardController.PendingOptions == null)
            return -1;

        IReadOnlyList<PlayerAbilityModule> pendingOptions = rewardController.PendingOptions;
        for (int i = 0; i < pendingOptions.Count; i++)
        {
            if (pendingOptions[i] == module)
                return i;
        }

        return -1;
    }

    private static PlayerAbilityRewardController FindLocalRewardController()
    {
        PlayerAbilityRewardController[] rewardControllers = FindObjectsOfType<PlayerAbilityRewardController>(true);
        foreach (PlayerAbilityRewardController controller in rewardControllers)
        {
            NetworkObject networkObject = controller.GetComponent<NetworkObject>();
            if (networkObject == null || networkObject.HasInputAuthority)
                return controller;
        }

        return null;
    }

    private void SetConfirmButtonState(bool hasSelectedCard)
    {
        if (confirmButton == null)
            return;

        Image buttonImage = confirmButton.GetComponent<Image>();

        if (buttonImage != null)
            buttonImage.color = hasSelectedCard ? confirmEnabledColor : confirmDisabledColor;
    }

    private void ShowMessage(string message)
    {
        if (messageText == null)
            return;

        if (messageCoroutine != null)
            StopCoroutine(messageCoroutine);

        messageCoroutine = StartCoroutine(ShowMessageRoutine(message));
    }

    private IEnumerator ShowMessageRoutine(string message)
    {
        messageText.gameObject.SetActive(true);
        messageText.text = message;

        yield return new WaitForSeconds(messageDuration);

        HideMessage();
    }

    private void HideMessage()
    {
        if (messageCoroutine != null)
        {
            StopCoroutine(messageCoroutine);
            messageCoroutine = null;
        }

        if (messageText != null)
            messageText.gameObject.SetActive(false);
    }

    private void ClearCards()
    {
        for (int i = 0; i < spawnedCards.Count; i++)
        {
            if (spawnedCards[i] != null)
                Destroy(spawnedCards[i].gameObject);
        }

        spawnedCards.Clear();
    }
}
