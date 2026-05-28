using System;
using System.Collections;
using System.Collections.Generic;
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

    private readonly List<RewardCardView> spawnedCards = new List<RewardCardView>();
    private Action<PlayerAbilityModule> onConfirmed;
    private PlayerAbilityModule selectedModule;
    private RewardCardView selectedCard;
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

    public void Show(
        IReadOnlyList<PlayerAbilityModule> modules,
        Func<PlayerAbilityModule, int> getLevelFunc,
        Action<PlayerAbilityModule> confirmCallback)
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

        if (onConfirmed != null)
            onConfirmed.Invoke(selectedModule);

        Hide();
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