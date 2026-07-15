using Fusion;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LobbyServerManager : NetworkBehaviour
{
    [Header("Character Models")]
    public GameObject[] lobbyCharacters;

    [Header("Slot UI")]
    public GameObject[] slotPanels;

    [Tooltip("Slot0, Slot1, Slot2 ��ġ�� Ready üũ ǥ��")]
    public GameObject[] readyEffects;

    public GameObject[] youIndicators;

    [Header("Player Name UI")]
    public Text[] nicknameTexts;

    [Header("Local Action Button")]
    public Button localReadyButton;
    public Text localReadyButtonText;

    [Tooltip("���� �ϴ� ��ư�� BG Image")]
    public Image localReadyButtonBackground;

    [Tooltip("���� �ϴ� ��ư�� EF ������Ʈ")]
    public GameObject localReadyButtonEffect;

    [Tooltip("���� �ϴ� ��ư EF�� Image")]
    public Image localReadyButtonEffectImage;

    [Header("Button Colors")]
    public Color inactiveRed =
        new Color(0.55f, 0.03f, 0.03f, 1f);

    public Color activeGreen =
        new Color(0.05f, 0.55f, 0.18f, 1f);

    [Header("Message")]
    public Text warningMessageText;
    public float warningMessageDuration = 2f;

    [Header("Guest Nickname Fallback")]
    [Tooltip("Nickname prefix for users without a login nickname (debug/guest). Slot number is appended. e.g. 'Guest ' -> Guest 1")]
    public string guestNicknamePrefix = "Guest ";

    [Header("Scene Names")]
    public string titleSceneName =
        "scTitle uicreate Main";

    private bool isChangingScene;
    private Coroutine warningMessageCoroutine;
    private ChangeDetector changeDetector;

    [Networked]
    public PlayerRef Slot0_Owner { get; set; }

    [Networked]
    public PlayerRef Slot1_Owner { get; set; }

    [Networked]
    public PlayerRef Slot2_Owner { get; set; }

    [Networked]
    public NetworkBool Slot0_Ready { get; set; }

    [Networked]
    public NetworkBool Slot1_Ready { get; set; }

    [Networked]
    public NetworkBool Slot2_Ready { get; set; }

    [Networked, Capacity(32)]
    public string Slot0_Nickname { get; set; }

    [Networked, Capacity(32)]
    public string Slot1_Nickname { get; set; }

    [Networked, Capacity(32)]
    public string Slot2_Nickname { get; set; }

    private void Start()
    {
        ShowLobbyCharacters();
        HideWarningMessage();
        HideAllReadyEffects();

        if (localReadyButtonEffect != null)
            localReadyButtonEffect.SetActive(true);

        SetButtonColorBlockToWhite();
    }

    public override void Spawned()
    {
        changeDetector = GetChangeDetector(
            ChangeDetector.Source.SimulationState);

        // 전투 진입(OnClickBattleButton) / 보스 기상(NetworkBossCore) 때 잠갔던 방을
        // 로비로 돌아온 시점에 다시 연다. (보스 클리어 후 복귀든 전멸 복귀든 동일)
        // 정책: '로비에 있는 동안만' 방이 열려 있고, 전투/보상 씬 동안에는 잠긴다.
        if (HasStateAuthority &&
            Runner != null &&
            Runner.SessionInfo != null &&
            !Runner.SessionInfo.IsOpen)
        {
            Runner.SessionInfo.IsOpen = true;
            Runner.SessionInfo.IsVisible = true;
            Debug.Log("[네트워크] 로비 복귀. 방 문을 다시 엽니다.");
        }

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
        if (changeDetector != null)
        {
            foreach (var change in changeDetector.DetectChanges(this))
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

        // ���� ���� ���Ŀ��� ��ư�� Ȱ��ȭ�ǵ��� �� ������ �����Ѵ�.
        RefreshLocalActionButton();
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

        RefreshLocalActionButton();
    }

    private void RefreshSlotUI(
        int slotIndex,
        PlayerRef owner,
        bool isReady,
        string nickname)
    {
        bool hasPlayer = owner != PlayerRef.None;

        if (IsValidIndex(slotPanels, slotIndex))
            slotPanels[slotIndex].SetActive(hasPlayer);

        SetReadyEffect(slotIndex, hasPlayer && isReady);

        if (!hasPlayer)
        {
            SetYouIndicator(slotIndex, false);
            return;
        }

        bool isLocalPlayer =
            owner == Runner.LocalPlayer;

        // 닉네임 결정: 정식 로그인 닉네임 → 없으면(디버그/게스트) "Guest N" 폴백.
        //  null 그대로 RPC에 넣으면 직렬화 예외로 Spawned()→RefreshLobbyUI가 통째로 중단되어
        //  슬롯 UI가 전부 깨지므로, 항상 유효한 문자열만 보낸다.
        //  네트워크 값이 아직 도착하지 않은 동안에만 아래 표시 로직이 "Loading..."을 보여준다.
        string myNickname =
            BackendManager.HasInstance
                ? BackendManager.Instance.CurrentNickname
                : null;

        if (string.IsNullOrEmpty(myNickname))
            myNickname = guestNicknamePrefix + (slotIndex + 1);

        if (isLocalPlayer &&
            nickname != myNickname)
        {
            RPC_SetNickname(
                slotIndex,
                myNickname);
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

    private void RefreshLocalActionButton()
    {
        if (Runner == null)
            return;

        int localSlotIndex =
            GetLocalPlayerSlotIndex();

        bool hasLocalSlot =
            localSlotIndex >= 0;

        bool isHost =
            HasStateAuthority;

        bool isLocalPlayerReady =
            hasLocalSlot &&
            GetSlotReady(localSlotIndex);

        if (localReadyButton != null)
        {
            localReadyButton.interactable =
                hasLocalSlot &&
                !isChangingScene;
        }

        if (localReadyButtonText != null)
        {
            if (isHost)
            {
                localReadyButtonText.text = "Start";
            }
            else
            {
                localReadyButtonText.text =
                    isLocalPlayerReady
                        ? "Cancel"
                        : "Ready";
            }
        }

        // ������ Start�� �Խ�Ʈ�� Cancel ���´� �ʷϻ��̴�.
        bool useGreen =
            isHost || isLocalPlayerReady;

        Color targetColor =
            useGreen
                ? activeGreen
                : inactiveRed;

        if (localReadyButtonBackground != null)
        {
            localReadyButtonBackground.color =
                targetColor;
        }

        if (localReadyButtonEffect != null)
        {
            localReadyButtonEffect.SetActive(
                hasLocalSlot);
        }

        if (localReadyButtonEffectImage != null)
        {
            localReadyButtonEffectImage.color =
                targetColor;
        }
    }

    public void OnClickLocalReadyButton()
    {
        if (Runner == null || isChangingScene)
            return;

        int localSlotIndex =
            GetLocalPlayerSlotIndex();

        if (localSlotIndex < 0)
        {
            Debug.LogWarning(
                "[Lobby] Local player slot was not found.");

            return;
        }

        // ������ Ready�� ������� �ʰ� �ٷ� ������ �õ��Ѵ�.
        if (HasStateAuthority)
        {
            TryStartBattle();
            return;
        }

        bool nextReadyState =
            !GetSlotReady(localSlotIndex);

        RPC_ToggleReady(
            localSlotIndex,
            nextReadyState);
    }

    private void TryStartBattle()
    {
        if (!HasStateAuthority || isChangingScene)
            return;

        if (!AreAllGuestPlayersReady())
        {
            ShowWarningMessage(
                "�غ����� ���� �÷��̾ �ֽ��ϴ�.");

            return;
        }

        StartBattle();
    }

    private bool AreAllGuestPlayersReady()
    {
        if (Runner == null)
            return false;

        if (Slot0_Owner != PlayerRef.None &&
            Slot0_Owner != Runner.LocalPlayer &&
            !Slot0_Ready)
        {
            return false;
        }

        if (Slot1_Owner != PlayerRef.None &&
            Slot1_Owner != Runner.LocalPlayer &&
            !Slot1_Ready)
        {
            return false;
        }

        if (Slot2_Owner != PlayerRef.None &&
            Slot2_Owner != Runner.LocalPlayer &&
            !Slot2_Ready)
        {
            return false;
        }

        return true;
    }

    private void StartBattle()
    {
        if (!HasStateAuthority || isChangingScene)
            return;

        isChangingScene = true;

        if (Runner != null &&
            Runner.SessionInfo != null)
        {
            Runner.SessionInfo.IsVisible = false;
            Runner.SessionInfo.IsOpen = false;
        }

        if (GameProgressionManager.Instance == null)
        {
            Debug.LogError(
                "[Lobby] GameProgressionManager is missing.");

            isChangingScene = false;
            return;
        }

        GameProgressionManager.Instance
            .StartFirstLevel(Runner);
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

        youIndicators[slotIndex]
            .SetActive(isLocalPlayer);
    }

    private void HideAllReadyEffects()
    {
        if (readyEffects == null)
            return;

        for (int i = 0; i < readyEffects.Length; i++)
        {
            if (readyEffects[i] != null)
                readyEffects[i].SetActive(false);
        }
    }

    private void SetButtonColorBlockToWhite()
    {
        if (localReadyButton == null)
            return;

        ColorBlock colors =
            localReadyButton.colors;

        colors.normalColor = Color.white;
        colors.highlightedColor = Color.white;

        colors.pressedColor =
            new Color(0.85f, 0.85f, 0.85f, 1f);

        colors.selectedColor = Color.white;
        colors.disabledColor = Color.white;

        localReadyButton.colors = colors;
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

    [Rpc(
        RpcSources.All,
        RpcTargets.StateAuthority)]
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

    [Rpc(
        RpcSources.All,
        RpcTargets.StateAuthority)]
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

    public async void OnClickBackButton()
    {
        if (isChangingScene)
            return;

        isChangingScene = true;

        string sceneName =
            titleSceneName;

        if (Runner != null)
            await Runner.Shutdown();

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

        for (int i = 0; i < lobbyCharacters.Length; i++)
        {
            if (lobbyCharacters[i] != null)
                lobbyCharacters[i].SetActive(true);
        }
    }

    private void ShowWarningMessage(string message)
    {
        if (warningMessageText == null)
            return;

        if (warningMessageCoroutine != null)
            StopCoroutine(warningMessageCoroutine);

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
            warningMessageText.gameObject.SetActive(false);
    }
}
