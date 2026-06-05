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

    // ==========================================
    // 백엔드의 중복 접속 강제 퇴장 명령 듣기
    // ==========================================
    private void Start()
    {
        // 백엔드 매니저가 존재하면 이벤트 구독
        if (BackendManager.Instance != null)
        {
            BackendManager.Instance.OnSessionExpired += HandleSessionExpired;
        }
    }

    private void OnDestroy()
    {
        // 오브젝트 파괴 시 이벤트 구독 해제 (메모리 누수 방지)
        if (BackendManager.Instance != null)
        {
            BackendManager.Instance.OnSessionExpired -= HandleSessionExpired;
        }
    }

    // 세션 만료 시 실행될 강제 종료 함수
    private async void HandleSessionExpired()
    {
        Debug.LogWarning("[NetworkManager] 중복 접속 감지! 네트워크를 끊고 타이틀로 강제 이동합니다.");

        // 1. 퓨전 네트워크가 돌아가고 있다면 즉시 셧다운!
        if (_runner != null && _runner.IsRunning)
        {
            await _runner.Shutdown();
            _runner = null; // 초기화
        }

        // 2. 타이틀(로그인) 씬으로 쫓아내기 
        // (LobbyServerManager에 적어두셨던 타이틀 씬 이름 적용!)
        SceneManager.LoadScene("scLogin"); 
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
                await _runner.LoadScene(lobbySceneName, LoadSceneMode.Single);
            }
        }
    }
}