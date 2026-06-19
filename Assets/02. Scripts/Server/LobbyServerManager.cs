using System.Collections;
using System.Linq;
using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LobbyServerManager : NetworkBehaviour
{
    [Header("Character Models")]
    public GameObject[] lobbyCharacters;

    [Header("Slot UI")]
    public GameObject[] slotPanels;
    public GameObject[] readyEffects;
    public GameObject[] youIndicators;

    [Header("Player Name UI")]
    public Text[] nicknameTexts;

    [Header("Local Ready Button")]
    public Button localReadyButton;
    public Text localReadyButtonText;

    [Header("Global Buttons")]
    public Button battleButton;
    public Text warningMessageText;

    [Header("Scene Fade")]
    public SceneFadeManager fadeManager;

    [Header("Scene Names")]
    public string titleSceneName = "scTitle uicreate Main";

    [Header("Message")]
    public float warningMessageDuration = 2f;

    private bool isChangingScene;
    private Coroutine warningMessageCoroutine;
    private ChangeDetector _changeDetector;

    [Networked] public PlayerRef Slot0_Owner { get; set; }
    [Networked] public PlayerRef Slot1_Owner { get; set; }
    [Networked] public PlayerRef Slot2_Owner { get; set; }

    [Networked] public NetworkBool Slot0_Ready { get; set; }
    [Networked] public NetworkBool Slot1_Ready { get; set; }
    [Networked] public NetworkBool Slot2_Ready { get; set; }

    [Networked, Capacity(32)] public string Slot0_Nickname { get; set; }
    [Networked, Capacity(32)] public string Slot1_Nickname { get; set; }
    [Networked, Capacity(32)] public string Slot2_Nickname { get; set; }

    private void Start()
    {
        ShowLobbyCharacters();
        HideWarningMessage();
        HideAllReadyEffects();
    }

    public override void Spawned()
    {
        Debug.Log("[Lobby] Lobby UI spawned.");

        _changeDetector =
            GetChangeDetector(ChangeDetector.Source.SimulationState);

        RefreshLobbyUI();
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority)
            return;

        RemoveDisconnectedPlayers();
        AssignNewPlayers();
    }

    public override void Render()
    {
        if (_changeDetector != null)
        {
            foreach (var change in _changeDetector.DetectChanges(this))
            {
                switch (change)
                {
                    case nameof(Slot0_Ready):
                    case nameof(Slot1_Ready):
                    case nameof(Slot2_Ready):
                    case nameof(Slot0_Owner):
                    case nameof(Slot1_Owner):
                    case nameof(Slot2_Owner):
                    case nameof(Slot0_Nickname):
                    case nameof(Slot1_Nickname):
                    case nameof(Slot2_Nickname):
                        RefreshLobbyUI();
                        break;
                }
            }
        }

        RefreshBattleButton();
    }

    private void RemoveDisconnectedPlayers()
    {
        if (Slot0_Owner != PlayerRef.None &&
            !Runner.ActivePlayers.Contains(Slot0_Owner))
        {
            Slot0_Owner = PlayerRef.None;
            Slot0_Ready = false;
            Slot0_Nickname = string.Empty;
        }

        if (Slot1_Owner != PlayerRef.None &&
            !Runner.ActivePlayers.Contains(Slot1_Owner))
        {
            Slot1_Owner = PlayerRef.None;
            Slot1_Ready = false;
            Slot1_Nickname = string.Empty;
        }

        if (Slot2_Owner != PlayerRef.None &&
            !Runner.ActivePlayers.Contains(Slot2_Owner))
        {
            Slot2_Owner = PlayerRef.None;
            Slot2_Ready = false;
            Slot2_Nickname = string.Empty;
        }
    }

    private void AssignNewPlayers()
    {
        foreach (var player in Runner.ActivePlayers)
        {
            if (Slot0_Owner == player ||
                Slot1_Owner == player ||
                Slot2_Owner == player)
            {
                continue;
            }

            if (Slot0_Owner == PlayerRef.None)
            {
                Slot0_Owner = player;
                Slot0_Ready = false;
            }
            else if (Slot1_Owner == PlayerRef.None)
            {
                Slot1_Owner = player;
                Slot1_Ready = false;
            }
            else if (Slot2_Owner == PlayerRef.None)
            {
                Slot2_Owner = player;
                Slot2_Ready = false;
            }
        }
    }

    private void RefreshLobbyUI()
    {
        if (Runner == null)
            return;

        RefreshSlotUI(
            0,
            Slot0_Owner,
            Slot0_Ready,
            Slot0_Nickname);

        RefreshSlotUI(
            1,
            Slot1_Owner,
            Slot1_Ready,
            Slot1_Nickname);

        RefreshSlotUI(
            2,
            Slot2_Owner,
            Slot2_Ready,
            Slot2_Nickname);

        RefreshLocalReadyButton();
        RefreshBattleButton();
    }

    private void RefreshSlotUI(
        int slotIndex,
        PlayerRef owner,
        bool isReady,
        string nickname)
    {
        bool hasPlayer = owner != PlayerRef.None;

        if (IsValidIndex(slotPanels, slotIndex))
        {
            slotPanels[slotIndex].SetActive(hasPlayer);
        }

        SetReadyEffect(slotIndex, hasPlayer && isReady);

        if (!hasPlayer)
        {
            SetYouIndicator(slotIndex, false);
            return;
        }

        bool isLocalPlayer = owner == Runner.LocalPlayer;

        if (isLocalPlayer &&
            BackendManager.HasInstance &&
            nickname != BackendManager.Instance.CurrentNickname)
        {
            RPC_SetNickname(
                slotIndex,
                BackendManager.Instance.CurrentNickname);
        }

        if (IsValidIndex(nicknameTexts, slotIndex))
        {
            nicknameTexts[slotIndex].text =
                string.IsNullOrEmpty(nickname)
                    ? "Loading..."
                    : nickname;
        }

        SetYouIndicator(slotIndex, isLocalPlayer);
    }

    private void RefreshLocalReadyButton()
    {
        int localSlotIndex = GetLocalPlayerSlotIndex();
        bool hasLocalSlot = localSlotIndex >= 0;

        if (localReadyButton != null)
        {
            localReadyButton.interactable = hasLocalSlot;
        }

        if (localReadyButtonText != null)
        {
            bool isReady =
                hasLocalSlot && GetSlotReady(localSlotIndex);

            localReadyButtonText.text =
                isReady ? "Cancel" : "Ready";
        }
    }

    private void RefreshBattleButton()
    {
        if (battleButton == null || Runner == null)
            return;

        battleButton.gameObject.SetActive(HasStateAuthority);

        if (!HasStateAuthority)
            return;

        int totalActivePlayers = Runner.ActivePlayers.Count();
        int totalReadyPlayers = 0;

        if (Slot0_Owner != PlayerRef.None && Slot0_Ready)
            totalReadyPlayers++;

        if (Slot1_Owner != PlayerRef.None && Slot1_Ready)
            totalReadyPlayers++;

        if (Slot2_Owner != PlayerRef.None && Slot2_Ready)
            totalReadyPlayers++;

        battleButton.interactable =
            totalActivePlayers > 0 &&
            totalReadyPlayers >= totalActivePlayers;
    }

    public void OnClickLocalReadyButton()
    {
        if (Runner == null)
            return;

        int localSlotIndex = GetLocalPlayerSlotIndex();

        if (localSlotIndex < 0)
        {
            Debug.LogWarning(
                "[Lobby] Local player slot was not found.");
            return;
        }

        bool nextReadyState =
            !GetSlotReady(localSlotIndex);

        RPC_ToggleReady(
            localSlotIndex,
            nextReadyState);
    }

    private int GetLocalPlayerSlotIndex()
    {
        if (Runner == null)
            return -1;

        if (Slot0_Owner == Runner.LocalPlayer)
            return 0;

        if (Slot1_Owner == Runner.LocalPlayer)
            return 1;

        if (Slot2_Owner == Runner.LocalPlayer)
            return 2;

        return -1;
    }

    private bool GetSlotReady(int slotIndex)
    {
        switch (slotIndex)
        {
            case 0:
                return Slot0_Ready;

            case 1:
                return Slot1_Ready;

            case 2:
                return Slot2_Ready;

            default:
                return false;
        }
    }

    private void SetReadyEffect(
        int slotIndex,
        bool isReady)
    {
        if (!IsValidIndex(readyEffects, slotIndex))
            return;

        readyEffects[slotIndex].SetActive(isReady);
    }

    private void SetYouIndicator(
        int slotIndex,
        bool isLocalPlayer)
    {
        if (!IsValidIndex(youIndicators, slotIndex))
            return;

        youIndicators[slotIndex].SetActive(isLocalPlayer);
    }

    private void HideAllReadyEffects()
    {
        if (readyEffects == null)
            return;

        for (int i = 0; i < readyEffects.Length; i++)
        {
            if (readyEffects[i] != null)
            {
                readyEffects[i].SetActive(false);
            }
        }
    }

    private bool IsValidIndex(
        GameObject[] array,
        int index)
    {
        return array != null &&
               index >= 0 &&
               index < array.Length &&
               array[index] != null;
    }

    private bool IsValidIndex(
        Text[] array,
        int index)
    {
        return array != null &&
               index >= 0 &&
               index < array.Length &&
               array[index] != null;
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_ToggleReady(
        int slotIndex,
        bool nextReadyState)
    {
        if (slotIndex == 0)
            Slot0_Ready = nextReadyState;

        if (slotIndex == 1)
            Slot1_Ready = nextReadyState;

        if (slotIndex == 2)
            Slot2_Ready = nextReadyState;
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_SetNickname(
        int slotIndex,
        string nickname)
    {
        if (slotIndex == 0)
            Slot0_Nickname = nickname;

        if (slotIndex == 1)
            Slot1_Nickname = nickname;

        if (slotIndex == 2)
            Slot2_Nickname = nickname;
    }

    public void OnClickBattleButton()
    {
        if (!HasStateAuthority || isChangingScene)
            return;

        isChangingScene = true;

        Debug.Log(
            "[Lobby] All players are ready. Starting battle.");

        if (Runner != null &&
            Runner.SessionInfo != null)
        {
            Runner.SessionInfo.IsVisible = false;
            Runner.SessionInfo.IsOpen = false;
        }

        GameProgressionManager.Instance
            .StartFirstLevel(Runner);
    }

    public async void OnClickBackButton()
    {
        if (isChangingScene)
            return;

        isChangingScene = true;

        string sceneName = titleSceneName;

        if (Runner != null)
        {
            await Runner.Shutdown();
        }

        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError(
                "[Lobby] Title scene name is empty.");
            return;
        }

        SceneManager.LoadScene(sceneName);
    }

    private void ShowLobbyCharacters()
    {
        if (lobbyCharacters == null)
            return;

        for (int i = 0;
             i < lobbyCharacters.Length;
             i++)
        {
            if (lobbyCharacters[i] != null)
            {
                lobbyCharacters[i].SetActive(true);
            }
        }
    }

    private void ShowWarningMessage(string message)
    {
        if (warningMessageText == null)
            return;

        if (warningMessageCoroutine != null)
        {
            StopCoroutine(warningMessageCoroutine);
        }

        warningMessageCoroutine =
            StartCoroutine(
                ShowWarningMessageRoutine(message));
    }

    private IEnumerator ShowWarningMessageRoutine(
        string message)
    {
        warningMessageText.gameObject.SetActive(true);
        warningMessageText.text = message;

        yield return new WaitForSeconds(
            warningMessageDuration);

        HideWarningMessage();
    }

    private void HideWarningMessage()
    {
        if (warningMessageText != null)
        {
            warningMessageText.gameObject.SetActive(false);
        }
    }
}