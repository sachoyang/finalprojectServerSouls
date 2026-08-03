using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement; // LoadSceneMode 사용을 위해 필수

// ISessionGuard 구현: 백엔드를 직접 모른 채 '세션 만료 시 네트워크 종료'만 책임
public class NetworkManager : MonoSingleton<NetworkManager>, ISessionGuard // 제네릭 모노싱글톤 상속
{
    // 기획상 한 방의 최대 인원. 로비 슬롯(LobbyServerManager의 Slot0~2)이 3개뿐이므로
    // 4번째 이상이 들어오면 슬롯을 못 받아 유령 인원이 된다. 세션 정원 자체를 여기서 잠근다.
    public const int MaxPlayers = 3;

    private NetworkRunner _runner;
    public NetworkRunner Runner => _runner;

    [Header("씬 이름 설정")]
    [Tooltip("로비 씬의 정확한 이름을 적어주세요.")]
    public string lobbySceneName = "scLobbyMain";

    // 구형 싱글톤 보일러플레이트 제거 (Instance/중복파괴/DontDestroyOnLoad는 베이스가 처리)
    protected override void Awake()
    {
        base.Awake(); // 베이스 싱글톤 초기화 먼저
        if (Instance != this) return; // 중복 인스턴스(파괴 예정)면 가드 부착 생략

        // 세션 가드 부착: 백엔드 의존을 SessionGuard로 격리 (NetworkManager는 BackendManager를 모름)
        SessionGuard guard = gameObject.AddComponent<SessionGuard>();
        guard.Initialize(this);
    }

    // ==========================================
    // ISessionGuard: 세션 만료 시 실행될 강제 종료 함수 (SessionGuard가 호출)
    // ==========================================
    public async void HandleSessionExpired()
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

    // ==========================================
    // F9: Fusion Statistics 실시간 네트워크 통계 패널 토글
    //  - 세션이 켜져 있는 동안(로비~보스~Path 전부) 어디서든 사용 가능
    //  - 에디터/개발 빌드: 항상 허용 / 릴리즈 빌드: admin 계정만 (DebugHotkey와 동일 정책)
    // ==========================================
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F9) && StatsToggleAllowed &&
            _runner != null && _runner.IsRunning)
        {
            ToggleFusionStatistics();
        }
    }

    private void ToggleFusionStatistics()
    {
        var stats = _runner.GetComponent<Fusion.Statistics.FusionStatistics>();
        if (stats == null)
        {
            // 런타임 추가 시에는 Spawned가 자동으로 안 오므로 SetupStatisticsPanel을 직접 호출.
            // (내부에서 runner.AddGlobal로 자가 등록 → Spawned → 패널 생성 순서로 이어짐)
            stats = _runner.gameObject.AddComponent<Fusion.Statistics.FusionStatistics>();
            stats.SetupStatisticsPanel();
            Debug.Log("[NetworkManager] F9: Fusion Statistics 패널을 켰습니다.");
        }
        else if (stats.IsPanelActive)
        {
            stats.DestroyStatisticsPanel();
        }
        else
        {
            stats.SetupStatisticsPanel();
        }
    }

    private static bool StatsToggleAllowed
    {
        get
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            return true;
#else
            return BackendManager.HasInstance && BackendManager.Instance.IsAdminAccount;
#endif
        }
    }

    // ==========================================
    // 세션을 깔끔히 종료하고 지정 씬으로 이동한다. (엔딩 → 타이틀 복귀 등에서 사용)
    //  HandleSessionExpired 와 동일한 셧다운 패턴을 쓰되, 목적지 씬만 바꿀 수 있게 분리했다.
    // ==========================================
    public async void ShutdownAndLoad(string sceneName)
    {
        if (_runner != null && _runner.IsRunning)
        {
            await _runner.Shutdown();
        }
        _runner = null; // 다음 세션에서 새 러너를 붙일 수 있도록 참조 정리

        if (!string.IsNullOrEmpty(sceneName))
        {
            SceneManager.LoadScene(sceneName);
        }
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
            // 🔒 정원 3명. 이 값을 넘기지 않으면 NetworkProjectConfig의 기본값(10명)이 적용되어
            //    4명째부터도 그냥 입장돼 버린다. 정원이 차면 포톤이 접속 자체를 거절한다.
            PlayerCount = MaxPlayers,
            SceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>()
        });

        if (!result.Ok)
        {
            // 방이 꽉 찼거나(정원 초과) 코드가 틀린 경우. 죽은 러너를 그대로 두면 다음 시도 때
            // 재사용되어 계속 실패하므로 반드시 정리하고 참조를 비운다.
            Debug.LogWarning($"[NetworkManager] 세션 진입 실패: {result.ShutdownReason}");

            if (_runner != null && !_runner.IsShutdown)
            {
                await _runner.Shutdown();
            }
            _runner = null;
            return;
        }

        Debug.Log("[NetworkManager] 세션 진입 성공!");
        if (_runner.IsServer)
        {
            // [오류 해결] 씬 이름을 문자열로 직접 던지면 깔끔하게 넘어갑니다!
            await _runner.LoadScene(lobbySceneName, LoadSceneMode.Single);
        }
    }
}