using UnityEngine;
using System.Collections.Generic;
using Fusion;
using UnityEngine.SceneManagement; // 씬 로드 이벤트 구독용

public class GameProgressionManager : MonoBehaviour // 런(Run) 동안 유지되는 준영속 매니저
{
    public static GameProgressionManager Instance { get; private set; } // 정적 인스턴스

    [Header("등장 가능한 보스 풀 (랜덤 추첨)")]
    public List<BossEncounterData> bossPool;

    [Header("런(Run) 길이 설정")]
    [Tooltip("이번 런의 최대 층수 = 마지막 보스가 나오는 층.\n" +
             "이 층의 보스를 잡으면 (Path 씬을 거쳐) 엔딩으로 넘어간다. 보스 종류가 늘어나면 이 값만 올리면 됨.")]
    [Min(1)]
    public int maxLevel = 3;

    // 동기화할 필요 없습니다! 방장(Host)이 여기서 계산하고 알아서 세팅합니다.
    public int CurrentLevel { get; private set; } = 1;
    public BossEncounterData CurrentBossData { get; private set; }

    // 지금 있는 층이 마지막 보스층인가? (이 층을 클리어하면 엔딩)
    public bool IsFinalLevel => CurrentLevel >= maxLevel;

    // 다음에 올라갈 층이 마지막 보스층인가? (보스 클리어 직후 Path 씬을 고를 때 사용 → scPathLast)
    public bool IsNextLevelFinal => CurrentLevel + 1 >= maxLevel;

    // ==========================================
    // 🕒 [전투 소요 시간] 이번 런에서 '전투(보스) 씬'에 머문 시간만 누적한다.
    //  - Path 씬(scPath/scPathLast)·로비·엔딩·타이틀은 측정 제외 (아래 IsCombatScene 참고)
    //  - 클리어(scEnding) 씬에서 GameProgressionManager.Instance.RunCombatSeconds로 읽어 표시
    //  - 죽어서 로비로 돌아가면 ResetRun()에서 0으로 초기화된다
    // ==========================================
    public float RunCombatSeconds { get; private set; }
    private bool _measuringCombatTime;

    // 게스트는 호스트가 내려주는 값만 쓰고 스스로는 누적하지 않는다(중복/드리프트 방지).
    //  BossArenaManager가 호스트→게스트로 시간을 내려줄 때 true로 켜진다(게스트에서만 호출됨).
    private bool _hostDrivesTime;

    // ==========================================
    // 보스 추첨용 '셔플백(가방)'.
    //  완전 랜덤이 아니라, bossPool의 보스가 전부 한 번씩 나올 때까지 중복 없이 뽑는다.
    //  (예: 보스 5마리 & 5층이면 1~5층에 5마리가 전부 한 번씩 등장)
    //  가방이 비면 다시 풀 전체로 채워서 반복 → 6층부터는 새 가방에서 다시 랜덤.
    // ==========================================
    private readonly List<BossEncounterData> _bossBag = new List<BossEncounterData>();

