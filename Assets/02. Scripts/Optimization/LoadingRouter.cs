using UnityEngine.SceneManagement;

// [역할] "로딩(예열) 씬을 거쳐 목표 씬으로 이동"하는 진입점.
//  사용: 어디서든 LoadingRouter.Go("Gothic_Stage"); 처럼 호출하면
//        → scLoading 씬으로 가서 예열 → 끝나면 자동으로 "Gothic_Stage"로 이동.
//
//  ※ 셰이더 예열은 '로컬(각 클라이언트)' 작업이고 세션당 1회면 충분하다.
//    네트워크(Fusion) 씬 전환과는 별개로, 무거운 게임플레이 씬을 처음 렌더하기 전에 한 번 거치면 된다.
public static class LoadingRouter
{
    // 로딩(예열) 씬 이름. Build Settings에 등록되어 있어야 한다.
    public const string LoadingSceneName = "scLoading";

    // 예열이 끝난 뒤 이동할 목표 씬 이름. (LoadingSceneController가 읽어서 로드)
    public static string NextScene;

    // 예열 씬을 거쳐 targetScene으로 이동.
    public static void Go(string targetScene)
    {
        NextScene = targetScene;
        SceneManager.LoadScene(LoadingSceneName);
    }
}
