using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// BossPatternModule 전용 인스펙터.
///
/// 보스 프리팹을 하나 지정해두면, 패턴에 들어있는 액션들을 **처음부터 끝까지 순서대로** 재생한다.
/// FBX 프리뷰처럼 ▶/⏸ 로 돌리고 타임라인을 드래그해서 원하는 지점으로 스크럽할 수 있으며,
/// 각 액션의 actionSoundPercent 지점을 지날 때 actionSound가 실제로 울린다.
/// → 실제 게임에서 들리는 것과 같은 순서/타이밍으로 사운드를 검수할 수 있다.
///
/// 지정한 프리팹은 EditorPrefs에 저장되므로 커밋되지 않고, 팀원마다 각자 고르면 된다.
/// </summary>
[CustomEditor(typeof(BossPatternModule))]
public class BossPatternModuleEditor : Editor
{
    private const string PreviewPrefabPrefsKey = "ServerSouls.BossPatternPreview.Prefab";
    private static readonly Color BackgroundColor = new Color(0.18f, 0.18f, 0.18f, 1f);
    private static readonly Color[] SegmentColors =
    {
        new Color(0.25f, 0.55f, 0.85f, 0.85f),
        new Color(0.30f, 0.70f, 0.55f, 0.85f),
    };

    private PreviewRenderUtility _previewUtility;
    private GameObject _previewPrefab;
    private GameObject _previewInstance;

    // 애니메이션 클립의 커브 경로는 Animator가 붙은 오브젝트 기준이다.
    // 보스 프리팹은 Animator가 중첩 프리팹(FBX) 안쪽 자식에 있으므로,
    // 루트에 SampleAnimation을 호출하면 경로가 안 맞아 아무것도 움직이지 않는다.
    private Animator _previewAnimator;
    private GameObject SampleTarget =>
        _previewAnimator != null ? _previewAnimator.gameObject : _previewInstance;

    // 패턴 전체 타임라인 재생 상태
    private bool _playing;
    private bool _loop = true;
    private float _speed = 1f;
    private float _time;                 // 패턴 전체 기준 경과 시간(초)
    private double _lastEditorTime;
    private bool[] _soundFired;          // 액션별 사운드 재생 여부

    // 카메라 궤도
    private float _yaw = 140f;
    private float _pitch = 12f;
    private float _distance = 6f;
    private Bounds _bounds = new Bounds(Vector3.up, Vector3.one * 2f);

    private BossPatternModule Module => (BossPatternModule)target;

    private float TotalDuration
    {
        get
        {
            float sum = 0f;
            for (int i = 0; i < Module.ActionCount; i++) sum += Mathf.Max(0f, Module.GetAction(i).duration);
            return sum;
        }
    }

    private void OnEnable()
    {
        string guid = EditorPrefs.GetString(PreviewPrefabPrefsKey, string.Empty);
        if (!string.IsNullOrEmpty(guid))
        {
            _previewPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
        }

        _lastEditorTime = EditorApplication.timeSinceStartup;
        EditorApplication.update += TickPreview;
    }

    private void OnDisable()
    {
        EditorApplication.update -= TickPreview;
        StopPlayback();
        CleanupPreview();
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("🔊 패턴 미리보기 (전체 시퀀스 + 사운드)", EditorStyles.boldLabel);

        DrawPrefabPicker();

        if (_previewPrefab == null)
        {
            EditorGUILayout.HelpBox(
                "보스 프리팹을 지정하면 이 패턴의 액션들이 순서대로 재생되고, 각 액션의 actionSound가 함께 들립니다.\n" +
                "예: PolarDragonBoss.prefab, BossOrkAssasin.prefab",
                MessageType.Info);
            return;
        }

        if (Module.ActionCount == 0)
        {
            EditorGUILayout.HelpBox("이 패턴에 액션이 없습니다.", MessageType.Warning);
            return;
        }

        DrawViewport();
        DrawTransportControls();
        DrawTimeline();
        DrawActionList();
    }

