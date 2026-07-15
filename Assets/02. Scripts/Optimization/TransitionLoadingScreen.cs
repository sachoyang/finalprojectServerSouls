using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// [역할] 네트워크 씬 전환(로비→보스, 보스→보스 등)을 가리는 '지속 로딩 커버'.
//  - 방장이 Start를 눌러 runner.LoadScene 이 일어나면 Fusion이 모든 피어에 OnSceneLoadStart/Done 콜백을 보낸다.
//    그 시점에 전체화면 커버를 띄웠다가, 로드 완료 + 최소 표시시간 뒤에 내린다.
//  - 호스트/클라이언트 구분 없이 자동 동작(콜백이 양쪽 다 옴).
//  - 씬을 Single로 새로 로드해도 살아남도록 DontDestroyOnLoad 캔버스를 쓴다(씬을 additive로 띄우면 Single 로드에 사라짐).
//
//  ※ 이 커버는 '전환을 가리는 용도'다. 셰이더 예열/풀 사전생성은 별도(로비 LobbyPreloadCover / 디버그 오버레이)에서 한다.
//
//  배치: 자동 생성됨(Resources에 "TransitionLoadingScreen" 프리팹이 있으면 그걸, 없으면 코드로 단색 커버 생성).
//        더 예쁜 화면을 원하면 Canvas를 자식으로 둔 프리팹을 Resources/TransitionLoadingScreen.prefab 로 만들면 된다.
public class TransitionLoadingScreen : MonoBehaviour, INetworkRunnerCallbacks
{
    public static TransitionLoadingScreen Instance { get; private set; }

    [Tooltip("커버를 최소 이만큼(초)은 보여준다(깜빡임 방지).")]
    [Min(0f)] public float minDisplayTime = 1.2f;

    [Tooltip("안전장치: 이 시간이 지나면 OnSceneLoadDone을 못 받았어도 커버를 강제로 내린다.")]
    [Min(1f)] public float maxDisplayTime = 20f;

    [Tooltip("페이드 시간(초).")]
    [Min(0f)] public float fadeDuration = 0.3f;

    [Tooltip("이 씬들은 로드 완료 시 전환 커버를 페이드 없이 즉시 내린다.")]
    [SerializeField] private string[] instantHideSceneNames = { "scLobbyMain" };

    private CanvasGroup _cg;
    private GameObject _coverRoot;
    private Text _text;

