using UnityEngine;
using UnityEngine.UI;

public class TitleManager : MonoBehaviour
{
    [Header("Panels")]
    public GameObject mainTitlePanel;
    public GameObject matchPanel;
    public GameObject optionPanel;

    [Header("Scene Fade")]
    public SceneFadeManager fadeManager;

    [Header("Scene Names")]
    public string lobbySceneName = "scServer_peace";

    // [추가] 유저가 코드를 입력할 인풋필드 연결 슬롯
    [Header("Network UI")]
    public InputField roomCodeInput;

    private bool isChangingScene;

    private void Start()
    {
        ShowMainTitlePanel();
    }

    public void OnClickContinueButton()
    {
        mainTitlePanel.SetActive(false);
        matchPanel.SetActive(true);

        if (optionPanel != null)
            optionPanel.SetActive(false);
    }

    // 기존 CONTINUE 버튼이 이 함수에 연결되어 있을 수 있어서 유지
    public void OnClickPlayButton()
    {
        OnClickContinueButton();
    }

    public void OnClickSystemButton()
    {
        if (optionPanel != null)
            optionPanel.SetActive(true);
    }

    public void OnClickCloseOptionButton()
    {
        if (optionPanel != null)
            optionPanel.SetActive(false);
    }

    public void OnClickReturnButton()
    {
        ShowMainTitlePanel();
    }

    // ========================================================
    // [수정됨] 여기서부터 로컬 씬 이동 대신 NetworkManager를 호출합니다.
    // ========================================================
    public void OnClickAloneButton()
    {
        if (NetworkManager.Instance != null)
            NetworkManager.Instance.StartAlone();
    }

    public void OnClickUniteButton()
    {
        if (NetworkManager.Instance != null)
            NetworkManager.Instance.StartAutoMatch();
    }

    public void OnClickAccessAndInitiateButton()
    {
        string code = "";
        if (roomCodeInput != null)
        {
            code = roomCodeInput.text; // 인풋창에 적힌 텍스트를 가져옴
        }
        
        if (NetworkManager.Instance != null)
            NetworkManager.Instance.StartWithCode(code);
    }
    // ========================================================

    public void OnClickQuitButton()
    {
        Application.Quit();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    private void ShowMainTitlePanel()
    {
        if (mainTitlePanel != null)
            mainTitlePanel.SetActive(true);

        if (matchPanel != null)
            matchPanel.SetActive(false);

        if (optionPanel != null)
            optionPanel.SetActive(false);
    }

    private void ChangeSceneToLobby()
    {
        if (isChangingScene)
            return;

        if (fadeManager == null)
        {
            Debug.LogError("TitleManager: Fade Manager가 연결되지 않았습니다.");
            return;
        }

        isChangingScene = true;
        fadeManager.ChangeScene(lobbySceneName);
    }
}