    private void DrawPrefabPicker()
    {
        EditorGUI.BeginChangeCheck();
        GameObject picked = (GameObject)EditorGUILayout.ObjectField(
            new GUIContent("미리보기 보스 프리팹", "이 설정은 내 PC에만 저장됩니다(EditorPrefs). 커밋되지 않습니다."),
            _previewPrefab, typeof(GameObject), false);

        if (EditorGUI.EndChangeCheck())
        {
            _previewPrefab = picked;
            StopPlayback();
            CleanupPreview();

            string guid = picked != null
                ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(picked))
                : string.Empty;
            EditorPrefs.SetString(PreviewPrefabPrefsKey, guid);
        }
    }

    // ==========================================
    // 재생 컨트롤 (FBX 프리뷰 느낌)
    // ==========================================
    private void DrawTransportControls()
    {
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button(_playing ? "⏸ 일시정지" : "▶ 재생", GUILayout.Width(90f)))
        {
            _playing = !_playing;
            _lastEditorTime = EditorApplication.timeSinceStartup;
            if (!_playing) StopPreviewAudio();
        }

        if (GUILayout.Button("⏮ 처음으로", GUILayout.Width(90f)))
        {
            SeekTo(0f);
        }

        _loop = GUILayout.Toggle(_loop, "반복", EditorStyles.miniButton, GUILayout.Width(50f));

        EditorGUILayout.LabelField("속도", GUILayout.Width(30f));
        _speed = EditorGUILayout.Slider(_speed, 0.1f, 2f);

        EditorGUILayout.EndHorizontal();

        float total = TotalDuration;
        EditorGUILayout.LabelField(
            $"{_time:0.00}s / {total:0.00}s   —   현재 액션: {DescribeCurrentAction()}",
            EditorStyles.miniLabel);
    }

    private string DescribeCurrentAction()
    {
        int idx = GetActionIndexAt(_time, out float local);
        if (idx < 0) return "-";

        BossActionModule a = Module.GetAction(idx);
        string name = string.IsNullOrEmpty(a.animationStateName) ? "(이름 없음)" : a.animationStateName;
        return $"[{idx}] {name}  ({local:0.00}s)";
    }

    // ==========================================
    // 타임라인 — 액션 구간 + 사운드 마커 + 스크럽
    // ==========================================
    private void DrawTimeline()
    {
        float total = TotalDuration;
        if (total <= 0f) return;

        Rect rect = GUILayoutUtility.GetRect(10f, 34f, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(rect, new Color(0.1f, 0.1f, 0.1f));

        // 액션 구간을 색을 번갈아 칠해서 경계를 보이게
        float cursorX = rect.x;
        for (int i = 0; i < Module.ActionCount; i++)
        {
            BossActionModule action = Module.GetAction(i);
            float w = rect.width * (Mathf.Max(0f, action.duration) / total);

            Rect seg = new Rect(cursorX, rect.y, w, rect.height);
            EditorGUI.DrawRect(seg, SegmentColors[i % SegmentColors.Length]);

            // 사운드가 울리는 지점 = 구간 시작 + duration * percent
            if (action.actionSound != null && action.duration > 0f)
            {
                float sx = seg.x + seg.width * Mathf.Clamp01(action.actionSoundPercent);
                EditorGUI.DrawRect(new Rect(sx - 1f, rect.y, 2f, rect.height), Color.yellow);
            }

            if (w > 30f)
            {
                GUI.Label(new Rect(seg.x + 3f, seg.y + 1f, seg.width - 6f, 14f),
                    i.ToString(), EditorStyles.whiteMiniLabel);
            }

            cursorX += w;
        }

        // 재생 헤드
        float headX = rect.x + rect.width * Mathf.Clamp01(_time / total);
        EditorGUI.DrawRect(new Rect(headX - 1f, rect.y - 2f, 2f, rect.height + 4f), Color.white);

        HandleScrub(rect, total);

        EditorGUILayout.LabelField("노란 선 = 사운드가 울리는 지점. 타임라인을 드래그하면 스크럽됩니다.", EditorStyles.miniLabel);
    }

    // hotControl을 잡아야 마우스가 타임라인 밖으로 나가도 드래그가 계속 따라온다.
    // (안 잡으면 MouseDrag 이벤트가 이 컨트롤로 안 오고 한 칸 찍고 끊긴다)
    private void HandleScrub(Rect rect, float total)
    {
        Event e = Event.current;
        int id = GUIUtility.GetControlID("BossPatternTimeline".GetHashCode(), FocusType.Passive, rect);

        switch (e.GetTypeForControl(id))
        {
            case EventType.MouseDown:
                if (e.button == 0 && rect.Contains(e.mousePosition))
                {
                    GUIUtility.hotControl = id;
                    _wasPlayingBeforeScrub = _playing;
                    _playing = false; // 스크럽 중엔 자동 재생 정지
                    ScrubToMouse(rect, total, e);
                    e.Use();
                }
                break;

            case EventType.MouseDrag:
                if (GUIUtility.hotControl == id)
                {
                    ScrubToMouse(rect, total, e);
                    e.Use();
                }
                break;

            case EventType.MouseUp:
                if (GUIUtility.hotControl == id)
                {
                    GUIUtility.hotControl = 0;
                    // 드래그 전에 재생 중이었으면 이어서 재생
                    _playing = _wasPlayingBeforeScrub;
                    _lastEditorTime = EditorApplication.timeSinceStartup;
                    e.Use();
                }
                break;
        }
    }

    private bool _wasPlayingBeforeScrub;

    private void ScrubToMouse(Rect rect, float total, Event e)
    {
        float t = Mathf.Clamp01((e.mousePosition.x - rect.x) / rect.width) * total;
        SeekTo(t);
        Repaint();
    }

    // 스크럽으로 건너뛴 구간의 사운드는 울리지 않게 "이미 재생됨"으로 표시한다.
    // 안 그러면 뒤로 드래그할 때마다 지나온 소리가 한꺼번에 터진다.
    private void SeekTo(float time)
    {
        _time = Mathf.Clamp(time, 0f, TotalDuration);
        StopPreviewAudio();
        EnsureSoundFiredArray();

        int current = GetActionIndexAt(_time, out float local);
        for (int i = 0; i < _soundFired.Length; i++)
        {
            if (i < current) { _soundFired[i] = true; continue; }
            if (i > current) { _soundFired[i] = false; continue; }

            BossActionModule a = Module.GetAction(i);
            float progress = a.duration > 0f ? local / a.duration : 1f;
            _soundFired[i] = progress >= a.actionSoundPercent;
        }

        SampleAt(_time);
    }

    // ==========================================
    // 액션 목록 (읽기 전용 요약)
    // ==========================================
    private void DrawActionList()
    {
        EditorGUILayout.Space(4);

        int current = GetActionIndexAt(_time, out _);

        for (int i = 0; i < Module.ActionCount; i++)
        {
            BossActionModule action = Module.GetAction(i);

            EditorGUILayout.BeginHorizontal(i == current ? "SelectionRect" : "box");

            string name = string.IsNullOrEmpty(action.animationStateName) ? "(상태 이름 없음)" : action.animationStateName;
            EditorGUILayout.LabelField($"[{i}] {name}  {action.duration:0.00}s", GUILayout.MinWidth(150f));

            string soundLabel = action.actionSound != null
                ? $"🔊 {action.actionSound.name} @ {action.actionSoundPercent:P0}"
                : "무음";
            EditorGUILayout.LabelField(soundLabel, EditorStyles.miniLabel, GUILayout.MinWidth(140f));

            if (GUILayout.Button("여기로", GUILayout.Width(55f)))
            {
                SeekTo(GetActionStartTime(i));
            }

            EditorGUILayout.EndHorizontal();
        }
    }

    // ==========================================
    // 시간 → 액션 매핑
    // ==========================================
    private float GetActionStartTime(int index)
    {
        float t = 0f;
        for (int i = 0; i < index && i < Module.ActionCount; i++) t += Mathf.Max(0f, Module.GetAction(i).duration);
        return t;
    }

    private int GetActionIndexAt(float time, out float localTime)
    {
        localTime = 0f;
        if (Module.ActionCount == 0) return -1;

        float cursor = 0f;
        for (int i = 0; i < Module.ActionCount; i++)
        {
            float d = Mathf.Max(0f, Module.GetAction(i).duration);
            if (time < cursor + d || i == Module.ActionCount - 1)
            {
                localTime = Mathf.Clamp(time - cursor, 0f, d);
                return i;
            }
            cursor += d;
        }

        return Module.ActionCount - 1;
    }

    // ==========================================
    // 프레임 갱신
    // ==========================================
    private void TickPreview()
    {
        double now = EditorApplication.timeSinceStartup;
        float dt = (float)(now - _lastEditorTime);
        _lastEditorTime = now;

        if (!_playing || _previewInstance == null) return;

        float total = TotalDuration;
        if (total <= 0f) return;

        EnsureSoundFiredArray();
        _time += dt * _speed;

        if (_time >= total)
        {
            if (_loop)
            {
                _time -= total;
                Array.Clear(_soundFired, 0, _soundFired.Length);
            }
            else
            {
                _time = total;
                _playing = false;
            }
        }

        FireSoundsUpTo(_time);
        SampleAt(_time);
        Repaint();
    }

    // 재생 헤드가 지나간 액션의 사운드를 순서대로 울린다.
    private void FireSoundsUpTo(float time)
    {
        int current = GetActionIndexAt(time, out float local);
        if (current < 0) return;

        for (int i = 0; i <= current; i++)
        {
            if (_soundFired[i]) continue;

            BossActionModule a = Module.GetAction(i);
            if (a.actionSound == null) { _soundFired[i] = true; continue; }

            // 이전 액션이면 무조건 지나간 것, 현재 액션이면 퍼센트 도달 여부로 판정.
            bool reached = i < current;
            if (!reached)
            {
                float progress = a.duration > 0f ? local / a.duration : 1f;
                reached = progress >= a.actionSoundPercent;
            }

            if (!reached) continue;

            _soundFired[i] = true;
            PlayPreviewAudio(a.actionSound);
        }
    }

    private void EnsureSoundFiredArray()
    {
        if (_soundFired == null || _soundFired.Length != Module.ActionCount)
        {
            _soundFired = new bool[Module.ActionCount];
        }
    }

    // 클립을 duration에 맞춰 늘리거나 줄여서 샘플링한다(런타임 SetAnimSpeed와 같은 방식).
    private void SampleAt(float time)
    {
        if (_previewInstance == null) return;

        int idx = GetActionIndexAt(time, out float local);
        if (idx < 0) return;

        BossActionModule action = Module.GetAction(idx);
        if (action.animationClip == null) return;

        GameObject target = SampleTarget;
        if (target == null) return;

        float normalized = action.duration > 0f ? Mathf.Clamp01(local / action.duration) : 0f;
        action.animationClip.SampleAnimation(target, normalized * action.animationClip.length);
    }

    // ==========================================
    // 뷰포트
    // ==========================================
    private void DrawViewport()
    {
        Rect rect = GUILayoutUtility.GetRect(10f, 260f, GUILayout.ExpandWidth(true));
        HandleOrbitInput(rect);

        EnsurePreview();
        if (_previewUtility == null || _previewInstance == null)
        {
            EditorGUI.DrawRect(rect, new Color(0.12f, 0.12f, 0.12f));
            return;
        }

        Vector3 center = _bounds.center;
        float radius = Mathf.Max(0.5f, _bounds.extents.magnitude);
        Quaternion orbit = Quaternion.Euler(_pitch, _yaw, 0f);
        float dist = Mathf.Clamp(_distance, radius * 1.2f, radius * 8f);

        Camera cam = _previewUtility.camera;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = BackgroundColor;
        cam.fieldOfView = 35f;
        cam.nearClipPlane = 0.01f;
        cam.farClipPlane = 500f;
        cam.transform.position = center + orbit * new Vector3(0f, radius * 0.25f, -dist);
        cam.transform.LookAt(center);

        _previewUtility.BeginPreview(rect, GUIStyle.none);
        _previewUtility.Render(true);
        Texture tex = _previewUtility.EndPreview();
        GUI.DrawTexture(rect, tex, ScaleMode.StretchToFill, false);
    }

    private void HandleOrbitInput(Rect rect)
    {
        Event e = Event.current;
        if (!rect.Contains(e.mousePosition)) return;

        if (e.type == EventType.MouseDrag && e.button == 0)
        {
            _yaw -= e.delta.x * 0.5f;
            _pitch = Mathf.Clamp(_pitch + e.delta.y * 0.5f, -20f, 60f);
            e.Use();
            Repaint();
        }
        else if (e.type == EventType.ScrollWheel)
        {
            float radius = Mathf.Max(0.5f, _bounds.extents.magnitude);
            _distance = Mathf.Clamp(_distance + e.delta.y * radius * 0.1f, radius * 1.2f, radius * 8f);
            e.Use();
            Repaint();
        }
    }

    // ==========================================
    // 에디터 오디오 재생 (UnityEditor.AudioUtil은 internal이라 리플렉션으로 접근)
    // ==========================================
    private static MethodInfo _playClipMethod;
    private static MethodInfo _stopClipsMethod;
    private static bool _audioReflectionResolved;

    private static void ResolveAudioReflection()
    {
        if (_audioReflectionResolved) return;
        _audioReflectionResolved = true;

        Type audioUtil = typeof(AudioImporter).Assembly.GetType("UnityEditor.AudioUtil");
        if (audioUtil == null) return;

        // Unity 2020+ : PlayPreviewClip(clip, startSample, loop)
        _playClipMethod = audioUtil.GetMethod(
            "PlayPreviewClip",
            BindingFlags.Static | BindingFlags.Public,
            null,
            new[] { typeof(AudioClip), typeof(int), typeof(bool) },
            null);

        _stopClipsMethod = audioUtil.GetMethod(
            "StopAllPreviewClips",
            BindingFlags.Static | BindingFlags.Public);
    }

    private static void PlayPreviewAudio(AudioClip clip)
    {
        if (clip == null) return;
        ResolveAudioReflection();

        if (_playClipMethod == null)
        {
            Debug.LogWarning("[BossPatternPreview] 이 Unity 버전에서는 에디터 사운드 미리듣기를 지원하지 않습니다. 애니메이션만 재생됩니다.");
            return;
        }

        _playClipMethod.Invoke(null, new object[] { clip, 0, false });
    }

    private static void StopPreviewAudio()
    {
        ResolveAudioReflection();
        _stopClipsMethod?.Invoke(null, null);
    }

    // ==========================================
    // 프리뷰 씬
    // ==========================================
    private void StopPlayback()
    {
        _playing = false;
        StopPreviewAudio();
    }

    private void EnsurePreview()
    {
        if (_previewPrefab == null) return;
        if (_previewUtility != null && _previewInstance != null) return;

        CleanupPreview();

        _previewUtility = new PreviewRenderUtility();
        _previewUtility.lights[0].intensity = 1.2f;
        _previewUtility.lights[0].transform.rotation = Quaternion.Euler(40f, 40f, 0f);
        if (_previewUtility.lights.Length > 1) _previewUtility.lights[1].intensity = 0.6f;

        _previewInstance = (GameObject)Instantiate(_previewPrefab);
        _previewInstance.hideFlags = HideFlags.HideAndDontSave;
        _previewUtility.AddSingleGO(_previewInstance);

        // 프리뷰 인스턴스에서 게임 로직(NetworkBehaviour 등)이 돌면 안 된다.
        // 단 Animator는 끄지 않는다 — SampleAnimation이 이 컴포넌트를 기준으로 본을 포즈시킨다.
        foreach (MonoBehaviour mb in _previewInstance.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (mb != null) mb.enabled = false;
        }

        _previewAnimator = _previewInstance.GetComponentInChildren<Animator>(true);
        if (_previewAnimator == null)
        {
            Debug.LogWarning($"[BossPatternPreview] '{_previewPrefab.name}' 에서 Animator를 찾지 못했습니다. 애니메이션이 재생되지 않습니다.");
        }

        CalculateBounds();
        _distance = Mathf.Max(0.5f, _bounds.extents.magnitude) * 2.5f;

        EnsureSoundFiredArray();
        SampleAt(_time);
    }

    private void CalculateBounds()
    {
        Renderer[] renderers = _previewInstance.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            _bounds = new Bounds(Vector3.up, Vector3.one * 2f);
            return;
        }

        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
        _bounds = b;
    }

    private void CleanupPreview()
    {
        if (_previewInstance != null)
        {
            DestroyImmediate(_previewInstance);
            _previewInstance = null;
        }
        _previewAnimator = null;

        if (_previewUtility != null)
        {
            _previewUtility.Cleanup();
            _previewUtility = null;
        }
    }
}