    private float _shownAt;
    private bool _visible;
    private Coroutine _hideRoutine;
    private Coroutine _safetyRoutine;
    private readonly HashSet<NetworkRunner> _registered = new HashSet<NetworkRunner>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoBootstrap()
    {
        if (Instance != null) return;
        var prefab = Resources.Load<GameObject>("TransitionLoadingScreen");
        if (prefab != null) Instantiate(prefab);
        else
        {
            var go = new GameObject("TransitionLoadingScreen");
            go.AddComponent<TransitionLoadingScreen>();
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // 프리팹에 캔버스가 들어 있으면 그걸 쓰고, 없으면 코드로 만든다.
        _cg = GetComponentInChildren<CanvasGroup>(true);
        if (_cg == null) BuildProgrammaticCover();
        else { _coverRoot = _cg.gameObject; _text = GetComponentInChildren<Text>(true); }

        HideImmediate();
        SceneManager.sceneLoaded += OnUnitySceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnUnitySceneLoaded;
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        // 활성 러너에 콜백 등록(런타임에 러너가 생겨도 자동 연결).
        var instances = NetworkRunner.Instances;
        if (instances != null)
        {
            for (int i = 0; i < instances.Count; i++)
            {
                var r = instances[i];
                if (r != null && !r.IsShutdown && _registered.Add(r))
                    r.AddCallbacks(this);
            }
        }
    }

    // ── 표시 제어 ─────────────────────────────────────────────
    public void Show()
    {
        if (_hideRoutine != null) { StopCoroutine(_hideRoutine); _hideRoutine = null; }
        if (_safetyRoutine != null) { StopCoroutine(_safetyRoutine); _safetyRoutine = null; }

        if (_coverRoot != null) _coverRoot.SetActive(true);
        if (_cg != null) { _cg.alpha = 1f; _cg.blocksRaycasts = true; }
        _shownAt = Time.realtimeSinceStartup;
        _visible = true;

        _safetyRoutine = StartCoroutine(SafetyHide());
    }

    public void HideAfterMinTime()
    {
        if (!_visible) return;
        if (_hideRoutine != null) StopCoroutine(_hideRoutine);
        _hideRoutine = StartCoroutine(HideRoutine());
    }

    public void HideNow()
    {
        if (_hideRoutine != null) { StopCoroutine(_hideRoutine); _hideRoutine = null; }
        if (_safetyRoutine != null) { StopCoroutine(_safetyRoutine); _safetyRoutine = null; }
        HideImmediate();
    }

    private IEnumerator HideRoutine()
    {
        float remain = minDisplayTime - (Time.realtimeSinceStartup - _shownAt);
        if (remain > 0f) yield return new WaitForSecondsRealtime(remain);

        // 페이드 아웃
        if (_cg != null && fadeDuration > 0f)
        {
            float t = 0f;
            while (t < fadeDuration)
            {
                t += Time.unscaledDeltaTime;
                _cg.alpha = Mathf.Lerp(1f, 0f, t / fadeDuration);
                yield return null;
            }
        }
        HideImmediate();
        _hideRoutine = null;
    }

    private IEnumerator SafetyHide()
    {
        yield return new WaitForSecondsRealtime(maxDisplayTime);
        if (_visible) HideAfterMinTime(); // Done을 못 받은 경우 강제 정리
    }

    private void HideImmediate()
    {
        if (_cg != null) { _cg.alpha = 0f; _cg.blocksRaycasts = false; }
        if (_coverRoot != null) _coverRoot.SetActive(false);
        _visible = false;
    }

    private void OnUnitySceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (ShouldHideImmediately(scene.name))
        {
            HideNow();
        }
    }

    private bool ShouldHideImmediately(string sceneName)
    {
        if (instantHideSceneNames == null)
            return false;

        for (int i = 0; i < instantHideSceneNames.Length; i++)
        {
            if (instantHideSceneNames[i] == sceneName)
                return true;
        }

        return false;
    }

    private void BuildProgrammaticCover()
    {
        var canvasGO = new GameObject("Cover", typeof(RectTransform));
        canvasGO.transform.SetParent(transform, false);

        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32760; // 최상단
        canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<GraphicRaycaster>();
        _cg = canvasGO.AddComponent<CanvasGroup>();

        // 배경(검정 풀스크린)
        var bgGO = new GameObject("BG", typeof(RectTransform));
        bgGO.transform.SetParent(canvasGO.transform, false);
        var bg = bgGO.AddComponent<Image>();
        bg.color = Color.black;
        Stretch(bg.rectTransform);

        // 텍스트
        var txtGO = new GameObject("Text", typeof(RectTransform));
        txtGO.transform.SetParent(canvasGO.transform, false);
        _text = txtGO.AddComponent<Text>();
        _text.text = "로딩 중...";
        _text.alignment = TextAnchor.MiddleCenter;
        _text.fontSize = 40;
        _text.color = Color.white;
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        _text.font = font;
        Stretch(_text.rectTransform);

        _coverRoot = canvasGO;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    // ── Fusion 콜백 ───────────────────────────────────────────
    public void OnSceneLoadStart(NetworkRunner runner) => Show();
    public void OnSceneLoadDone(NetworkRunner runner) => HideAfterMinTime();
    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { _registered.Remove(runner); }

    // 나머지 INetworkRunnerCallbacks (미사용)
    public void OnInput(NetworkRunner runner, NetworkInput input) { }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    [SuppressMessage("Unity", "UNT0006", Justification = "Fusion INetworkRunnerCallbacks method; not a Unity legacy message.")]
    public void OnConnectedToServer(NetworkRunner runner) { }
    [SuppressMessage("Unity", "UNT0006", Justification = "Fusion INetworkRunnerCallbacks method; not a Unity legacy message.")]
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player) { }
    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
}
