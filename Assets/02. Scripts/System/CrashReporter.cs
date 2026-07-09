using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

// 서버로 올릴 크래시 리포트 1건. 디스크에도 이 형태 그대로 저장된다.
[Serializable]
public class CrashReportPayload
{
    public string client_report_id;  // 중복 업로드 방지용 GUID (서버에서 UNIQUE)
    public string report_type;       // exception / unhandled / native_crash
    public string message;
    public string stack_trace;
    public string log_tail;          // 네이티브 크래시일 때 직전 세션 Player-prev.log 꼬리
    public string scene;
    public string app_version;
    public string unity_version;
    public string platform;
    public string device_model;
    public string gpu;
    public int ram_mb;
    public string occurred_at;       // "yyyy-MM-dd HH:mm:ss" (UTC)
}

[Serializable]
public class CrashReportResponse
{
    public string status;
    public string message;
}

/// <summary>
/// 배포 빌드에서 터진 C# 예외를 서버로 보내고,
/// 네이티브 크래시(프로세스 즉사)는 "비정상 종료 마커"로 다음 실행 때 감지해서 보고한다.
///
/// Sentry와 병행 운용한다. Sentry가 메인이고 이쪽은 우리 DB에 남기는 백업 + 게임 고유 컨텍스트용.
/// 씬에 배치할 필요 없이 RuntimeInitializeOnLoadMethod로 게임 시작 시 자동 생성된다.
/// </summary>
public class CrashReporter : MonoBehaviour
{
    // 한 세션에서 서버로 보낼 리포트 최대 개수. Update 루프에서 예외가 터지면
    // 매 프레임 같은 예외가 쏟아지므로 상한이 없으면 서버가 죽는다.
    private const int MaxReportsPerSession = 10;

    // 서버 부하 방지용 상한. 스택은 보통 2~3KB, 로그 꼬리는 훨씬 길다.
    private const int MaxStackTraceLength = 8000;
    private const int MaxLogTailLength = 30000;
    private const int LogTailReadBytes = 32 * 1024;

#if UNITY_EDITOR
    // 에디터에서 개발 중에 나는 예외까지 서버로 쏘면 실제 유저 리포트가 묻힌다.
    // 그래서 기본은 꺼짐. 서버 연동을 테스트할 때만 개인적으로 켠다.
    //
    // EditorPrefs는 이 PC에만 저장되므로 커밋되지 않는다. 즉 내가 켜도 팀원 에디터는
    // 그대로 꺼져 있다. 메뉴: Tools > Crash Reporter > 에디터에서도 서버로 전송
    private const string UploadInEditorPrefKey = "SoulRush.CrashReporter.UploadInEditor";

    // Assembly-CSharp-Editor(Assets/Editor)에서 접근하므로 internal이 아니라 public이어야 한다.
    public static bool UploadInEditorEnabled
    {
        get => UnityEditor.EditorPrefs.GetBool(UploadInEditorPrefKey, false);
        set => UnityEditor.EditorPrefs.SetBool(UploadInEditorPrefKey, value);
    }
#endif

    // Debug.LogError까지 보고하면 노이즈가 너무 많다. 진짜 예외만 잡는다.
    private const bool CaptureLogError = false;

    private const float FlushIntervalSeconds = 10f;
    // 타이틀 씬이 뜨고 BackendManager가 LAN/WAN 탐지를 끝낼 때까지 넉넉히 기다린다.
    private const float ServerReadyTimeoutSeconds = 60f;

    private static CrashReporter _instance;

    // 백그라운드 스레드에서 온 로그를 메인 스레드로 넘기는 큐.
    // logMessageReceivedThreaded는 아무 스레드에서나 불릴 수 있는데,
    // UnityWebRequest / SystemInfo / persistentDataPath는 전부 메인 스레드 전용이다.
    private readonly ConcurrentQueue<PendingLog> _incoming = new ConcurrentQueue<PendingLog>();

    // 같은 예외가 반복해서 올라오는 걸 막는다.
    private readonly HashSet<string> _seenSignatures = new HashSet<string>();

    private int _reportsThisSession;
    private bool _isUploading;

    // 메인 스레드에서만 읽을 수 있는 값들을 미리 캐싱해둔다.
    private string _pendingDir;
    private string _sessionMarkerPath;
    private string _prevLogPath;
    private string _currentSceneName = "(unknown)";
    private string _appVersion;
    private string _unityVersion;
    private string _platform;
    private string _deviceModel;
    private string _gpu;
    private int _ramMb;

