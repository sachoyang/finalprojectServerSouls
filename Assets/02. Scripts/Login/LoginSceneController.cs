using UnityEngine;
using UnityEngine.UI;

public class LoginSceneController : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject loginPanel;
    [SerializeField] private GameObject registerPanel;

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

    // private const string AccountPasswordPrefix = "Account_Password_";
    // private const string AccountNicknamePrefix = "Account_Nickname_";
    // private const string CurrentLoginIdKey = "CurrentLoginId";
    // private const string CurrentNicknameKey = "CurrentNickname";
    // PlayerPrefab이제 안씀. db용으로 변경

    private void Start()
    {
        ShowLoginPanel();

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
            ShowSystemMessage("아이디 또는 PW를 확인해주세요");
            return;
        }

        // [🔥 DB 연동 부분] 기존 TryLogin 대신 BackendManager를 호출합니다!
        BackendManager.Instance.LoginUser(id, pw, (isSuccess, message) =>
        {
            if (isSuccess)
            {
                // BackendManager 내부에 닉네임과 스킬 정보가 저장되어 있습니다.
                Debug.Log($"DB 로그인 성공 ID: {id}, Nickname: {BackendManager.Instance.CurrentNickname}");
                ChangeScene(titleSceneName);
            }
            else
            {
                Debug.Log($"DB 로그인 실패: {message}");
                ShowSystemMessage(message); // "아이디 또는 비밀번호가 틀렸습니다." 등 출력
            }
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
            ShowSystemMessage("아이디, PW, 닉네임을 모두 입력해주세요");
            return;
        }

        // [🔥 DB 연동 부분] PlayerPrefs 대신 BackendManager로 가입 요청!
        BackendManager.Instance.RegisterUser(id, pw, nickname, (isSuccess, message) =>
        {
            if (isSuccess)
            {
                Debug.Log($"DB 회원가입 완료 ID: {id}, Nickname: {nickname}");
                ShowSystemMessage("계정이 생성되었습니다");

                if (loginIdInput != null) loginIdInput.text = id;
                if (loginPwInput != null) loginPwInput.text = "";

                ShowLoginPanel();
            }
            else
            {
                Debug.Log($"DB 회원가입 실패: {message}");
                ShowSystemMessage(message); // "이미 존재하는 아이디입니다." 등 출력
            }
        });
    }

    public void OnClickRegisterBackButton()
    {
        ShowLoginPanel();
    }

    // private bool TryLogin(string id, string pw)
    // {
    //     string passwordKey = AccountPasswordPrefix + id;

    //     if (!PlayerPrefs.HasKey(passwordKey))
    //     {
    //         Debug.Log("��ϵ��� ���� ���̵�: " + id);
    //         return false;
    //     }

    //     string savedPw = PlayerPrefs.GetString(passwordKey, "");

    //     if (savedPw != pw)
    //     {
    //         Debug.Log("��й�ȣ ����ġ: " + id);
    //         return false;
    //     }

    //     return true;
    // }

    private void ShowLoginPanel()
    {
        if (loginPanel != null)
            loginPanel.SetActive(true);

        if (registerPanel != null)
            registerPanel.SetActive(false);

        HideSystemMessage();
    }

    private void ShowRegisterPanel()
    {
        if (loginPanel != null)
            loginPanel.SetActive(false);

        if (registerPanel != null)
            registerPanel.SetActive(true);

        HideSystemMessage();

        if (registerIdInput != null)
            registerIdInput.Select();
    }

   private void ChangeScene(string sceneName)
    {
        if (fadeManager == null)
        {
            Debug.LogError("LoginSceneController: Fade Manager가 연결되지 않았습니다.");
            return;
        }
        if (string.IsNullOrEmpty(sceneName)) return;

        isChangingScene = true;
        fadeManager.ChangeScene(sceneName);
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