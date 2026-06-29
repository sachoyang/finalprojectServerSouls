using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// [역할] 카테고리 기반 이펙트 오브젝트 풀(지속 싱글톤).
//  - Instantiate/Destroy를 반복하면 생기는 첫 생성 끊김(텍스처/메시 GPU 업로드, GC)을 없앤다.
//  - 카테고리별 '동시 활성 최대 개수'를 두고, 초과 시 가장 오래된 인스턴스를 회수해 재사용한다.
//  - 로딩(프리워밍) 때 PrewarmRoutine으로 미리 채운다. (LoadingSceneController.ExtraPreload에 자동 연결)
//
//  사용:
//    EffectPoolManager.Instance.Spawn(prefab, pos, rot);      // 파티클 끝나면(또는 autoReturnAfter 뒤) 자동 회수
//    var go = EffectPoolManager.Instance.Get(prefab, pos, rot); ... ; pool.Return(go);  // 수동 제어
//
//  배치: scLobbyMain(또는 앱 부팅 씬)에 빈 오브젝트로 1개. DontDestroyOnLoad로 보스 씬이 바뀌어도 유지/재사용.
public class EffectPoolManager : MonoBehaviour
{
    public static EffectPoolManager Instance { get; private set; }

    // 씬마다 직접 배치하지 않아도 되도록, Resources의 "EffectPoolManager" 프리팹을 게임 시작 시 1회 자동 생성한다.
    //  → 로비/디버그씬 등 어느 씬에서 시작하든 풀이 항상 존재(DontDestroyOnLoad).
    //  셋업: EffectPoolManager 컴포넌트가 붙고 config가 연결된 프리팹을 Resources/EffectPoolManager.prefab 로 둘 것.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoBootstrap()
    {
        if (Instance != null) return; // 이미 씬에 배치돼 있거나 생성됨
        var prefab = Resources.Load<GameObject>("EffectPoolManager");
        if (prefab != null) Instantiate(prefab); // Awake에서 Instance 설정 + DontDestroyOnLoad
    }

    [SerializeField] private EffectPoolConfig config;

    [Tooltip("씬 전환에도 풀을 유지(권장).")]
    [SerializeField] private bool persistAcrossScenes = true;

    [Tooltip("Awake에서 로딩 프리워밍 훅(LoadingSceneController.ExtraPreload)에 자동 등록.")]
    [SerializeField] private bool registerLoadingHook = true;

    // prefab -> 사용 가능한(비활성) 인스턴스 큐
    private readonly Dictionary<GameObject, Queue<GameObject>> _available = new Dictionary<GameObject, Queue<GameObject>>();
    // prefab -> 소속 카테고리(런타임)
    private readonly Dictionary<GameObject, CategoryRuntime> _prefabCategory = new Dictionary<GameObject, CategoryRuntime>();
    // prefab -> 설정 엔트리
    private readonly Dictionary<GameObject, EffectPoolConfig.Entry> _prefabEntry = new Dictionary<GameObject, EffectPoolConfig.Entry>();
    // 카테고리 이름 -> 런타임
    private readonly Dictionary<string, CategoryRuntime> _categories = new Dictionary<string, CategoryRuntime>();

    private class CategoryRuntime
    {
        public string name;
        public int maxActive;
        public readonly LinkedList<PooledInstance> active = new LinkedList<PooledInstance>(); // 앞=가장 오래됨
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if (persistAcrossScenes) DontDestroyOnLoad(gameObject);

        BuildFromConfig();

        if (registerLoadingHook)
            LoadingSceneController.ExtraPreload = onProgress => PrewarmRoutine(onProgress);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void BuildFromConfig()
    {
        if (config == null) { Debug.LogWarning("[EffectPool] config가 비어 있습니다."); return; }
        foreach (var cat in config.categories)
        {
            if (cat == null || string.IsNullOrEmpty(cat.name)) continue;
            if (!_categories.TryGetValue(cat.name, out var rt))
            {
                rt = new CategoryRuntime { name = cat.name, maxActive = cat.maxActive };
                _categories[cat.name] = rt;
            }
            foreach (var e in cat.prefabs)
            {
                if (e == null || e.prefab == null) continue;
                _prefabCategory[e.prefab] = rt;
                _prefabEntry[e.prefab] = e;
                if (!_available.ContainsKey(e.prefab)) _available[e.prefab] = new Queue<GameObject>();
            }
        }
    }

    // ── 로딩 때 호출: prewarmCount만큼 미리 생성(프레임 분산) ──────────────────
    public IEnumerator PrewarmRoutine(Action<float> onProgress)
    {
        if (config == null) { onProgress?.Invoke(1f); yield break; }

        int total = 0;
        foreach (var cat in config.categories)
            foreach (var e in cat.prefabs)
                if (e != null && e.prefab != null) total += Mathf.Max(0, e.prewarmCount);

        if (total == 0) { onProgress?.Invoke(1f); yield break; }

        int done = 0, sinceYield = 0;
        foreach (var cat in config.categories)
        {
            foreach (var e in cat.prefabs)
            {
                if (e == null || e.prefab == null) continue;
                for (int i = 0; i < e.prewarmCount; i++)
                {
                    var inst = CreateNew(e.prefab);
                    Deactivate(inst);
                    _available[e.prefab].Enqueue(inst);

                    done++;
                    onProgress?.Invoke(done / (float)total);

                    if (++sinceYield >= 3) { sinceYield = 0; yield return null; } // 멈춤 방지(프레임 분산)
                }
            }
        }
        onProgress?.Invoke(1f);
    }