    private struct PendingLog
    {
        public string Message;
        public string StackTrace;
        public LogType Type;
        public DateTime UtcTime;
    }

    // ==========================================
    // [0] 자동 부팅 — 씬 배치 불필요, 최대한 이른 시점에
    // ==========================================
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (_instance != null) return;

        GameObject go = new GameObject(nameof(CrashReporter));
        _instance = go.AddComponent<CrashReporter>();
        DontDestroyOnLoad(go);
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;

        CacheMainThreadValues();

        // 순서 중요: 마커를 새로 쓰기 전에 지난 세션 마커부터 확인해야 한다.
        bool previousSessionCrashed = File.Exists(_sessionMarkerPath);
        WriteSessionMarker();

        if (previousSessionCrashed)
        {
            Debug.LogWarning("[CrashReporter] 지난 세션이 정상 종료되지 않았습니다. 네이티브 크래시로 간주하고 보고합니다.");
            QueueNativeCrashReport();
        }

        Application.logMessageReceivedThreaded += OnLogMessageThreaded;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        SceneManager.activeSceneChanged += OnActiveSceneChanged;

        StartCoroutine(FlushRoutine());
    }

    private void OnDestroy()
    {
        if (_instance != this) return;

        Application.logMessageReceivedThreaded -= OnLogMessageThreaded;
        AppDomain.CurrentDomain.UnhandledException -= OnUnhandledException;
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
    }

    private void OnApplicationQuit()
    {
        // 정상 종료 → 마커를 지운다. 이게 지워지지 않은 채 다음 실행이 시작되면 크래시.
        ClearSessionMarker();
    }

    private void CacheMainThreadValues()
    {
        string root = Application.persistentDataPath;
        _pendingDir = Path.Combine(root, "CrashReports");
        _sessionMarkerPath = Path.Combine(root, "session_open.marker");

        // Windows 스탠드얼론 기준 Player 로그는 persistentDataPath와 같은 폴더에 있다.
        _prevLogPath = Path.Combine(root, "Player-prev.log");

        _appVersion = Application.version;
        _unityVersion = Application.unityVersion;
        _platform = Application.platform.ToString();
        _deviceModel = SystemInfo.deviceModel;
        _gpu = SystemInfo.graphicsDeviceName;
        _ramMb = SystemInfo.systemMemorySize;
        _currentSceneName = SceneManager.GetActiveScene().name;

        Directory.CreateDirectory(_pendingDir);
    }

    private void OnActiveSceneChanged(Scene from, Scene to)
    {
        // 백그라운드 스레드에서 씬 이름을 물어볼 수 없으니 바뀔 때마다 캐싱해둔다.
        _currentSceneName = to.name;
    }

    // ==========================================
    // [1] 예외 수집
    // ==========================================
    private void OnLogMessageThreaded(string message, string stackTrace, LogType type)
    {
        bool isReportable = type == LogType.Exception
                            || type == LogType.Assert
                            || (CaptureLogError && type == LogType.Error);
        if (!isReportable) return;

        _incoming.Enqueue(new PendingLog
        {
            Message = message,
            StackTrace = stackTrace,
            Type = type,
            UtcTime = DateTime.UtcNow
        });
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (!(e.ExceptionObject is Exception ex)) return;

        _incoming.Enqueue(new PendingLog
        {
            Message = ex.Message,
            StackTrace = ex.StackTrace,
            Type = LogType.Exception,
            UtcTime = DateTime.UtcNow
        });
    }

    private void Update()
    {
        // 메인 스레드에서 큐를 비운다. 한 프레임에 몰아서 처리하면 렉이 걸리므로 조금씩.
        int drained = 0;
        while (drained < 3 && _incoming.TryDequeue(out PendingLog log))
        {
            drained++;
            HandleLog(log);
        }
    }

    private void HandleLog(PendingLog log)
    {
        if (_reportsThisSession >= MaxReportsPerSession) return;

        string signature = BuildSignature(log);
        if (!_seenSignatures.Add(signature)) return; // 이미 보고한 예외

        _reportsThisSession++;

        var payload = CreatePayload(
            log.Type == LogType.Assert ? "unhandled" : "exception",
            log.Message,
            log.StackTrace,
            null,
            log.UtcTime);

        SaveToDisk(payload);
    }

    // 같은 예외인지 판별하는 키. 메시지 + 스택 최상단 프레임이면 충분하다.
    private static string BuildSignature(PendingLog log)
    {
        string firstFrame = string.Empty;
        if (!string.IsNullOrEmpty(log.StackTrace))
        {
            int newline = log.StackTrace.IndexOf('\n');
            firstFrame = newline > 0 ? log.StackTrace.Substring(0, newline) : log.StackTrace;
        }
        return $"{log.Type}|{log.Message}|{firstFrame}";
    }

    // ==========================================
    // [2] 네이티브 크래시 (지난 세션 비정상 종료)
    // ==========================================
    private void QueueNativeCrashReport()
    {
        string logTail = ReadPreviousLogTail();

        // 마커 파일에 지난 세션 시작 시각 등을 적어뒀다.
        string markerInfo = SafeReadAllText(_sessionMarkerPath);

        var payload = CreatePayload(
            "native_crash",
            "지난 세션이 정상 종료되지 않음 (네이티브 크래시 추정)",
            markerInfo,
            logTail,
            DateTime.UtcNow);

        SaveToDisk(payload);
    }

    // Player-prev.log 전체를 읽으면 수십 MB일 수 있다. 뒤쪽 32KB만 본다.
    private string ReadPreviousLogTail()
    {
        try
        {
            if (!File.Exists(_prevLogPath)) return "(Player-prev.log 없음)";

            // Unity가 아직 잡고 있을 수 있으므로 공유 모드로 연다.
            using (var fs = new FileStream(_prevLogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                long start = Math.Max(0, fs.Length - LogTailReadBytes);
                fs.Seek(start, SeekOrigin.Begin);

                using (var reader = new StreamReader(fs, Encoding.UTF8))
                {
                    return Truncate(reader.ReadToEnd(), MaxLogTailLength);
                }
            }
        }
        catch (Exception ex)
        {
            return $"(로그 읽기 실패: {ex.Message})";
        }
    }

    private void WriteSessionMarker()
    {
        try
        {
            string info = $"session_started_utc={DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}\n"
                        + $"app_version={_appVersion}\n"
                        + $"gpu={_gpu}\n";
            File.WriteAllText(_sessionMarkerPath, info);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[CrashReporter] 세션 마커 기록 실패: {ex.Message}");
        }
    }

    private void ClearSessionMarker()
    {
        try
        {
            if (File.Exists(_sessionMarkerPath)) File.Delete(_sessionMarkerPath);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[CrashReporter] 세션 마커 삭제 실패: {ex.Message}");
        }
    }

    // ==========================================
    // [3] 디스크 저장 (업로드 실패해도 다음 실행 때 재시도)
    // ==========================================
    private CrashReportPayload CreatePayload(string type, string message, string stack, string logTail, DateTime utc)
    {
        return new CrashReportPayload
        {
            client_report_id = Guid.NewGuid().ToString("N"),
            report_type = type,
            message = Truncate(message ?? string.Empty, 1000),
            stack_trace = Truncate(stack ?? string.Empty, MaxStackTraceLength),
            log_tail = Truncate(logTail ?? string.Empty, MaxLogTailLength),
            scene = _currentSceneName,
            app_version = _appVersion,
            unity_version = _unityVersion,
            platform = _platform,
            device_model = _deviceModel,
            gpu = _gpu,
            ram_mb = _ramMb,
            occurred_at = utc.ToString("yyyy-MM-dd HH:mm:ss")
        };
    }

    private void SaveToDisk(CrashReportPayload payload)
    {
        try
        {
            string path = Path.Combine(_pendingDir, $"{payload.client_report_id}.json");
            File.WriteAllText(path, JsonUtility.ToJson(payload), Encoding.UTF8);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[CrashReporter] 리포트 저장 실패: {ex.Message}");
        }
    }

    // ==========================================
    // [4] 업로드
    // ==========================================
    private IEnumerator FlushRoutine()
    {
#if UNITY_EDITOR
        if (!UploadInEditorEnabled)
        {
            Debug.Log("[CrashReporter] 에디터 전송이 꺼져 있습니다. 리포트는 디스크에만 쌓입니다. "
                    + "(Tools > Crash Reporter 메뉴에서 켤 수 있습니다)");
            yield break;
        }

        Debug.Log("[CrashReporter] 에디터 전송이 켜져 있습니다. 예외가 실제 서버로 올라갑니다.");
#endif
        // BackendManager.Instance를 직접 건드리면 씬에 있는 진짜 인스턴스 대신
        // 인스펙터 설정이 빠진 빈 오브젝트가 만들어진다. 씬 쪽이 뜰 때까지 기다리기만 한다.
        float waited = 0f;
        while (waited < ServerReadyTimeoutSeconds)
        {
            if (BackendManager.HasInstance && BackendManager.Instance.isServerReady) break;

            waited += Time.unscaledDeltaTime;
            yield return null;
        }

        if (!BackendManager.HasInstance || !BackendManager.Instance.isServerReady)
        {
            Debug.LogWarning("[CrashReporter] 서버 주소를 확인하지 못했습니다. 리포트는 디스크에 남아 다음 실행 때 전송됩니다.");
            yield break;
        }

        while (true)
        {
            yield return UploadPendingRoutine();
            yield return new WaitForSecondsRealtime(FlushIntervalSeconds);
        }
    }

    private IEnumerator UploadPendingRoutine()
    {
        if (_isUploading) yield break;
        _isUploading = true;

        // catch 블록 안에서는 yield를 쓸 수 없으므로, 실패 시 빈 배열로 두고 루프를 그냥 건너뛴다.
        string[] files;
        try
        {
            files = Directory.GetFiles(_pendingDir, "*.json");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[CrashReporter] 리포트 폴더 조회 실패: {ex.Message}");
            files = Array.Empty<string>();
        }

        foreach (string file in files)
        {
            string json = SafeReadAllText(file);
            if (string.IsNullOrEmpty(json))
            {
                TryDelete(file);
                continue;
            }

            CrashReportPayload payload = null;
            try
            {
                payload = JsonUtility.FromJson<CrashReportPayload>(json);
            }
            catch (Exception ex)
            {
                // 깨진 파일은 계속 재시도해봐야 소용없다.
                Debug.LogWarning($"[CrashReporter] 리포트 파싱 실패, 폐기합니다: {ex.Message}");
            }

            if (payload == null)
            {
                TryDelete(file);
                continue;
            }

            bool uploaded = false;
            yield return SendReportRoutine(payload, success => uploaded = success);

            if (uploaded) TryDelete(file);
            else break; // 서버가 죽었으면 나머지도 실패한다. 다음 주기에 재시도.
        }

        _isUploading = false;
    }

    private IEnumerator SendReportRoutine(CrashReportPayload payload, Action<bool> onComplete)
    {
        // 종료 중이거나 매니저가 파괴된 상태면 다음 실행 때 다시 보낸다.
        if (!BackendManager.HasInstance)
        {
            onComplete?.Invoke(false);
            yield break;
        }

        var backend = BackendManager.Instance;

        WWWForm form = new WWWForm();
        form.AddField("client_report_id", payload.client_report_id);
        form.AddField("report_type", payload.report_type);
        form.AddField("message", payload.message);
        form.AddField("stack_trace", payload.stack_trace);
        form.AddField("log_tail", payload.log_tail);
        form.AddField("scene", payload.scene);
        form.AddField("app_version", payload.app_version);
        form.AddField("unity_version", payload.unity_version);
        form.AddField("platform", payload.platform);
        form.AddField("device_model", payload.device_model);
        form.AddField("gpu", payload.gpu);
        form.AddField("ram_mb", payload.ram_mb.ToString());
        form.AddField("occurred_at", payload.occurred_at);

        // 로그인 전에 크래시가 날 수도 있으므로 계정 정보는 있을 때만 붙인다.
        form.AddField("login_id", backend.CurrentLoginID ?? string.Empty);
        form.AddField("nickname", backend.CurrentNickname ?? string.Empty);
        form.AddField("session_token", backend.CurrentSessionToken ?? string.Empty);

        using (UnityWebRequest www = UnityWebRequest.Post(backend.BASE_URL + "crash_report.php", form))
        {
            www.timeout = 10;
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[CrashReporter] 전송 실패(네트워크): {www.error}");
                onComplete?.Invoke(false);
                yield break;
            }

            CrashReportResponse res = null;
            try
            {
                res = JsonUtility.FromJson<CrashReportResponse>(www.downloadHandler.text);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[CrashReporter] 응답 파싱 실패: {ex.Message}");
            }

            // "duplicate"는 서버가 이미 갖고 있다는 뜻이므로 성공으로 친다(파일 삭제).
            bool ok = res != null && (res.status == "success" || res.status == "duplicate");
            if (!ok) Debug.LogWarning($"[CrashReporter] 서버가 거부: {www.downloadHandler.text}");

            onComplete?.Invoke(ok);
        }
    }

    // ==========================================
    // 유틸
    // ==========================================
    private static string Truncate(string value, int max)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= max) return value ?? string.Empty;
        return value.Substring(0, max) + "\n...(truncated)";
    }

    private static string SafeReadAllText(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8) : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static void TryDelete(string path)
    {
        try { File.Delete(path); }
        catch (Exception ex) { Debug.LogWarning($"[CrashReporter] 파일 삭제 실패: {ex.Message}"); }
    }
}
