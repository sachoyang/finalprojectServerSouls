using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// [역할] 로딩(예열) 화면 컨트롤러. scLoading 씬에 둔다. 두 가지 방식 모두 지원(로드 방식으로 자동 감지).
//   1) Transition (single 로드): 디버그/비네트워크 진입용. 예열 → LoadingRouter.NextScene 으로 이동.
//   2) Overlay   (additive 로드): 로비 위에 덮어 '멈춘 화면'을 가림. 예열 → 자기 자신(scLoading)만 Unload → 뒤 씬이 드러남.
//
//  공통 작업 순서: 셰이더 예열 → 추가 프리로드(풀 사전생성 등, ExtraPreload 훅) → 마무리 GC.
//  - ExtraPreload: 풀링 매니저 등 다른 시스템이 코루틴을 등록해 끼워 넣는 확장점. (프레임 분산해서 멈춤 방지)
public class LoadingSceneController : MonoBehaviour
{
    public enum LoadingMode { AutoDetect, Transition, Overlay }

    [Header("동작 방식")]
    [Tooltip("AutoDetect: 다른 씬과 함께(additive) 로드됐으면 Overlay, 단독(single)이면 Transition 으로 자동 판단.")]
    [SerializeField] private LoadingMode mode = LoadingMode.AutoDetect;

    [Header("예열 데이터")]
    [SerializeField] private WarmupLibrary library;

    [Header("이동 대상 (Transition 전용)")]
    [Tooltip("LoadingRouter.NextScene 가 비어 있을 때 이동할 기본 씬(이 씬 단독 테스트용)")]
    [SerializeField] private string fallbackScene = "";

    [Header("진행률 UI (선택)")]
    [SerializeField] private Slider progressBar;
    [SerializeField] private Text progressText;
    [SerializeField] private CanvasGroup canvasGroup; // Overlay 페이드아웃용
    [SerializeField] private float fadeOutDuration = 0.4f;

    [Header("기타")]
    [Tooltip("로딩 화면을 최소 이만큼(초)은 보여준다. 사전작업이 이미 끝나 금방 끝나도 이 시간은 채운다.\n" +
             "→ 사전로딩 완료 시: 이 최소시간만 노출 / 아직이면: 실제 로딩이 끝날 때까지(이 값 이상) 노출.")]
    [Min(0f)] [SerializeField] private float minDisplayTime = 1.5f;
    [Tooltip("로딩 끝에 사용하지 않는 에셋 정리 + GC. 전투 중 GC 스파이크 예방.")]
    [SerializeField] private bool runGCAtEnd = true;
    [Tooltip("이미 예열했어도 매번 다시(테스트용).")]
    [SerializeField] private bool forceWarmEachTime = false;

    // 풀 사전생성 등 추가 프리로드를 끼우는 훅. 코루틴(진행률 콜백)을 반환하는 함수를 등록한다.
    //  예) LoadingSceneController.ExtraPreload = onProgress => PoolManager.Instance.PrewarmRoutine(onProgress);
    public static Func<Action<float>, IEnumerator> ExtraPreload;

    private IEnumerator Start()
    {
        float startTime = Time.realtimeSinceStartup;
        yield return null; // 씬 초기화 대기

        LoadingMode actual = ResolveMode();

        // Overlay면 이 씬의 카메라/오디오리스너를 꺼서 뒤 씬(로비)과 충돌 방지. (로딩 캔버스는 Screen Space-Overlay라 카메라 불필요)
        if (actual == LoadingMode.Overlay)
            DisableSceneCamerasAndListeners();

        // 1) 셰이더 예열 (0 ~ 0.5)
        yield return ShaderWarmupRunner.Warm(library, p => Report(p * 0.5f, "셰이더 예열 중..."), forceWarmEachTime);

        // 2) 추가 프리로드: 풀 사전생성 등 (0.5 ~ 0.9)
        if (ExtraPreload != null)
            yield return ExtraPreload(p => Report(0.5f + p * 0.4f, "리소스 준비 중..."));

        // 3) 마무리 GC (0.9 ~ 1.0)
        if (runGCAtEnd)
        {
            Report(0.92f, "정리 중...");
            Resources.UnloadUnusedAssets();
            GC.Collect();
            yield return null;
        }
        Report(1f, "완료");

        // 최소 표시시간 보장: 사전작업이 금방 끝나도 화면이 깜빡이지 않게 이 시간은 채운다.
        float remain = minDisplayTime - (Time.realtimeSinceStartup - startTime);
        if (remain > 0f) yield return new WaitForSecondsRealtime(remain);

        // 4) 마무리: 방식에 따라 이동 또는 오버레이 해제
        if (actual == LoadingMode.Overlay)
        {
            yield return FadeOut();
            SceneManager.UnloadSceneAsync(gameObject.scene);
        }
        else
        {
            string target = string.IsNullOrEmpty(LoadingRouter.NextScene) ? fallbackScene : LoadingRouter.NextScene;
            if (string.IsNullOrEmpty(target))
            {
                Debug.LogWarning("[LoadingScene] 이동할 대상 씬이 없습니다. (LoadingRouter.NextScene / fallbackScene 둘 다 비어 있음)");
                yield break;
            }

            var op = SceneManager.LoadSceneAsync(target);
            while (op != null && !op.isDone)
            {
                Report(0.8f + 0.2f * Mathf.Clamp01(op.progress / 0.9f), "씬 로드 중...");
                yield return null;
            }
        }
    }

    private LoadingMode ResolveMode()
    {
        if (mode != LoadingMode.AutoDetect) return mode;
        // 다른 씬과 함께 로드됐으면(additive) Overlay, 단독이면 Transition.
        return SceneManager.sceneCount > 1 ? LoadingMode.Overlay : LoadingMode.Transition;
    }

    private void DisableSceneCamerasAndListeners()
    {
        Scene myScene = gameObject.scene;
        foreach (var go in myScene.GetRootGameObjects())
        {
            foreach (var cam in go.GetComponentsInChildren<Camera>(true)) cam.enabled = false;
            foreach (var al in go.GetComponentsInChildren<AudioListener>(true)) al.enabled = false;
        }
    }

    private IEnumerator FadeOut()
    {
        if (canvasGroup == null) yield break;
        float t = 0f;
        float start = canvasGroup.alpha;
        while (t < fadeOutDuration)
        {
            t += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(start, 0f, t / fadeOutDuration);
            yield return null;
        }
        canvasGroup.alpha = 0f;
    }

    private void Report(float p, string label)
    {
        if (progressBar != null) progressBar.value = p;
        if (progressText != null) progressText.text = $"{label} {(int)(p * 100)}%";
    }
}
