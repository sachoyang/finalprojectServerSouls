using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class CombatQuickMenuView : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private GameObject quickMenuRoot;
    [SerializeField] private GameObject menuPanel;
    [SerializeField] private GameObject surrenderConfirmPopup;
    [SerializeField] private GameObject surrenderVotePopup;
    [SerializeField] private GameObject voteFailedMessage;

    [Header("Buttons")]
    [SerializeField] private Button retreatButton;
    [SerializeField] private Button optionsButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button confirmYesButton;
    [SerializeField] private Button confirmNoButton;
    [SerializeField] private Button voteAgreeButton;
    [SerializeField] private Button voteRejectButton;

    [Header("Optional Option UI")]
    [SerializeField] private GameObject optionPanel;

    [Header("Message")]
    [SerializeField] private float voteFailedMessageDuration = 2f;
    [SerializeField] private Text surrenderConfirmMessageText;
    [SerializeField] private string surrenderConfirmMessage = "전투를 포기하시겠습니까?\n\n(2인 이상일 경우 투표가 진행됩니다)";
    [SerializeField] private string soloSelfReviveConfirmMessage = "부활하시겠습니까?";

    private SurrenderVoteManager surrenderVoteManager;
    private int activeVoteSessionId;
    private Coroutine voteFailedRoutine;
    private PlayerStats pendingSoloSelfReviveTarget;
    private bool soloSelfReviveConfirmActive;

    private void Awake()
    {
        ResolveSurrenderConfirmMessageText();
        BindButtons();
        CloseMenu();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            ThirdPersonCameraController.SuppressEscapeToggleThisFrame();
            ToggleMenu();
        }
    }

    public void ToggleMenu()
    {
        if (quickMenuRoot != null && quickMenuRoot.activeSelf)
            CloseMenu();
        else
            OpenMenu();
    }

    public void OpenMenu()
    {
        ClearSoloSelfReviveConfirm();
        SetSurrenderConfirmMessage(false);

        if (quickMenuRoot != null)
            quickMenuRoot.SetActive(true);

        SetPanelActive(menuPanel, true);
        SetPanelActive(surrenderConfirmPopup, false);
        SetPanelActive(surrenderVotePopup, false);
        SetPanelActive(voteFailedMessage, false);

        ThirdPersonCameraController.ForceCursorVisible = true;
    }

    public void CloseMenu()
    {
        if (soloSelfReviveConfirmActive)
        {
            SubmitSoloSelfRevive(false);
            return;
        }

        ClearSoloSelfReviveConfirm();
        SetSurrenderConfirmMessage(false);

        SetPanelActive(surrenderConfirmPopup, false);
        SetPanelActive(surrenderVotePopup, false);
        SetPanelActive(voteFailedMessage, false);

        if (quickMenuRoot != null)
            quickMenuRoot.SetActive(false);

        ThirdPersonCameraController.RequestCursorLock();
    }

    public void OnClickRetreat()
    {
        ClearSoloSelfReviveConfirm();
        SetSurrenderConfirmMessage(false);

        if (quickMenuRoot != null)
            quickMenuRoot.SetActive(true);

        SetPanelActive(menuPanel, false);
        SetPanelActive(surrenderConfirmPopup, true);
        SetPanelActive(surrenderVotePopup, false);
        SetPanelActive(voteFailedMessage, false);

        ThirdPersonCameraController.ForceCursorVisible = true;
    }

    public void OnClickOptions()
    {
        if (optionPanel != null)
            optionPanel.SetActive(true);
    }

    public void OnClickCancelSurrender()
    {
        if (soloSelfReviveConfirmActive)
        {
            SubmitSoloSelfRevive(false);
            return;
        }

        SetPanelActive(surrenderConfirmPopup, false);
        SetPanelActive(menuPanel, true);
    }

    public void OnClickConfirmSurrender()
    {
        if (soloSelfReviveConfirmActive)
        {
            SubmitSoloSelfRevive(true);
            return;
        }

        SetPanelActive(surrenderConfirmPopup, false);
        SetPanelActive(menuPanel, false);

        ResolveSurrenderVoteManager();

        if (surrenderVoteManager != null)
            surrenderVoteManager.RequestSurrenderFromLocalPlayer();
    }

    public void ShowSoloSelfRevivePopup(PlayerStats target)
    {
        if (target == null)
            return;

        pendingSoloSelfReviveTarget = target;
        soloSelfReviveConfirmActive = true;
        SetSurrenderConfirmMessage(true);

        if (quickMenuRoot != null)
            quickMenuRoot.SetActive(true);

        SetPanelActive(menuPanel, false);
        SetPanelActive(surrenderConfirmPopup, true);
        SetPanelActive(surrenderVotePopup, false);
        SetPanelActive(voteFailedMessage, false);

        ThirdPersonCameraController.ForceCursorVisible = true;
    }

    public void ShowSurrenderWaiting()
    {
        if (quickMenuRoot != null)
            quickMenuRoot.SetActive(true);

        SetPanelActive(menuPanel, false);
        SetPanelActive(surrenderConfirmPopup, false);
        SetPanelActive(surrenderVotePopup, false);
        SetPanelActive(voteFailedMessage, false);

        ThirdPersonCameraController.ForceCursorVisible = true;
    }

    public void ShowSurrenderVotePopup(int sessionId)
    {
        activeVoteSessionId = sessionId;

        if (quickMenuRoot != null)
            quickMenuRoot.SetActive(true);

        SetPanelActive(menuPanel, false);
        SetPanelActive(surrenderConfirmPopup, false);
        SetPanelActive(surrenderVotePopup, true);
        SetPanelActive(voteFailedMessage, false);

        ThirdPersonCameraController.ForceCursorVisible = true;
    }

    public void OnClickAgreeSurrenderVote()
    {
        SubmitVote(true);
    }

    public void OnClickRejectSurrenderVote()
    {
        SubmitVote(false);
    }

    public void ShowVoteFailedMessage()
    {
        if (voteFailedRoutine != null)
            StopCoroutine(voteFailedRoutine);

        voteFailedRoutine = StartCoroutine(ShowVoteFailedMessageRoutine());
    }

    private IEnumerator ShowVoteFailedMessageRoutine()
    {
        if (quickMenuRoot != null)
            quickMenuRoot.SetActive(true);

        SetPanelActive(menuPanel, false);
        SetPanelActive(surrenderConfirmPopup, false);
        SetPanelActive(surrenderVotePopup, false);
        SetPanelActive(voteFailedMessage, true);

        ThirdPersonCameraController.ForceCursorVisible = true;

        yield return new WaitForSeconds(voteFailedMessageDuration);

        SetPanelActive(voteFailedMessage, false);
        CloseMenu();
    }

    private void SubmitVote(bool agreed)
    {
        SetPanelActive(surrenderVotePopup, false);

        ResolveSurrenderVoteManager();

        if (surrenderVoteManager != null)
            surrenderVoteManager.SubmitLocalVote(activeVoteSessionId, agreed);
    }

    private void SubmitSoloSelfRevive(bool accepted)
    {
        PlayerStats target = pendingSoloSelfReviveTarget;
        ClearSoloSelfReviveConfirm();

        SetPanelActive(surrenderConfirmPopup, false);
        SetPanelActive(menuPanel, false);

        if (target != null)
        {
            if (accepted)
                target.ConsumeSoloSelfRevive();
            else
                target.DeclineSoloSelfRevive();
        }

        CloseMenu();
    }

    private void ClearSoloSelfReviveConfirm()
    {
        pendingSoloSelfReviveTarget = null;
        soloSelfReviveConfirmActive = false;
    }

    private void SetSurrenderConfirmMessage(bool isSoloSelfRevive)
    {
        ResolveSurrenderConfirmMessageText();

        if (surrenderConfirmMessageText != null)
        {
            surrenderConfirmMessageText.text = isSoloSelfRevive
                ? soloSelfReviveConfirmMessage
                : surrenderConfirmMessage;
        }
    }

    private void ResolveSurrenderConfirmMessageText()
    {
        if (surrenderConfirmMessageText != null || surrenderConfirmPopup == null)
            return;

        Text[] texts = surrenderConfirmPopup.GetComponentsInChildren<Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            Text text = texts[i];
            if (text != null && text.text.Contains("포기"))
            {
                surrenderConfirmMessageText = text;
                surrenderConfirmMessage = text.text;
                return;
            }
        }
    }

    private void ResolveSurrenderVoteManager()
    {
        if (surrenderVoteManager == null)
            surrenderVoteManager = FindObjectOfType<SurrenderVoteManager>();
    }

    private void BindButtons()
    {
        if (retreatButton != null)
        {
            retreatButton.onClick.RemoveListener(OnClickRetreat);
            retreatButton.onClick.AddListener(OnClickRetreat);
        }

        if (optionsButton != null)
        {
            optionsButton.onClick.RemoveListener(OnClickOptions);
            optionsButton.onClick.AddListener(OnClickOptions);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(CloseMenu);
            closeButton.onClick.AddListener(CloseMenu);
        }

        if (confirmYesButton != null)
        {
            confirmYesButton.onClick.RemoveListener(OnClickConfirmSurrender);
            confirmYesButton.onClick.AddListener(OnClickConfirmSurrender);
        }

        if (confirmNoButton != null)
        {
            confirmNoButton.onClick.RemoveListener(OnClickCancelSurrender);
            confirmNoButton.onClick.AddListener(OnClickCancelSurrender);
        }

        if (voteAgreeButton != null)
        {
            voteAgreeButton.onClick.RemoveListener(OnClickAgreeSurrenderVote);
            voteAgreeButton.onClick.AddListener(OnClickAgreeSurrenderVote);
        }

        if (voteRejectButton != null)
        {
            voteRejectButton.onClick.RemoveListener(OnClickRejectSurrenderVote);
            voteRejectButton.onClick.AddListener(OnClickRejectSurrenderVote);
        }
    }

    private void SetPanelActive(GameObject target, bool active)
    {
        if (target != null)
            target.SetActive(active);
    }
}
