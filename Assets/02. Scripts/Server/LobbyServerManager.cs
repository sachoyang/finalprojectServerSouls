using System.Collections;
using System.Collections.Generic;
using System.Linq; // [추가] ActivePlayers.Count() 오류 해결용
using Fusion;
using UnityEngine;
using UnityEngine.UI;

public class LobbyServerManager : NetworkBehaviour
{
    [Header("Character Models")]
    public GameObject[] lobbyCharacters;

    [Header("Ready State")]
    public Button player1ReadyButton;
    public Text player1ReadyButtonText;
    public Button battleButton;
    public Text warningMessageText;

    [Header("Ready Button Color")]
    public Color notReadyColor = Color.white;
    public Color readyColor = new Color(0.4f, 1f, 0.4f);

    [Header("Scene Fade")]
    public SceneFadeManager fadeManager; // 팀원 스크립트 그대로 유지

    [Header("Scene Names")]
    public string titleSceneName = "scTitle uicreate Main";
    public string gameSceneName = "scLevel"; // 인스펙터에서 보스전 씬 이름 다시 확인!

    [Header("Message")]
    public float warningMessageDuration = 2f;

    private bool isPlayer1Ready;
    private bool isChangingScene;
    private Coroutine warningMessageCoroutine;

    [Networked] public int ReadyCount { get; set; }

    private void Start()
    {
        ShowLobbyCharacters();
        SetPlayer1Ready(false);
        HideWarningMessage();
    }

    public override void Spawned()
    {
        Debug.Log("[Lobby] 포톤 로비 동기화 오브젝트 생성 완료.");
    }

    public void OnClickPlayer1ReadyButton()
    {
        SetPlayer1Ready(!isPlayer1Ready);
        RPC_SetReady(Runner.LocalPlayer, isPlayer1Ready);
    }

    public void OnClickBattleButton()
    {
        if (!HasStateAuthority)
        {
            ShowWarningMessage("방장만 게임을 시작할 수 있습니다!");
            return;
        }

        int totalPlayers = Runner.ActivePlayers.Count();

        if (ReadyCount >= totalPlayers || Runner.GameMode == GameMode.Host)
        {
            if (isChangingScene) return;
            isChangingScene = true;

            Debug.Log("보스 메인 레벨씬으로 멀티플레이 이동을 시작합니다.");
            
            // [멀티플레이 전용 이동] 
            // 팀원의 FadeManager는 내부적으로 싱글 씬 로드를 쓰기 때문에 포톤과 충돌합니다.
            // 따라서 배틀 시작 시에는 즉시 네트워크 서버 이동 명령을 내립니다.
            Runner.LoadScene(gameSceneName, UnityEngine.SceneManagement.LoadSceneMode.Single);
        }
        else
        {
            ShowWarningMessage("모든 플레이어가 준비(Ready)해야 시작할 수 있습니다!");
        }
    }

    public void OnClickBackButton()
    {
        if (isChangingScene) return;

        if (Runner != null)
        {
            Runner.Shutdown(); // 포톤 접속 정상 종료 (연결 끊기)
        }

        isChangingScene = true;

        // [팀원 코드 사용 구역]
        // 뒤로가기는 서버 연동이 필요 없으므로 팀원분의 페이드 이동 로직을 그대로 씁니다.
        if (fadeManager != null)
        {
            fadeManager.ChangeScene(titleSceneName); 
        }
        else
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(titleSceneName);
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_SetReady(PlayerRef player, bool isReady)
    {
        if (isReady) ReadyCount++;
        else ReadyCount = Mathf.Max(0, ReadyCount - 1);
    }

    private void ShowLobbyCharacters()
    {
        if (lobbyCharacters == null || lobbyCharacters.Length == 0) return;
        for (int i = 0; i < lobbyCharacters.Length; i++)
        {
            if (lobbyCharacters[i] != null) lobbyCharacters[i].SetActive(true);
        }
    }

    private void SetPlayer1Ready(bool ready)
    {
        isPlayer1Ready = ready;
        if (player1ReadyButtonText != null)
            player1ReadyButtonText.text = ready ? "Ready!" : "Ready";

        if (player1ReadyButton != null)
        {
            ColorBlock colors = player1ReadyButton.colors;
            colors.normalColor = ready ? readyColor : notReadyColor;
            colors.highlightedColor = ready ? readyColor : notReadyColor;
            colors.pressedColor = ready ? readyColor : notReadyColor;
            colors.selectedColor = ready ? readyColor : notReadyColor;
            player1ReadyButton.colors = colors;
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