    // ── 꺼내기 ────────────────────────────────────────────────
    // 수동 제어용. 다 쓰면 Return(go) 호출해야 한다.
    public GameObject Get(GameObject prefab, Vector3 pos, Quaternion rot, Transform parent = null)
    {
        if (prefab == null) return null;

        if (!_available.TryGetValue(prefab, out var q))
        {
            RegisterUnconfigured(prefab);
            q = _available[prefab];
        }

        var cat = _prefabCategory[prefab];

        // 카테고리 최대치 초과 → 가장 오래된 활성 인스턴스 회수해 슬롯 확보
        if (cat.maxActive > 0 && cat.active.Count >= cat.maxActive)
        {
            var oldest = cat.active.First != null ? cat.active.First.Value : null;
            if (oldest != null) Return(oldest.gameObject);
        }

        GameObject inst = q.Count > 0 ? q.Dequeue() : CreateNew(prefab);

        var t = inst.transform;
        t.SetParent(parent, false);
        t.SetPositionAndRotation(pos, rot);
        Activate(inst);

        var pi = inst.GetComponent<PooledInstance>();
        pi.activeNode = cat.active.AddLast(pi);
        return inst;
    }

    // 정적 편의용: 풀이 있으면 풀에서 Spawn, 없으면(설정 누락 등) 안전하게 직접 생성.
    //  호출측을 간결하게: EffectPoolManager.SpawnPooled(prefab, pos, rot);
    public static GameObject SpawnPooled(GameObject prefab, Vector3 pos, Quaternion rot, Transform parent = null)
    {
        if (prefab == null) return null;
        if (Instance != null) return Instance.Spawn(prefab, pos, rot, parent);
        // 풀 매니저가 없으면 폴백(누수 가능 — Resources/EffectPoolManager.prefab 셋업 확인 필요)
        return Instantiate(prefab, pos, rot, parent);
    }

    // 편의용. 파티클이 끝나면(또는 autoReturnAfter 뒤) 자동으로 풀에 반환된다. 일반 이펙트는 이걸 쓰면 됨.
    public GameObject Spawn(GameObject prefab, Vector3 pos, Quaternion rot, Transform parent = null)
    {
        var inst = Get(prefab, pos, rot, parent);
        if (inst == null) return null;

        var ar = inst.GetComponent<AutoReturnToPool>();
        if (ar == null) ar = inst.AddComponent<AutoReturnToPool>();
        float life = _prefabEntry.TryGetValue(prefab, out var e) ? e.autoReturnAfter : 0f;
        ar.Begin(life);
        return inst;
    }

    // ── 반환 ──────────────────────────────────────────────────
    public void Return(GameObject instance)
    {
        if (instance == null) return;

        var pi = instance.GetComponent<PooledInstance>();
        if (pi == null || pi.sourcePrefab == null) { Destroy(instance); return; } // 풀 소속이 아니면 그냥 파괴

        if (pi.activeNode != null)
        {
            if (pi.activeNode.List != null) pi.activeNode.List.Remove(pi.activeNode);
            pi.activeNode = null;
        }

        Deactivate(instance);
        instance.transform.SetParent(transform, false);

        if (!_available.TryGetValue(pi.sourcePrefab, out var q))
        {
            q = new Queue<GameObject>();
            _available[pi.sourcePrefab] = q;
        }
        q.Enqueue(instance);
    }

    // ── 내부 ──────────────────────────────────────────────────
    private GameObject CreateNew(GameObject prefab)
    {
        var inst = Instantiate(prefab, transform);
        var pi = inst.GetComponent<PooledInstance>();
        if (pi == null) pi = inst.AddComponent<PooledInstance>();
        pi.sourcePrefab = prefab;
        pi.category = _prefabCategory.TryGetValue(prefab, out var c) ? c.name : "Unsorted";
        pi.manager = this;
        return inst;
    }

    private void RegisterUnconfigured(GameObject prefab)
    {
        if (!_categories.TryGetValue("Unsorted", out var rt))
        {
            rt = new CategoryRuntime { name = "Unsorted", maxActive = 0 };
            _categories["Unsorted"] = rt;
        }
        _prefabCategory[prefab] = rt;
        if (!_available.ContainsKey(prefab)) _available[prefab] = new Queue<GameObject>();
        Debug.LogWarning($"[EffectPool] 설정에 없는 프리팹 '{prefab.name}' — Unsorted(무제한)로 동적 등록. config에 추가를 권장.");
    }

    private void Activate(GameObject go)
    {
        go.SetActive(true);
        foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
        {
            ps.Clear(true);
            ps.Play(true);
        }
    }

    private void Deactivate(GameObject go)
    {
        foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        go.SetActive(false);
    }
}
