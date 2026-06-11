using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LobbyServerManager : NetworkBehaviour
{
    [Header("Character Models")]
    public GameObject[] lobbyCharacters;

    [Header("Slot UI Arrays")]
    [Tooltip("슬롯 1, 2, 3의 부모 패널 오브젝트")]
    public GameObject[] slotPanels;

    [Tooltip("슬롯별 레디 버튼")]
    public Button[] readyButtons;

    [Tooltip("슬롯별 레디 텍스트 (Ready / Ready!)")]
    public Text[] readyTexts;

    [Tooltip("자신이 누구인지 알려주는 'YOU' 인디케이터 오브젝트")]
    public GameObject[] youIndicators;

    [Header("Player Name UI")]
    [Tooltip("모든 유저에게 보여질 닉네임 텍스트")]
    public Text[] nicknameTexts;

    [Header("Ready State UI (Global)")]
    public Button battleButton;
    public Text warningMessageText;

    [Header("Ready Button Color")]
    public Color notReadyColor = Color.white;
    public Color readyColor = new Color(0.4f, 1f, 0.4f);

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
    }

    public override void Spawned()
    {
        Debug.Log("[Lobby] 포톤 로비 동기화 오브젝트 생성 완료.");

        _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);
        RefreshLobbyUI();
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority) return;

        if (Slot0_Owner != PlayerRef.None && !Runner.ActivePlayers.Contains(Slot0_Owner))
        {
            Slot0_Owner = PlayerRef.None;
            Slot0_Ready = false;
            Slot0_Nickname = "";
        }

        if (Slot1_Owner != PlayerRef.None && !Runner.ActivePlayers.Contains(Slot1_Owner))
        {
            Slot1_Owner = PlayerRef.None;
            Slot1_Ready = false;
            Slot1_Nickname = "";
        }

        if (Slot2_Owner != PlayerRef.None && !Runner.ActivePlayers.Contains(Slot2_Owner))
        {
            Slot2_Owner = PlayerRef.None;
            Slot2_Ready = false;
            Slot2_Nickname = "";
        }

        foreach (var player in Runner.ActivePlayers)
        {
            if (Slot0_Owner == player || Slot1_Owner == player || Slot2_Owner == player)
                continue;

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

    public override void Render()
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

        RefreshBattleButton();
    }

    private void RefreshLobbyUI()
    {
        if (Runner == null) return;

        bool hasP0 = Slot0_Owner != PlayerRef.None;
        if (slotPanels != null && slotPanels.Length > 0 && slotPanels[0] != null)
            slotPanels[0].SetActive(hasP0);

        if (hasP0)
        {
            if (Slot0_Owner == Runner.LocalPlayer && Slot0_Nickname != BackendManager.Instance.CurrentNickname)
            {
                RPC_SetNickname(0, BackendManager.Instance.CurrentNickname);
            }

            if (nicknameTexts != null && nicknameTexts.Length > 0 && nicknameTexts[0] != null)
                nicknameTexts[0].text = string.IsNullOrEmpty(Slot0_Nickname) ? "Loading..." : Slot0_Nickname;

            if (readyTexts != null && readyTexts.Length > 0 && readyTexts[0] != null)
                readyTexts[0].text = Slot0_Ready ? "Ready!" : "Ready";

            if (readyButtons != null && readyButtons.Length > 0 && readyButtons[0] != null)
            {
                readyButtons[0].colors = GetUpdatedButtonColors(Slot0_Ready);
                readyButtons[0].interactable = Slot0_Owner == Runner.LocalPlayer;
            }

            if (youIndicators != null && youIndicators.Length > 0 && youIndicators[0] != null)
                youIndicators[0].SetActive(Slot0_Owner == Runner.LocalPlayer);
        }

        bool hasP1 = Slot1_Owner != PlayerRef.None;
        if (slotPanels != null && slotPanels.Length > 1 && slotPanels[1] != null)
            slotPanels[1].SetActive(hasP1);

        if (hasP1)
        {
            if (Slot1_Owner == Runner.LocalPlayer && Slot1_Nickname != BackendManager.Instance.CurrentNickname)
            {
                RPC_SetNickname(1, BackendManager.Instance.CurrentNickname);
            }

            if (nicknameTexts != null && nicknameTexts.Length > 1 && nicknameTexts[1] != null)
                nicknameTexts[1].text = string.IsNullOrEmpty(Slot1_Nickname) ? "Loading..." : Slot1_Nickname;

            if (readyTexts != null && readyTexts.Length > 1 && readyTexts[1] != null)
                readyTexts[1].text = Slot1_Ready ? "Ready!" : "Ready";

            if (readyButtons != null && readyButtons.Length > 1 && readyButtons[1] != null)
            {
                readyButtons[1].colors = GetUpdatedButtonColors(Slot1_Ready);
                readyButtons[1].interactable = Slot1_Owner == Runner.LocalPlayer;
            }

            if (youIndicators != null && youIndicators.Length > 1 && youIndicators[1] != null)
                youIndicators[1].SetActive(Slot1_Owner == Runner.LocalPlayer);
        }

        bool hasP2 = Slot2_Owner != PlayerRef.None;
        if (slotPanels != null && slotPanels.Length > 2 && slotPanels[2] != null)
            slotPanels[2].SetActive(hasP2);

        if (hasP2)
        {
            if (Slot2_Owner == Runner.LocalPlayer && Slot2_Nickname != BackendManager.Instance.CurrentNickname)
            {
                RPC_SetNickname(2, BackendManager.Instance.CurrentNickname);
            }

            if (nicknameTexts != null && nicknameTexts.Length > 2 && nicknameTexts[2] != null)
                nicknameTexts[2].text = string.IsNullOrEmpty(Slot2_Nickname) ? "Loading..." : Slot2_Nickname;

            if (readyTexts != null && readyTexts.Length > 2 && readyTexts[2] != null)
                readyTexts[2].text = Slot2_Ready ? "Ready!" : "Ready";

            if (readyButtons != null && readyButtons.Length > 2 && readyButtons[2] != null)
            {
                readyButtons[2].colors = GetUpdatedButtonColors(Slot2_Ready);
                readyButtons[2].interactable = Slot2_Owner == Runner.LocalPlayer;
            }

            if (youIndicators != null && youIndicators.Length > 2 && youIndicators[2] != null)
                youIndicators[2].SetActive(Slot2_Owner == Runner.LocalPlayer);
        }
    }

    private void RefreshBattleButton()
    {
        if (battleButton != null && Runner != null)
        {
            battleButton.gameObject.SetActive(HasStateAuthority);

            if (HasStateAuthority)
            {
                int totalActivePlayers = Runner.ActivePlayers.Count();
                int totalReadyPlayers = 0;

                if (Slot0_Owner != PlayerRef.None && Slot0_Ready) totalReadyPlayers++;
                if (Slot1_Owner != PlayerRef.None && Slot1_Ready) totalReadyPlayers++;
                if (Slot2_Owner != PlayerRef.None && Slot2_Ready) totalReadyPlayers++;

                battleButton.interactable = totalReadyPlayers >= totalActivePlayers;
            }
        }
    }

    private ColorBlock GetUpdatedButtonColors(bool isReady)
    {
        ColorBlock colors = ColorBlock.defaultColorBlock;

        if (readyButtons != null && readyButtons.Length > 0 && readyButtons[0] != null)
        {
            colors = readyButtons[0].colors;
        }

        Color targetColor = isReady ? readyColor : notReadyColor;
        colors.normalColor = targetColor;
        colors.highlightedColor = targetColor;
        colors.pressedColor = targetColor;
        colors.selectedColor = targetColor;
        colors.disabledColor = new Color(targetColor.r, targetColor.g, targetColor.b, 0.4f);

        return colors;
    }

    public void OnClickReadySlotButton(int slotIndex)
    {
        if (slotIndex == 0 && Slot0_Owner == Runner.LocalPlayer) RPC_ToggleReady(0, !Slot0_Ready);
        if (slotIndex == 1 && Slot1_Owner == Runner.LocalPlayer) RPC_ToggleReady(1, !Slot1_Ready);
        if (slotIndex == 2 && Slot2_Owner == Runner.LocalPlayer) RPC_ToggleReady(2, !Slot2_Ready);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_ToggleReady(int slotIndex, bool nextReadyState)
    {
        if (slotIndex == 0) Slot0_Ready = nextReadyState;
        if (slotIndex == 1) Slot1_Ready = nextReadyState;
        if (slotIndex == 2) Slot2_Ready = nextReadyState;
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_SetNickname(int slotIndex, string nickname)
    {
        if (slotIndex == 0) Slot0_Nickname = nickname;
        if (slotIndex == 1) Slot1_Nickname = nickname;
        if (slotIndex == 2) Slot2_Nickname = nickname;
    }

    public void OnClickBattleButton()
    {
        if (!HasStateAuthority || isChangingScene) return;

        isChangingScene = true;

        Debug.Log("모든 인원 준비 완료. 보스전 레벨로 전체 이동합니다.");

        // 방 잠그기 (난입 방지)
        if (Runner != null && Runner.SessionInfo != null)
        {
            // 1. IsVisible = false: 자동 매칭(랜덤) 리스트에서 이 방을 숨깁니다.
            Runner.SessionInfo.IsVisible = false; 
            
            // 2. IsOpen = false: 방 코드를 직접 치고 들어오는 것조차 완벽하게 차단합니다.
            Runner.SessionInfo.IsOpen = false; 
            
            Debug.Log("[Lobby] 방 문을 잠갔습니다. 더 이상 새로운 유저가 난입할 수 없습니다.");
        }

        GameProgressionManager.Instance.StartFirstLevel(Runner);
    }

    public async void OnClickBackButton()
    {
        if (isChangingScene)
            return;

        isChangingScene = true;

        string sceneName = titleSceneName;

        Debug.Log("[Lobby] Back 버튼 클릭. 로비를 종료하고 타이틀로 이동합니다.");

        if (Runner != null)
        {
            Debug.Log("[Lobby] Runner Shutdown 시작");
            await Runner.Shutdown();
            Debug.Log("[Lobby] Runner Shutdown 완료");
        }

        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError("[Lobby] Title Scene Name이 비어 있습니다.");
            return;
        }

        Debug.Log("[Lobby] 타이틀 씬 이동: " + sceneName);
        SceneManager.LoadScene(sceneName);
    }

    private void ShowLobbyCharacters()
    {
        if (lobbyCharacters == null || lobbyCharacters.Length == 0) return;

        for (int i = 0; i < lobbyCharacters.Length; i++)
        {
            if (lobbyCharacters[i] != null)
                lobbyCharacters[i].SetActive(true);
        }
    }

    private void ShowWarningMessage(string message)
    {
        if (warningMessageText == null) return;

        if (warningMessageCoroutine != null)
            StopCoroutine(warningMessageCoroutine);

        warningMessageCoroutine = StartCoroutine(ShowWarningMessageRoutine(message));
    }

    private IEnumerator ShowWarningMessageRoutine(string message)
    {
        warningMessageText.gameObject.SetActive(true);
        warningMessageText.text = message;

        yield return new WaitForSeconds(warningMessageDuration);

        HideWarningMessage();
    }

    private void HideWarningMessage()
    {
        if (warningMessageText != null)
            warningMessageText.gameObject.SetActive(false);
    }
}