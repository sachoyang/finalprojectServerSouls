using Fusion;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 전투 씬(scServer_stage) 전용 디버그 빠른 입장 GUI (좌상단).
///
/// 목적: 로그인 → 타이틀 → 로비로 이어지는 긴 플로우를 전부 건너뛰고,
///       전투 씬에서 바로 포톤 방을 만들거나(Host) 참가(Client)해서 둘이 붙어 전투를 테스트한다.
///       로그인/DB/로비는 전혀 거치지 않는다. (서버가 꺼져 있어도 동작)
///
/// 사용법: scServer_stage 에 빈 오브젝트를 만들어 이 스크립트를 붙인다. 그게 전부.
///        (이 디버그 진입은 NetworkManager 세션이 없는 단독 실행을 전제로 한다.)
///
/// 버튼:
///   · 방 생성 (Host)  : 현재 씬을 네트워크 씬으로 Host 시작. 코드 비우면 자동 생성해 표시.
///   · 참가 (Client)   : 입력한 코드로 Client 접속 → 방장 씬으로 따라감.
///   · 자동매칭        : AutoHostOrClient (빠른 1:1 테스트용)
///   · 1인 솔로        : GameMode.Single, 포톤 클라우드 없이 완전 오프라인 전투
///
/// Use Random Boss:
///   · OFF(기본) : 지금 열어둔 전투 씬에 그대로 머물며 그 씬의 보스를 스폰 (현재 씬 테스트)
///                  → GameProgressionManager.SetupForDebug 메서드에 의존.
///   · ON        : 정상 플로우처럼 StartFirstLevel 로 랜덤 보스 전용 맵으로 이동
///                  → SetupForDebug 불필요, 순수 이 스크립트만으로 동작.
///
/// * 릴리즈 빌드에는 버튼이 보이지 않습니다(에디터 / 개발용 빌드 전용).
/// * 구분용 userid 는 실행 시 랜덤 생성되어 PlayerPrefs["CurrentNickname"] 에만 들어갑니다.
/// </summary>
public class DebugQuickEntry : MonoBehaviour
{
    [Header("보스 데이터 (씬에 GameProgressionManager 가 없을 때 대비용)")]
    [Tooltip("GameProgressionManager 가 씬에 없거나 bossPool 이 비어 있을 때 사용할 보스 목록.")]
    [SerializeField] private List<BossEncounterData> debugBossPool;

    [Header("보스 선택 방식")]
    [Tooltip("OFF: 지금 열어둔 전투 씬에 그대로 머물며 그 씬 보스 스폰 (현재 씬 테스트)\n" +
             "ON : StartFirstLevel 로 랜덤 보스 전용 맵으로 이동 (정상 플로우)")]
    [SerializeField] private bool useRandomBoss = false;

    [Header("디버그 유저")]
    [Tooltip("플레이어 구분용 임시 ID. 비워두면 실행 시 랜덤 생성됩니다.")]
    [SerializeField] private string debugUserId = "";

    [Header("GUI 위치 / 너비")]
    [SerializeField] private Vector2 guiPosition = new Vector2(10, 10);
    [SerializeField] private float guiWidth = 260f;

    private const string CurrentNicknameKey = "CurrentNickname";

    private NetworkRunner _runner;
    private bool _isConnecting;
    private string _status = "";
    private string _roomCode = "";

