using UnityEngine;

// [역할] 정식 플로우용 셰이더 예열 부트스트랩.
//  scLobbyMain(로비) 같은 '게임플레이 직전에 머무는 씬'에 빈 GameObject로 올려두면,
//  대기/레디 시간 동안 백그라운드로 셰이더/이펙트를 미리 컴파일한다.
//  → 보스 씬으로 들어간 뒤 피/보스 이펙트가 처음 뜰 때 생기던 컴파일 끊김을 없앤다.
//
//  왜 로딩씬(scLoading)이 아니라 로비에서 하나?
//   - 로비 → 보스 씬 전환은 Fusion 네트워크 씬 로드(runner.LoadScene)라, 중간에 로컬 scLoading을
//     끼우면 세션이 어긋난다. 예열은 '로컬·세션당 1회'면 충분하므로, 로비에서 미리 끝내두는 게 안전하다.
//   - scLoading은 디버그 빠른시작(DebugQuickEntry)과 네트워크가 아닌 메뉴 전환에서 사용한다.
//
//  사용: scLobbyMain에 빈 오브젝트 → 이 컴포넌트 → Library에 WarmupLibrary 에셋 연결. 그게 전부.
public class ShaderWarmupBootstrap : MonoBehaviour
{
    [SerializeField] private WarmupLibrary library;

    [Tooltip("씬 진입 시 자동으로 예열 시작.")]
    [SerializeField] private bool warmOnStart = true;

    [Tooltip("예열이 끝나면 이 오브젝트를 자동 파괴. (씬 전환을 넘겨 끝까지 돌도록 DontDestroyOnLoad로 유지)")]
    [SerializeField] private bool destroyWhenDone = true;

    // 같은 세션에서 중복 실행 방지(재입장 등).
    private static bool _running;

    // 예열 완료 여부 — 필요하면 다른 코드에서 참고(예: 완료 전 전투 시작 막기).
    public static bool IsReady => ShaderWarmupRunner.HasWarmedUp;

    private void Start()
    {
        if (!warmOnStart) return;

        // 이미 예열했거나 진행 중이면 중복 실행 안 함.
        if (ShaderWarmupRunner.HasWarmedUp || _running)
        {
            if (destroyWhenDone) Destroy(gameObject);
            return;
        }

        _running = true;
        // 보스 씬으로 넘어가도 코루틴이 끊기지 않도록 유지.
        DontDestroyOnLoad(gameObject);
        StartCoroutine(WarmRoutine());
    }

    private System.Collections.IEnumerator WarmRoutine()
    {
        yield return ShaderWarmupRunner.Warm(library);
        _running = false;
        Debug.Log("[ShaderWarmupBootstrap] 셰이더 예열 완료.");
        if (destroyWhenDone) Destroy(gameObject);
    }
}
