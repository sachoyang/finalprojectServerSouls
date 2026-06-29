using System;
using System.Collections;
using UnityEngine;

// [역할] 셰이더 예열(프리워밍)의 실제 동작. 로딩 화면 등에서 코루틴으로 호출한다.
//  방식:
//   1) (선택) ShaderVariantCollection.WarmUp() 으로 녹화된 변형을 한 번에 컴파일.
//   2) 등록된 이펙트 프리팹들을 화면 밖 작은 RenderTexture 카메라로 잠깐 렌더 →
//      그 이펙트가 '실제로 쓰는' 셰이더 변형/키워드를 강제로 컴파일하고, 텍스처/메시를 GPU에 업로드.
//   → 게임플레이 중 처음 뜰 때 생기던 컴파일/업로드 멈춤(로딩 걸림)을 로딩 화면 시점으로 옮긴다.
//
//  ※ 셰이더 변형 컴파일 결과는 '세션 동안 유지'되므로, 한 세션에 한 번만 예열하면 된다(HasWarmedUp).
public static class ShaderWarmupRunner
{
    // 이번 실행(세션)에서 예열을 이미 끝냈는지. 한 번 true가 되면 다시 안 해도 된다.
    public static bool HasWarmedUp { get; private set; }

    public static IEnumerator Warm(WarmupLibrary library, Action<float> onProgress = null, bool force = false)
    {
        if (library == null) { onProgress?.Invoke(1f); yield break; }
        if (HasWarmedUp && !force) { onProgress?.Invoke(1f); yield break; }

        // 1) 녹화된 변형 컬렉션 예열 (있을 때만)
        if (library.shaderVariants != null)
            library.shaderVariants.WarmUp();

        if (library.warmupAllLoadedCollections)
            Shader.WarmupAllShaders();

        // 2) 이펙트 프리팹을 화면 밖에서 잠깐 렌더해 변형/리소스 강제 로드
        var prefabs = library.effectPrefabs;
        if (prefabs != null && prefabs.Count > 0)
        {
            // 플레이어에게 안 보이도록 아주 먼 곳 + 작은 RenderTexture에 렌더
            Vector3 warmPos = new Vector3(0f, -10000f, 0f);

            var camGO = new GameObject("__ShaderWarmupCamera");
            camGO.transform.position = warmPos + new Vector3(0f, 0f, -6f);
            var cam = camGO.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.cullingMask = ~0;               // 모든 레이어 렌더
            cam.nearClipPlane = 0.01f;
            cam.farClipPlane = 1000f;
            cam.depth = -1000;                  // 다른 카메라보다 먼저, 화면 출력엔 영향 없게
            var rt = new RenderTexture(64, 64, 16);
            cam.targetTexture = rt;             // 화면이 아니라 RT로 렌더(깜빡임 방지). 렌더 자체로 컴파일은 유발됨

            var root = new GameObject("__ShaderWarmupRoot");
            root.transform.position = warmPos;

            int n = prefabs.Count;
            int frames = Mathf.Max(1, library.framesPerPrefab);

            for (int i = 0; i < n; i++)
            {
                var pf = prefabs[i];
                if (pf == null) { onProgress?.Invoke((i + 1f) / n); continue; }

                var inst = UnityEngine.Object.Instantiate(pf, warmPos + new Vector3(0f, 0f, 2f), Quaternion.identity, root.transform);

                // 예열은 '렌더'만 필요하다. 게임 로직 스크립트(데미지/사운드/네트워크 등)는 꺼서 부작용 방지.
                foreach (var mb in inst.GetComponentsInChildren<MonoBehaviour>(true))
                    if (mb != null) mb.enabled = false;

                // 파티클은 재생 상태여야 머티리얼이 실제로 렌더된다.
                foreach (var ps in inst.GetComponentsInChildren<ParticleSystem>(true))
                {
                    ps.Simulate(0.1f, true, true);
                    ps.Play(true);
                }

                // 활성 카메라가 매 프레임 RT로 렌더 → 이 사이에 변형 컴파일 + 리소스 업로드가 일어난다.
                for (int f = 0; f < frames; f++)
                    yield return null;

                UnityEngine.Object.Destroy(inst);
                onProgress?.Invoke((i + 1f) / n);
            }

            cam.targetTexture = null;
            UnityEngine.Object.Destroy(rt);
            UnityEngine.Object.Destroy(camGO);
            UnityEngine.Object.Destroy(root);
        }

        HasWarmedUp = true;
        onProgress?.Invoke(1f);
    }
}
