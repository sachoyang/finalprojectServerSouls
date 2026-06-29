using UnityEngine;
using UnityEngine.SceneManagement;

// [역할] 로비(scLobbyMain) 진입 시, scLoading을 Additive(오버레이)로 덮어 사전로딩 시간을 가린다.
//  - scLoading 위에서 LoadingSceneController가 예열 + 풀 사전생성을 끝내면 스스로 Unload → 로비가 드러남.
//  - Additive 로컬 로드라 Fusion 네트워크 세션은 건드리지 않는다.
//  - 세션당 1회만 덮는다(예열 완료 후 재입장 시엔 안 덮음).
//
//  사용: scLobbyMain에 빈 오브젝트 → 이 컴포넌트. (Library는 scLoading 쪽 컨트롤러에 연결돼 있으면 됨)
public class LobbyPreloadCover : MonoBehaviour
{
    [SerializeField] private string loadingSceneName = "scLoading";

    [Tooltip("이미 예열했으면 다시 안 덮음(세션당 1회).")]
    [SerializeField] private bool onlyOncePerSession = true;

    private static bool _shownThisSession;

    private void Start()
    {
        if (onlyOncePerSession && (_shownThisSession || ShaderWarmupRunner.HasWarmedUp))
            return;

        if (SceneManager.GetSceneByName(loadingSceneName).isLoaded)
            return; // 이미 떠 있으면 중복 로드 안 함

        _shownThisSession = true;
        SceneManager.LoadSceneAsync(loadingSceneName, LoadSceneMode.Additive);
    }
}
