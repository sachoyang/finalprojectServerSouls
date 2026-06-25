using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LoginSceneController : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject loginPanel;
    [SerializeField] private GameObject registerPanel;
    [SerializeField] private GameObject guestNoticePanel;

    [Header("Login Input")]
    [SerializeField] private InputField loginIdInput;
    [SerializeField] private InputField loginPwInput;

    [Header("Register Input")]
    [SerializeField] private InputField registerIdInput;
    [SerializeField] private InputField registerPwInput;
    [SerializeField] private InputField registerNicknameInput;

    [Header("Message")]
    [SerializeField] private Text systemMessageText;

    [Header("Scene Fade")]
    [SerializeField] private SceneFadeManager fadeManager;

    [Header("Scene Names")]
    [SerializeField] private string titleSceneName = "scTitle uicreate Main";

    private bool isChangingScene;

    public const string PlayerAccessModeKey = "PlayerAccessMode";
    public const string LoggedInModeValue = "LoggedIn";
    public const string GuestModeValue = "Guest";

    private void Start()
    {
        GameManager.GetOrCreate()?.SetLoginMode();

        ShowLoginPanel();
        HideGuestNotice();

        if (loginPwInput != null)
            loginPwInput.contentType = InputField.ContentType.Password;

        if (registerPwInput != null)
            registerPwInput.contentType = InputField.ContentType.Password;

        if (loginIdInput != null)
            loginIdInput.Select();
    }

    public void OnClickLoginButton()
    {
        if (isChangingScene)
            return;

        string id = loginIdInput != null ? loginIdInput.text.Trim() : "";
        string pw = loginPwInput != null ? loginPwInput.text.Trim() : "";

        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(pw))
        {
            ShowSystemMessage("ID 또는 PW를 확인해주세요.");
            return;
        }

        GameManager gameManager = GameManager.GetOrCreate();
        BackendManager backendManager = gameManager != null ? gameManager.Backend : null;
        AbilityManager abilityManager = gameManager != null ? gameManager.Ability : null;

        if (backendManager == null || abilityManager == null)
        {
            ShowSystemMessage("필수 매니저 초기화에 실패했습니다.");
            return;
        }

        ShowSystemMessage("로그인 중입니다...");

        backendManager.LoginUser(id, pw, (isSuccess, message) =>
        {
            if (!isSuccess)
            {
                ShowSystemMessage(message);
                return;
            }

            ShowSystemMessage("스킬 데이터를 불러오는 중입니다...");

            abilityManager.FetchAbilities((isLoaded) =>
            {
                if (!isLoaded)
                {
                    ShowSystemMessage("스킬 데이터를 불러오는데 실패했습니다.");
                    return;
                }

                PlayerPrefs.SetString(PlayerAccessModeKey, LoggedInModeValue);
                PlayerPrefs.Save();

                ChangeScene(titleSceneName);
            });
        });
    }

    public void OnClickOpenRegisterPanelButton()
    {
        ShowRegisterPanel();
    }

    public void OnClickRegisterButton()
    {
        string id = registerIdInput != null ? registerIdInput.text.Trim() : "";
        string pw = registerPwInput != null ? registerPwInput.text.Trim() : "";
        string nickname = registerNicknameInput != null ? registerNicknameInput.text.Trim() : "";

        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(pw) || string.IsNullOrEmpty(nickname))
        {
            ShowSystemMessage("ID, PW, 닉네임을 모두 입력해주세요.");
            return;
        }

        GameManager gameManager = GameManager.GetOrCreate();
        BackendManager backendManager = gameManager != null ? gameManager.Backend : null;

        if (backendManager == null)
        {
            ShowSystemMessage("서버 매니저 초기화에 실패했습니다.");
            return;
        }

        ShowSystemMessage("회원가입 중입니다...");

        backendManager.RegisterUser(id, pw, nickname, (isSuccess, message) =>
        {
            if (!isSuccess)
            {
                ShowSystemMessage(message);
                return;
            }

            ShowSystemMessage("계정이 생성되었습니다.");

            if (loginIdInput != null)
                loginIdInput.text = id;

            if (loginPwInput != null)
                loginPwInput.text = "";

            ShowLoginPanel();
        });
    }

    public void OnClickRegisterBackButton()
    {
        ShowLoginPanel();
    }

    public void OnClickGuestButton()
    {
        if (isChangingScene)
            return;

        HideSystemMessage();
        ShowGuestNotice();
    }

    public void OnClickGuestConfirmYes()
    {
        if (isChangingScene)
            return;

        PlayerPrefs.SetString(PlayerAccessModeKey, GuestModeValue);
        PlayerPrefs.Save();

        ChangeScene(titleSceneName);
    }

    public void OnClickGuestConfirmNo()
    {
        HideGuestNotice();
    }

    private void ShowLoginPanel()
    {
        if (loginPanel != null)
            loginPanel.SetActive(true);

        if (registerPanel != null)
            registerPanel.SetActive(false);

        HideGuestNotice();
        HideSystemMessage();
    }

    private void ShowRegisterPanel()
    {
        if (loginPanel != null)
            loginPanel.SetActive(false);

        if (registerPanel != null)
            registerPanel.SetActive(true);

        HideGuestNotice();
        HideSystemMessage();

        if (registerIdInput != null)
            registerIdInput.Select();
    }

    private void ShowGuestNotice()
    {
        if (guestNoticePanel != null)
            guestNoticePanel.SetActive(true);
    }

    private void HideGuestNotice()
    {
        if (guestNoticePanel != null)
            guestNoticePanel.SetActive(false);
    }

    private void ChangeScene(string sceneName)
    {
        if (isChangingScene)
            return;

        if (string.IsNullOrEmpty(sceneName))
        {
            ShowSystemMessage("이동할 씬 이름이 비어 있습니다.");
            return;
        }

        isChangingScene = true;

        if (fadeManager != null)
        {
            fadeManager.ChangeScene(sceneName);
            return;
        }

        SceneManager.LoadScene(sceneName);
    }

    private void ShowSystemMessage(string message)
    {
        if (systemMessageText == null)
            return;

        systemMessageText.gameObject.SetActive(true);
        systemMessageText.text = message;
    }

    private void HideSystemMessage()
    {
        if (systemMessageText != null)
            systemMessageText.gameObject.SetActive(false);
    }
}