using Sentry;
using Sentry.Unity;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Sentry로 올라가는 이벤트에 게임 고유 컨텍스트를 붙인다.
///
/// 크래시 자체는 Sentry SDK가 알아서 잡는다. 이 스크립트가 하는 일은
/// "누가, 어느 씬에서, 직전에 무슨 씬을 거쳐서" 죽었는지를 이벤트에 태워 보내는 것.
/// 이게 없으면 Sentry 이슈에 스택트레이스만 덩그러니 남는다.
/// </summary>
public class SentryGameContext : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        // SentryOptions.asset에 DSN이 없거나 SDK가 꺼져 있으면 아무것도 하지 않는다.
        if (!SentrySdk.IsEnabled) return;

        GameObject go = new GameObject(nameof(SentryGameContext));
        go.AddComponent<SentryGameContext>();
        DontDestroyOnLoad(go);
    }

    private void Awake()
    {
        SceneManager.activeSceneChanged += OnActiveSceneChanged;
        ApplyScene(SceneManager.GetActiveScene().name);
    }

    private void OnDestroy()
    {
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
    }

    private void Update()
    {
        // 로그인은 크래시 리포터보다 늦게 끝난다. 닉네임이 잡히면 한 번만 붙여준다.
        if (_userApplied || !BackendManager.HasInstance) return;

        string nickname = BackendManager.Instance.CurrentNickname;
        if (string.IsNullOrEmpty(nickname)) return;

        ApplyUser(BackendManager.Instance.CurrentLoginID, nickname);
        _userApplied = true;
    }

    private bool _userApplied;

    private void OnActiveSceneChanged(Scene from, Scene to)
    {
        // 크래시 직전 씬 이동 경로가 이슈에 타임라인으로 남는다.
        SentrySdk.AddBreadcrumb($"{from.name} -> {to.name}", category: "scene");
        ApplyScene(to.name);
    }

    private static void ApplyScene(string sceneName)
    {
        if (!SentrySdk.IsEnabled) return;

        // 태그로 넣어야 Sentry에서 "이 씬에서만 나는 크래시"로 필터링할 수 있다.
        SentrySdk.ConfigureScope(scope => scope.SetTag("game.scene", sceneName));
    }

    private static void ApplyUser(string loginId, string nickname)
    {
        if (!SentrySdk.IsEnabled) return;

        SentrySdk.ConfigureScope(scope =>
        {
            scope.User.Id = loginId;
            scope.User.Username = nickname;
        });
    }
}
