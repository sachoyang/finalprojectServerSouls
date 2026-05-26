using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class LobbyManager : MonoBehaviour
{
    [Header("Character Models")]
    public GameObject[] lobbyCharacters;

    [Header("Player Info")]
    public Text playerNameText;

    [Header("Ready State")]
    public Button player1ReadyButton;
    public Text player1ReadyButtonText;
    public Button battleButton;
    public Text warningMessageText;

    [Header("Ready Button Color")]
    public Color notReadyColor = Color.white;
    public Color readyColor = new Color(0.4f, 1f, 0.4f);

    [Header("Scene Fade")]
    public SceneFadeManager fadeManager;

    [Header("Scene Names")]
    public string titleSceneName = "scTitle uicreate Main";
    public string gameSceneName = "scServer";

    [Header("Message")]
    public float warningMessageDuration = 2f;

    private bool isPlayer1Ready;
    private bool isChangingScene;
    private Coroutine warningMessageCoroutine;

    private const string CurrentNicknameKey = "CurrentNickname";

    private void Start()
    {
        Debug.Log("LobbyManager Start 실행됨");

        ShowLobbyCharacters();
        SetPlayer1Ready(false);
        HideWarningMessage();

        StartCoroutine(UpdatePlayerNameAfterSceneReady());
    }

    public void OnClickPlayer1ReadyButton()
    {
        SetPlayer1Ready(!isPlayer1Ready);
    }

    public void OnClickBattleButton()
    {
        if (!isPlayer1Ready)
        {
            ShowWarningMessage("준비하지 않은 플레이어가 있습니다.");
            return;
        }

        ChangeScene(gameSceneName);
    }

    public void OnClickBackButton()
    {
        ChangeScene(titleSceneName);
    }

    [ContextMenu("Debug/Refresh Player Name")]
    public void UpdatePlayerName()
    {
        if (playerNameText == null)
        {
            Debug.LogError("LobbyManager: Player Name Text가 연결되지 않았습니다.");
            return;
        }

        bool hasNickname = PlayerPrefs.HasKey(CurrentNicknameKey);
        string nickname = PlayerPrefs.GetString(CurrentNicknameKey, "Player");

        Debug.Log("LobbyManager: CurrentNickname 존재 여부 = " + hasNickname);
        Debug.Log("LobbyManager: Lobby에서 읽은 CurrentNickname = " + nickname);

        playerNameText.text = nickname;
    }

    private IEnumerator UpdatePlayerNameAfterSceneReady()
    {
        yield return null;
        UpdatePlayerName();

        yield return new WaitForSeconds(0.1f);
        UpdatePlayerName();

        yield return new WaitForSeconds(0.5f);
        UpdatePlayerName();
    }

    private void ShowLobbyCharacters()
    {
        if (lobbyCharacters == null || lobbyCharacters.Length == 0)
        {
            Debug.LogWarning("LobbyManager: Lobby Characters가 비어 있습니다.");
            return;
        }

        for (int i = 0; i < lobbyCharacters.Length; i++)
        {
            if (lobbyCharacters[i] != null)
                lobbyCharacters[i].SetActive(true);
        }
    }

    private void SetPlayer1Ready(bool isReady)
    {
        isPlayer1Ready = isReady;

        if (player1ReadyButtonText != null)
            player1ReadyButtonText.text = isPlayer1Ready ? "Ready!" : "Ready";

        if (player1ReadyButton != null)
        {
            ColorBlock colors = player1ReadyButton.colors;
            colors.normalColor = isPlayer1Ready ? readyColor : notReadyColor;
            colors.highlightedColor = isPlayer1Ready ? readyColor : notReadyColor;
            colors.pressedColor = isPlayer1Ready ? readyColor : notReadyColor;
            colors.selectedColor = isPlayer1Ready ? readyColor : notReadyColor;
            player1ReadyButton.colors = colors;
        }
    }

    private void ShowWarningMessage(string message)
    {
        if (warningMessageText == null)
        {
            Debug.LogWarning("LobbyManager: Warning Message Text가 연결되지 않았습니다.");
            return;
        }

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

    private void ChangeScene(string sceneName)
    {
        if (isChangingScene)
            return;

        if (fadeManager == null)
        {
            Debug.LogError("LobbyManager: Fade Manager가 연결되지 않았습니다.");
            return;
        }

        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError("LobbyManager: 이동할 씬 이름이 비어 있습니다.");
            return;
        }

        isChangingScene = true;
        fadeManager.ChangeScene(sceneName);
    }
}