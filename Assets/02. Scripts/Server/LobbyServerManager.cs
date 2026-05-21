using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Fusion;
using UnityEngine;
using UnityEngine.UI;

public class LobbyServerManager : NetworkBehaviour
{
    [Header("Character Models")]
    public GameObject[] lobbyCharacters;

    [Header("Slot UI Arrays (크기를 무조건 3으로 설정하세요)")]
    [Tooltip("슬롯 1, 2, 3의 부모 패널 오브젝트")]
    public GameObject[] slotPanels;      
    [Tooltip("슬롯별 레디 버튼")]
    public Button[] readyButtons;        
    [Tooltip("슬롯별 레디 텍스트 (Ready / Ready!)")]
    public Text[] readyTexts;            
    [Tooltip("자신이 누구인지 알려주는 'YOU' 인디케이터 오브젝트")]
    public GameObject[] youIndicators;  

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
    public string gameSceneName = "scLevel"; 

    [Header("Message")]
    public float warningMessageDuration = 2f;

    private bool isChangingScene;
    private Coroutine warningMessageCoroutine;

    // ========================================================
    // [에러 원인 제거 완] SlotOwners 배열 싹 다 지우고 개별 변수로 분리!
    // ========================================================
    [Networked] public PlayerRef Slot0_Owner { get; set; }
    [Networked] public PlayerRef Slot1_Owner { get; set; }
    [Networked] public PlayerRef Slot2_Owner { get; set; }

    [Networked] public NetworkBool Slot0_Ready { get; set; }
    [Networked] public NetworkBool Slot1_Ready { get; set; }
    [Networked] public NetworkBool Slot2_Ready { get; set; }

    private void Start()
    {
        ShowLobbyCharacters();
        HideWarningMessage();
    }

    public override void Spawned()
    {
        Debug.Log("[Lobby] 포톤 로비 동기화 오브젝트 생성 완료.");
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority) return; // 방장(서버)만 관리

        // 1. 방을 나간 유저가 있다면 해당 슬롯 비우기
        if (Slot0_Owner != PlayerRef.None && !Runner.ActivePlayers.Contains(Slot0_Owner)) { Slot0_Owner = PlayerRef.None; Slot0_Ready = false; }
        if (Slot1_Owner != PlayerRef.None && !Runner.ActivePlayers.Contains(Slot1_Owner)) { Slot1_Owner = PlayerRef.None; Slot1_Ready = false; }
        if (Slot2_Owner != PlayerRef.None && !Runner.ActivePlayers.Contains(Slot2_Owner)) { Slot2_Owner = PlayerRef.None; Slot2_Ready = false; }

        // 2. 빈 자리에 순차 배치
        foreach (var player in Runner.ActivePlayers)
        {
            if (Slot0_Owner == player || Slot1_Owner == player || Slot2_Owner == player) continue;

            if (Slot0_Owner == PlayerRef.None) { Slot0_Owner = player; Slot0_Ready = false; }
            else if (Slot1_Owner == PlayerRef.None) { Slot1_Owner = player; Slot1_Ready = false; }
            else if (Slot2_Owner == PlayerRef.None) { Slot2_Owner = player; Slot2_Ready = false; }
        }
    }

    public override void Render()
    {
        int totalActivePlayers = Runner.ActivePlayers.Count();
        int totalReadyPlayers = 0;

        // ----------- [1번 슬롯 갱신] -----------
        bool hasP0 = (Slot0_Owner != PlayerRef.None);
        if (slotPanels[0] != null) slotPanels[0].SetActive(hasP0);
        if (hasP0)
        {
            if (Slot0_Ready) totalReadyPlayers++;
            if (readyTexts[0] != null) readyTexts[0].text = Slot0_Ready ? "Ready!" : "Ready";
            if (readyButtons[0] != null)
            {
                readyButtons[0].colors = UpdateButtonColors(Slot0_Ready);
                readyButtons[0].interactable = (Slot0_Owner == Runner.LocalPlayer); 
            }
            if (youIndicators[0] != null) youIndicators[0].SetActive(Slot0_Owner == Runner.LocalPlayer);
        }

        // ----------- [2번 슬롯 갱신] -----------
        bool hasP1 = (Slot1_Owner != PlayerRef.None);
        if (slotPanels[1] != null) slotPanels[1].SetActive(hasP1);
        if (hasP1)
        {
            if (Slot1_Ready) totalReadyPlayers++;
            if (readyTexts[1] != null) readyTexts[1].text = Slot1_Ready ? "Ready!" : "Ready";
            if (readyButtons[1] != null)
            {
                readyButtons[1].colors = UpdateButtonColors(Slot1_Ready);
                readyButtons[1].interactable = (Slot1_Owner == Runner.LocalPlayer);
            }
            if (youIndicators[1] != null) youIndicators[1].SetActive(Slot1_Owner == Runner.LocalPlayer);
        }

        // ----------- [3번 슬롯 갱신] -----------
        bool hasP2 = (Slot2_Owner != PlayerRef.None);
        if (slotPanels[2] != null) slotPanels[2].SetActive(hasP2);
        if (hasP2)
        {
            if (Slot2_Ready) totalReadyPlayers++;
            if (readyTexts[2] != null) readyTexts[2].text = Slot2_Ready ? "Ready!" : "Ready";
            if (readyButtons[2] != null)
            {
                readyButtons[2].colors = UpdateButtonColors(Slot2_Ready);
                readyButtons[2].interactable = (Slot2_Owner == Runner.LocalPlayer);
            }
            if (youIndicators[2] != null) youIndicators[2].SetActive(Slot2_Owner == Runner.LocalPlayer);
        }

        // ----------- [방장 전용 배틀 버튼 제어] -----------
        if (battleButton != null)
        {
            battleButton.gameObject.SetActive(HasStateAuthority);
            if (HasStateAuthority)
            {
                battleButton.interactable = (totalReadyPlayers >= totalActivePlayers);
            }
        }
    }

    private ColorBlock UpdateButtonColors(bool isReady)
    {
        ColorBlock colors = readyButtons[0].colors; 
        Color targetColor = isReady ? readyColor : notReadyColor;
        colors.normalColor = targetColor;
        colors.highlightedColor = targetColor;
        colors.pressedColor = targetColor;
        colors.selectedColor = targetColor;
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

    public void OnClickBattleButton()
    {
        if (!HasStateAuthority || isChangingScene) return;
        isChangingScene = true;

        Debug.Log("모든 인원 준비 완료. 보스전 레벨로 전체 이동합니다.");
        Runner.LoadScene(gameSceneName, UnityEngine.SceneManagement.LoadSceneMode.Single);
    }

    public void OnClickBackButton()
    {
        if (isChangingScene) return;
        if (Runner != null) Runner.Shutdown();

        isChangingScene = true;

        if (fadeManager != null) fadeManager.ChangeScene(titleSceneName);
        else UnityEngine.SceneManagement.SceneManager.LoadScene(titleSceneName);
    }

    private void ShowLobbyCharacters()
    {
        if (lobbyCharacters == null || lobbyCharacters.Length == 0) return;
        for (int i = 0; i < lobbyCharacters.Length; i++)
        {
            if (lobbyCharacters[i] != null) lobbyCharacters[i].SetActive(true);
        }
    }

    private void ShowWarningMessage(string message)
    {
        if (warningMessageText == null) return;
        if (warningMessageCoroutine != null) StopCoroutine(warningMessageCoroutine);
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
        if (warningMessageText != null) warningMessageText.gameObject.SetActive(false);
    }
}