    private void Awake()
    {
        if (string.IsNullOrEmpty(debugUserId))
            debugUserId = "Debug_" + UnityEngine.Random.Range(1000, 9999);
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private void OnGUI()
    {
        GUILayout.BeginArea(new Rect(guiPosition.x, guiPosition.y, guiWidth, 300), GUI.skin.box);

        GUILayout.Label("[DEBUG] 전투씬 빠른 입장");
        GUILayout.Label($"User ID : {debugUserId}");
        GUILayout.Space(4);

        GUILayout.Label("방 코드");
        _roomCode = GUILayout.TextField(_roomCode);

        GUILayout.Space(4);
        GUI.enabled = !_isConnecting;

        if (GUILayout.Button("방 생성 (Host)", GUILayout.Height(32)))
            OnClickCreate();

        if (GUILayout.Button("참가 (Client)", GUILayout.Height(32)))
            OnClickJoin();

        GUILayout.Space(2);

        if (GUILayout.Button("자동매칭", GUILayout.Height(28)))
            StartFusion(GameMode.AutoHostOrClient, "");

        if (GUILayout.Button("1인 솔로 (오프라인)", GUILayout.Height(28)))
            StartFusion(GameMode.Single, "");

        GUI.enabled = true;

        if (!string.IsNullOrEmpty(_status))
            GUILayout.Label(_status);

        GUILayout.EndArea();
    }
#endif

    private void OnClickCreate()
    {
        string code = string.IsNullOrEmpty(_roomCode) ? GenerateRoomCode() : _roomCode.Trim().ToUpper();
        _roomCode = code; // 상대에게 알려줄 코드 화면 표시
        StartFusion(GameMode.Host, code);
    }

    private void OnClickJoin()
    {
        string code = _roomCode.Trim().ToUpper();
        if (string.IsNullOrEmpty(code))
        {
            _status = "방 코드를 입력하세요.";
            return;
        }
        StartFusion(GameMode.Client, code);
    }

    // 임시 유저 정보 세팅
    private void ApplyDebugUser()
    {
        PlayerPrefs.SetString(CurrentNicknameKey, debugUserId);
        PlayerPrefs.Save();
        Debug.Log($"[DebugQuickEntry] 디버그 유저 적용: {debugUserId}");
    }

    // ───────────────────────────────────────────────
    // 포톤 세션 시작
    // ───────────────────────────────────────────────
    private async void StartFusion(GameMode mode, string code)
    {
        if (_isConnecting)
            return;

        ApplyDebugUser();
        _isConnecting = true;
        _status = "접속 중...";

        // [중요] 현재 씬을 그대로 테스트하는 경우(useRandomBoss = OFF), BossArenaManager.Spawned() 가
        // StartGame 도중 실행되므로 보스 데이터를 StartGame "이전"에 세팅해야 보스가 스폰됨.
        // 클라이언트는 보스를 스폰하지 않으므로 생략.
        if (!useRandomBoss && mode != GameMode.Client)
        {
            GameProgressionManager gpm = EnsureProgression();
            BossEncounterData boss = PickBossForCurrentScene(gpm);
            gpm.SetupForDebug(1, boss); // ← GameProgressionManager 에 추가해둔 디버그 메서드
            Debug.Log($"[DebugQuickEntry] 현재 씬 보스 세팅: {(boss != null ? boss.bossName : "없음(풀 비어있음)")}");
        }

        // 씬이 바뀌어도 세션이 끊기지 않도록 러너는 별도 GameObject 에 두고 파괴 방지
        GameObject runnerGO = new GameObject("DebugNetworkRunner");
        DontDestroyOnLoad(runnerGO);
        _runner = runnerGO.AddComponent<NetworkRunner>();
        _runner.ProvideInput = true;

        var args = new StartGameArgs()
        {
            GameMode = mode,
            SessionName = code, // 빈 문자열이면 자동
            SceneManager = runnerGO.AddComponent<NetworkSceneManagerDefault>()
        };

        // 클라이언트는 방장 씬을 따라가므로 Scene 미지정.
        // 그 외(호스트/솔로/오토)는 현재 씬을 네트워크 씬으로 등록.
        if (mode != GameMode.Client)
        {
            var sceneInfo = new NetworkSceneInfo();
            sceneInfo.AddSceneRef(
                SceneRef.FromIndex(SceneManager.GetActiveScene().buildIndex),
                LoadSceneMode.Additive);
            args.Scene = sceneInfo;
        }

        Debug.Log($"[DebugQuickEntry] 세션 시작 (Mode={mode}, Code='{code}')");

        var result = await _runner.StartGame(args);

        if (!result.Ok)
        {
            _status = $"접속 실패: {result.ShutdownReason}";
            Debug.LogError($"[DebugQuickEntry] 시작 실패: {result.ShutdownReason}");
            _isConnecting = false;
            Destroy(runnerGO);
            return;
        }

        _status = $"접속 성공! (Server={_runner.IsServer}, Code='{code}')";
        Debug.Log($"[DebugQuickEntry] 성공. IsServer={_runner.IsServer}");

        // 랜덤 보스 모드: 호스트만 StartFirstLevel 호출 (랜덤 보스 + 전용 맵 로드)
        if (useRandomBoss && _runner.IsServer)
        {
            GameProgressionManager gpm = EnsureProgression();
            gpm.StartFirstLevel(_runner);
        }
    }

    // GameProgressionManager 확보 (없으면 디버그용 생성, bossPool 비었으면 채움)
    private GameProgressionManager EnsureProgression()
    {
        GameProgressionManager gpm = GameProgressionManager.Instance;
        if (gpm == null)
        {
            var go = new GameObject("DebugGameProgressionManager");
            gpm = go.AddComponent<GameProgressionManager>(); // Awake 에서 Instance + DontDestroyOnLoad
            Debug.Log("[DebugQuickEntry] GameProgressionManager 가 없어 디버그용으로 생성했습니다.");
        }

        if ((gpm.bossPool == null || gpm.bossPool.Count == 0) &&
            debugBossPool != null && debugBossPool.Count > 0)
        {
            gpm.bossPool = debugBossPool;
        }

        return gpm;
    }

    // 현재 열려 있는 씬에 맞는 보스 찾기 (sceneName 일치 우선, 없으면 첫 번째)
    private BossEncounterData PickBossForCurrentScene(GameProgressionManager gpm)
    {
        string current = SceneManager.GetActiveScene().name;

        List<BossEncounterData> pool =
            (gpm.bossPool != null && gpm.bossPool.Count > 0) ? gpm.bossPool : debugBossPool;

        if (pool == null || pool.Count == 0)
            return null;

        foreach (var b in pool)
        {
            if (b != null && b.sceneName == current)
                return b;
        }

        return pool[0]; // 매칭되는 보스가 없으면 첫 번째 사용
    }

    private string GenerateRoomCode()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        string code = "";
        for (int i = 0; i < 5; i++)
            code += chars[UnityEngine.Random.Range(0, chars.Length)];
        return code;
    }
}