    [Header("씬 이름 (생명주기 기준)")]
    [Tooltip("세션을 유지한 채 복귀하는 로비 씬. 이 씬이 로드되면 런 상태를 초기화(ResetRun)한다.")]
    public string lobbySceneName = "scLobbyMain";
    [Tooltip("세션이 완전히 끝나고 돌아가는 타이틀 씬. 이 씬이 로드되면 매니저 자체를 파괴한다.")]
    public string titleSceneName = "scTitle uicreate Main";

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject); // 런(Run) 유지용 (scPath 등 전환 시 생존)
        SceneManager.sceneLoaded += OnSceneLoaded; // 복귀 감지용 구독
    }

    // 씬 로드 기준 생명주기 정리.
    //  - 로비 복귀(세션 유지, 같은 인원) → 런 상태만 초기화하고 매니저는 유지 (모든 클라이언트에서 각자 실행됨)
    //  - 타이틀 복귀(세션 종료)         → 매니저 파괴
    //  🔥 [버그 픽스] 기존엔 "scLobby"라는 존재하지 않는 씬 이름을 검사해 로비 복귀 시 아무 정리도 안 됐다.
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 🕒 로드된 씬이 전투(보스) 씬일 때만 시간 측정을 켠다. 그 외(Path/로비/엔딩/타이틀)는 멈춤.
        _measuringCombatTime = IsCombatScene(scene.name);

        if (scene.name == titleSceneName)
        {
            Destroy(gameObject);
        }
        else if (scene.name == lobbySceneName)
        {
            ResetRun();
        }
    }

    private void Update()
    {
        // 🕒 전투 씬에 있는 동안만 소요 시간을 누적한다. (일시정지로 timeScale=0이면 자동으로 안 쌓임)
        //    게스트는 호스트 값만 받으므로 자가 누적하지 않는다.
        if (_measuringCombatTime && !_hostDrivesTime)
            RunCombatSeconds += Time.deltaTime;
    }

    // ==========================================
    // 게임오버 → 로비 복귀 시 '런(Run) 단위' 상태 일괄 청소.
    //  세션(NetworkRunner)과 영속 매니저(사운드/풀/설정/로그인)는 유지하고,
    //  이번 런에서만 유효했던 것들을 버린다. 새 항목이 생기면 반드시 여기에 추가할 것!
    // ==========================================
    public void ResetRun()
    {
        CurrentLevel = 1;
        CurrentBossData = null;
        _bossBag.Clear(); // 새 런에서는 새 가방으로 다시 추첨

        // 🕒 죽어서 로비로 돌아온 것이므로 전투 소요 시간도 리셋한다.
        RunCombatSeconds = 0f;
        _measuringCombatTime = false;
        // 호스트 주도 여부는 다음 보스 씬에서 BossArenaManager가 다시 결정한다(역할 변경 대비).
        _hostDrivesTime = false;

        // 아직 재생 중이던 전투 이펙트가 로비까지 끌려오지 않게 전부 풀로 회수
        if (EffectPoolManager.Instance != null)
            EffectPoolManager.Instance.ReturnAllActive();

        // 🔥 마우스 커서 복구. 게임오버 화면에서 켠 ForceCursorVisible(static)을 여기서 내려줘야
        //    다음 런의 전투 씬에서 카메라가 커서를 다시 정상적으로 잠글 수 있다.
        //    로비에는 커서를 잠그는 카메라가 없으므로 여기서 풀어둔 상태가 그대로 유지된다.
        ThirdPersonCameraController.ForceCursorVisible = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // ▼▼▼ [플레이어팀 작업 필요] ▼▼▼
        // PlayerSessionStore에 ClearAll()을 추가하고 아래 주석을 해제해 주세요.
        // static 저장소 3종(스탯 스냅샷 / 어빌리티 목록 / 보상 선택 기록)을 비워야
        // 다음 런에서 이전 런의 체력·어빌리티가 복원되는 버그가 사라집니다.
        // PlayerSessionStore.ClearAll();
        // ▲▲▲ [플레이어팀 작업 필요] ▲▲▲

        Debug.Log("[GameProgressionManager] 런 상태 초기화 완료 (로비 복귀).");
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded; // 핸들러 누수 방지
    }

    // 로비에서 레디가 끝나면 호출됨
    public void StartFirstLevel(NetworkRunner runner)
    {
        CurrentLevel = 1;
        _bossBag.Clear(); // 런 시작은 항상 새 가방으로 (로비를 안 거치는 디버그 진입 대비)
        CurrentBossData = null;
        RunCombatSeconds = 0f; // 🕒 새 런 시작이므로 소요 시간도 0에서 시작
        LoadNextRandomLevel(runner);
    }

    // 다음 층으로 넘어갈 때 호출됨
    public void LoadNextRandomLevel(NetworkRunner runner)
    {
        if (bossPool == null || bossPool.Count == 0) return;

        // 1. 셔플백에서 보스 뽑기 (가방이 빌 때까지 중복 없음)
        CurrentBossData = DrawBossFromBag();

        Debug.Log($"=== [통제실] {CurrentLevel}층 진입! 출현 보스: {CurrentBossData.bossName}, 맵: {CurrentBossData.sceneName} ===");

        // 🔥 [추가됨] 뽑힌 보스의 테마곡(BGM)을 SoundManager에게 재생하라고 넘겨줌!
        if (CurrentBossData.stageMainBGM != null && SoundManager.Instance != null)
        {
            SoundManager.Instance.PlayBGM(CurrentBossData.stageMainBGM);
        }

        // 2. 뽑힌 보스의 전용 맵(씬)으로 플레이어 전원 강제 이동!
        runner.LoadScene(CurrentBossData.sceneName, UnityEngine.SceneManagement.LoadSceneMode.Single);
    }

    public void GoToNextLevel(NetworkRunner runner)
    {
        CurrentLevel++;
        LoadNextRandomLevel(runner);
    }

    // 셔플백에서 보스 1마리 추첨.
    //  - 가방이 비어 있으면 bossPool 전체로 다시 채운다 (다음 순회 시작)
    //  - 새로 채울 때, 직전 층 보스가 연속으로 나오는 것만 1회 방지 (가방 경계에서 같은 보스 2연속 방지)
    private BossEncounterData DrawBossFromBag()
    {
        if (_bossBag.Count == 0)
        {
            _bossBag.AddRange(bossPool);

            // 직전 가방의 마지막 보스가 새 가방의 첫 추첨에서 또 나오지 않게 임시 제외
            // (풀이 2마리 이상일 때만. 1마리뿐이면 어쩔 수 없이 그대로 나온다)
            if (CurrentBossData != null && _bossBag.Count > 1 && _bossBag.Contains(CurrentBossData))
            {
                _bossBag.Remove(CurrentBossData);
                BossEncounterData drawn = TakeRandomFromBag();
                _bossBag.Add(CurrentBossData); // 다음 층부터는 다시 후보에 포함
                return drawn;
            }
        }

        return TakeRandomFromBag();
    }

    private BossEncounterData TakeRandomFromBag()
    {
        int randomIndex = Random.Range(0, _bossBag.Count);
        BossEncounterData drawn = _bossBag[randomIndex];
        _bossBag.RemoveAt(randomIndex);
        return drawn;
    }

    // 클라이언트가 방장의 층수를 받아와서 강제로 동기화하는 함수
    public void SetLevelFromHost(int level)
    {
        CurrentLevel = level;
    }

    // 🕒 게스트가 호스트의 전투 소요 시간을 그대로 받아 맞춘다.
    //  같이 플레이한 사람 모두 엔딩에서 같은 시간을 봐야 하므로 '호스트 값'이 기준.
    //  (BossArenaManager가 층수처럼 매 틱 호스트→게스트로 흘려보낸다)
    public void SetRunSecondsFromHost(float seconds)
    {
        _hostDrivesTime = true; // 이 매니저는 게스트 → 자가 누적을 끄고 호스트 값만 따른다
        RunCombatSeconds = seconds;
    }

    // ==========================================
    // 🕒 로드된 씬이 '전투(보스) 씬'인지 판별.
    //  보스 풀(bossPool)에 등록된 sceneName 중 하나면 전투 씬으로 본다.
    //  → 경유 씬(scPath/scPathLast)·로비·엔딩(scEnding)·타이틀은 자동으로 제외되어 측정되지 않는다.
    // ==========================================
    private bool IsCombatScene(string sceneName)
    {
        if (bossPool == null || string.IsNullOrEmpty(sceneName))
            return false;

        for (int i = 0; i < bossPool.Count; i++)
        {
            if (bossPool[i] != null && bossPool[i].sceneName == sceneName)
                return true;
        }

        return false;
    }

    // 🕒 엔딩/결과 화면 표시용 문자열. 1시간 이상이면 h:mm:ss, 아니면 mm:ss.
    public string GetRunCombatTimeText()
    {
        int total = Mathf.FloorToInt(RunCombatSeconds);
        int hours = total / 3600;
        int minutes = (total % 3600) / 60;
        int seconds = total % 60;

        return hours > 0
            ? $"{hours}:{minutes:00}:{seconds:00}"
            : $"{minutes:00}:{seconds:00}";
    }

    // ▼▼▼ [디버그 전용] 씬 이동 없이 층/보스 데이터만 세팅 (DebugQuickEntry 에서 사용) ▼▼▼
    // 현재 열려 있는 보스 씬을 그대로 테스트할 때, runner.LoadScene 을 거치지 않고
    // CurrentBossData 를 직접 채워주기 위한 메서드입니다. 정상 플레이 흐름에는 영향 없음.
    public void SetupForDebug(int level, BossEncounterData bossData)
    {
        CurrentLevel = level;
        CurrentBossData = bossData;
        Debug.Log($"[GameProgressionManager] (디버그) Level={level}, Boss={(bossData != null ? bossData.bossName : "없음")} 세팅");

        // 디버그 모드로 바로 진입해도 해당 보스의 BGM을 틀어줍니다!
        if (CurrentBossData != null && CurrentBossData.stageMainBGM != null && SoundManager.Instance != null)
        {
            SoundManager.Instance.PlayBGM(CurrentBossData.stageMainBGM);
        }
    }
    // ▲▲▲ 디버그 전용 끝 ▲▲▲
}