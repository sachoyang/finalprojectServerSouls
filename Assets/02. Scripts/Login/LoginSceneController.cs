using UnityEngine;
using UnityEngine.UI;

public class LoginSceneController : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private InputField idInput;
    [SerializeField] private InputField pwInput;

    [Header("Message")]
    [SerializeField] private Text systemMessageText;

    [Header("Scene Fade")]
    [SerializeField] private SceneFadeManager fadeManager;

    [Header("Scene Names")]
    [SerializeField] private string titleSceneName = "scTitle uicreate Main";

    private bool isChangingScene;

    private const string AccountPrefix = "Account_";
    private const string CurrentLoginIdKey = "CurrentLoginId";

    private void Start()
    {
        if (systemMessageText != null)
            systemMessageText.gameObject.SetActive(false);

        if (pwInput != null)
            pwInput.contentType = InputField.ContentType.Password;

        if (idInput != null)
            idInput.Select();
    }

    public void OnClickLoginButton()
    {
        if (isChangingScene)
            return;

        string id = idInput != null ? idInput.text.Trim() : "";
        string pw = pwInput != null ? pwInput.text.Trim() : "";

        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(pw))
        {
            ShowSystemMessage("아이디 또는 PW를 확인해주세요");
            return;
        }

        bool loginSuccess = TryLoginOrRegister(id, pw);

        if (!loginSuccess)
        {
            ShowSystemMessage("아이디 또는 PW를 확인해주세요");
            Debug.Log("로그인 실패: " + id);
            return;
        }

        PlayerPrefs.SetString(CurrentLoginIdKey, id);
        PlayerPrefs.Save();

        Debug.Log("CurrentLoginId 저장됨: " + PlayerPrefs.GetString(CurrentLoginIdKey));

        ChangeScene(titleSceneName);
    }

    private bool TryLoginOrRegister(string id, string pw)
    {
        string accountKey = AccountPrefix + id;

        if (!PlayerPrefs.HasKey(accountKey))
        {
            PlayerPrefs.SetString(accountKey, pw);
            PlayerPrefs.Save();

            Debug.Log("새 계정 등록: " + id);
            return true;
        }

        string savedPw = PlayerPrefs.GetString(accountKey, "");

        if (savedPw == pw)
        {
            Debug.Log("기존 계정 로그인 성공: " + id);
            return true;
        }

        Debug.Log("비밀번호 불일치: " + id);
        return false;
    }

    private void ChangeScene(string sceneName)
    {
        if (fadeManager == null)
        {
            Debug.LogError("LoginSceneController: Fade Manager가 연결되지 않았습니다.");
            return;
        }

        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError("LoginSceneController: 이동할 씬 이름이 비어 있습니다.");
            return;
        }

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
}