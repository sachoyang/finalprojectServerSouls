using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement; // LoadSceneMode 사용을 위해 필수

public class NetworkManager : MonoBehaviour
{
    private static NetworkManager _instance;
    public static NetworkManager Instance => _instance;

    private NetworkRunner _runner;
    public NetworkRunner Runner => _runner;

    [Header("씬 이름 설정")]
    [Tooltip("로비 씬의 정확한 이름을 적어주세요.")]
    public string lobbySceneName = "scLobbyMain";

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void StartAlone()
    {
        StartSession(GameMode.Host, "ALONE_" + Random.Range(1000, 9999));
    }

    public void StartAutoMatch()
    {
        StartSession(GameMode.AutoHostOrClient, "");
    }

    public void StartWithCode(string roomCode)
    {
        if (string.IsNullOrEmpty(roomCode))
        {
            roomCode = GenerateRandomCode();
        }
        StartSession(GameMode.AutoHostOrClient, roomCode.ToUpper());
    }

    public string GenerateRandomCode()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        string code = "";
        for (int i = 0; i < 5; i++)
        {
            code += chars[Random.Range(0, chars.Length)];
        }
        return code;
    }

    private async void StartSession(GameMode mode, string sessionName)
    {
        if (_runner == null)
        {
            _runner = gameObject.AddComponent<NetworkRunner>();
            _runner.ProvideInput = true;
        }

        Debug.Log($"[NetworkManager] 포톤 접속 시도.. 모드: {mode}, 방제: {sessionName}");

        var result = await _runner.StartGame(new StartGameArgs()
        {
            GameMode = mode,
            SessionName = sessionName,
            SceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>()
        });

        if (result.Ok)
        {
            Debug.Log("[NetworkManager] 세션 진입 성공!");
            if (_runner.IsServer)
            {
                // [오류 해결] 씬 이름을 문자열로 직접 던지면 깔끔하게 넘어갑니다!
                _runner.LoadScene(lobbySceneName, LoadSceneMode.Single);
            }
        }
    }
}