using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PlayerAbilityModule))]
public class PlayerAbilityModuleEditor : Editor
{
    private const string PreviewPlayerPrefsKey = "ServerSouls.SkillPreview.PlayerPrefab";
    private const string PreviewAutoPlayPrefsKey = "ServerSouls.SkillPreview.AutoPlay";
    private const string PreviewLoopPrefsKey = "ServerSouls.SkillPreview.Loop";
    private static readonly Color PreviewBackgroundColor = new Color(0.18f, 0.18f, 0.18f, 1f);

    // 인스펙터 폴드아웃(Fold-out) 상태 저장 변수들
    private static bool _rewardOpen = true;
    private static bool _activeSettingsOpen = true;
    private static bool _effectOpen = true;
    private static bool _passiveStatsOpen = true;
    private static bool _presentationOpen = true;
    private static bool _vfxOpen = true;
    private static bool _hitboxOpen = true;
    private static bool _previewOpen = true;
    private static bool _soundOpen = true;
    private static int _selectedLevelIndex;

    // 프로퍼티 매핑용 변수들
    private SerializedProperty _abilityId;
    private SerializedProperty _displayName;
    private SerializedProperty _description;
    private SerializedProperty _icon;
    private SerializedProperty _abilityType;
    private SerializedProperty _minBossStage;
    private SerializedProperty _maxBossStage;
    private SerializedProperty _unlockedSkill;
    private SerializedProperty _basicSkill;
    private SerializedProperty _maxLevel;
    private SerializedProperty _levelSettings;
    private SerializedProperty _specialEffect;
    private SerializedProperty _animationClip;
    private SerializedProperty _animationStateName;
    private SerializedProperty _animationTrigger;
    private SerializedProperty _animationSpeed;
    private SerializedProperty _effectPrefab;
    private SerializedProperty _effectLocalOffset;
    private SerializedProperty _parentEffectToPlayer;
    private SerializedProperty _hitboxPrefab;
    private SerializedProperty _hitboxLocalOffset;
    private SerializedProperty _hitboxRevivePower;
    private SerializedProperty _hitboxDelay;
    private SerializedProperty _hitboxLifetime;
    private SerializedProperty _hitEvents;
    private SerializedProperty _bitIndex;
    private SerializedProperty _soundClip;
    private SerializedProperty _soundVolume;
    private SerializedProperty _soundDelay;

    // 프리뷰 유틸리티 관련 변수들
    private PreviewRenderUtility _previewUtility;
    private GameObject _previewPlayerPrefab;
    private GameObject _previewPlayer;
    private Animator _previewAnimator;
    private GameObject _previewFloor;
    private GameObject _previewVfx;
    private GameObject _hitboxVisual;

    // 💡 최적화: 매 프레임 GetComponent 검색 및 GC Alloc(가비지 생성) 방지를 위한 캐싱 리스트
    private readonly List<GameObject> _hitEventVisuals = new List<GameObject>();
    private readonly List<Material> _hitEventMaterials = new List<Material>();
    private readonly List<Mesh> _hitEventMeshes = new List<Mesh>();
    private readonly List<Renderer> _hitEventRenderers = new List<Renderer>(); // 추가: 실시간 렌더러 캐싱용

    private Material _floorMaterial;
    private Mesh _floorMesh;
    private Texture2D _floorTexture;
    private Texture2D _previewBackgroundTexture;
    private GUIStyle _previewBackgroundStyle;
    private Material _hitboxMaterial;
    private ParticleSystem[] _particles = Array.Empty<ParticleSystem>();
    private Bounds _playerPreviewBounds = new Bounds(Vector3.up, Vector3.one * 2f);
    private Vector3 _previewGroundCenter;
    private Vector3 _previewCameraGroundCenter;
    private float _previewGroundY;
    private float _previewPivotHeight = 1f;
    private float _previewOrbitRadius = 1f;
    private bool _previewPlaying;
    private bool _previewAutoPlay;
    private bool _previewLoop;
    private float _previewTime;
    private double _lastEditorTime;
    private float _previewYaw = 145f;
    private float _previewPitch = 15f;
    private float _previewDistance = 4.5f;

    private PlayerAbilityModule Module => (PlayerAbilityModule)target;

    private void OnEnable()
    {
        _bitIndex = serializedObject.FindProperty("bitIndex");
        _abilityId = serializedObject.FindProperty("abilityId");
        _displayName = serializedObject.FindProperty("displayName");
        _description = serializedObject.FindProperty("description");
        _icon = serializedObject.FindProperty("icon");
        _abilityType = serializedObject.FindProperty("abilityType");
        _minBossStage = serializedObject.FindProperty("minBossStage");
        _maxBossStage = serializedObject.FindProperty("maxBossStage");
        _unlockedSkill = serializedObject.FindProperty("unlockedSkill");
        _basicSkill = serializedObject.FindProperty("basicSkill");
        _maxLevel = serializedObject.FindProperty("maxLevel");
        _levelSettings = serializedObject.FindProperty("levelSettings");
        _specialEffect = serializedObject.FindProperty("specialEffect");
        _animationClip = serializedObject.FindProperty("animationClip");
        _animationStateName = serializedObject.FindProperty("animationStateName");
        _animationTrigger = serializedObject.FindProperty("animationTrigger");
        _animationSpeed = serializedObject.FindProperty("animationSpeed");
        _effectPrefab = serializedObject.FindProperty("effectPrefab");
        _effectLocalOffset = serializedObject.FindProperty("effectLocalOffset");
        _parentEffectToPlayer = serializedObject.FindProperty("parentEffectToPlayer");
        _hitboxPrefab = serializedObject.FindProperty("hitboxPrefab");
        _hitboxLocalOffset = serializedObject.FindProperty("hitboxLocalOffset");
        _hitboxRevivePower = serializedObject.FindProperty("hitboxRevivePower");
        _hitboxDelay = serializedObject.FindProperty("hitboxDelay");
        _hitboxLifetime = serializedObject.FindProperty("hitboxLifetime");
        _hitEvents = serializedObject.FindProperty("hitEvents");
        _bitIndex = serializedObject.FindProperty("bitIndex");
        _soundClip = serializedObject.FindProperty("soundClip");
        _soundVolume = serializedObject.FindProperty("soundVolume");
        _soundDelay = serializedObject.FindProperty("soundDelay");

        // 에디터 세팅 데이터 로드
        string prefabGuid = EditorPrefs.GetString(PreviewPlayerPrefsKey, string.Empty);
        string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuid);
        _previewPlayerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        _previewAutoPlay = EditorPrefs.GetBool(PreviewAutoPlayPrefsKey, false);
        _previewLoop = EditorPrefs.GetBool(PreviewLoopPrefsKey, true);

        // 에디터 글로벌 업데이트 루프 연결 (실시간 프리뷰 재생용)
        EditorApplication.update += UpdatePreviewPlayback;
    }

    private void OnDisable()
    {
        EditorApplication.update -= UpdatePreviewPlayback;
        EditorGUIUtility.SetWantsMouseJumping(0);
        EditorPrefs.SetBool(PreviewAutoPlayPrefsKey, _previewAutoPlay);
        EditorPrefs.SetBool(PreviewLoopPrefsKey, _previewLoop);
        CleanupPreview();
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        _abilityType.enumValueIndex = EditorGUILayout.Popup(
            "스킬 분류",
            _abilityType.enumValueIndex,
            new[] { "패시브", "액티브", "유틸리티" });
        bool isActive = _abilityType.enumValueIndex == (int)AbilityType.Active;
        bool isPassive = _abilityType.enumValueIndex == (int)AbilityType.Passive;
        bool isUtility = _abilityType.enumValueIndex == (int)AbilityType.Utility;
        bool usesAcquisitionPresentation =
            isPassive ||
            (isUtility && _specialEffect.enumValueIndex != (int)PlayerAbilitySpecialEffect.None);
        EnsureLevelSettingsInitialized();

        // 섹션별 렌더링 코드들
        DrawSection("보상 설정", ref _rewardOpen, DrawReward);
        DrawSection(
            "레벨 설정",
            ref _activeSettingsOpen,
            () => DrawLevelSettings(isActive, isPassive, isUtility));

        DrawSection(usesAcquisitionPresentation ? "획득 연출" : "사용 연출", ref _presentationOpen, DrawPresentation);
        DrawSection(usesAcquisitionPresentation ? "획득 VFX" : "사용 VFX", ref _vfxOpen, DrawVfx);
        DrawSection("사운드", ref _soundOpen, DrawSound);

        if (isActive)
        {
            DrawSection("공격 판정", ref _hitboxOpen, DrawHitbox);
        }

        serializedObject.ApplyModifiedProperties();

        // 프리뷰 뷰포트 드로잉
        DrawSection("미리보기", ref _previewOpen, DrawEmbeddedPreview);
    }

    private void DrawReward()
    {
        EditorGUILayout.PropertyField(_bitIndex, new GUIContent("비트 인덱스"));
        EditorGUILayout.PropertyField(_abilityId, new GUIContent("스킬 ID"));
        EditorGUILayout.PropertyField(_displayName, new GUIContent("표시 이름"));
        EditorGUILayout.PropertyField(_description, new GUIContent("설명"));
        EditorGUILayout.PropertyField(_icon, new GUIContent("아이콘"));
        EditorGUILayout.PropertyField(_minBossStage, new GUIContent("최소 등장 스테이지"));
        EditorGUILayout.PropertyField(_maxBossStage, new GUIContent("최대 등장 스테이지"));
        EditorGUILayout.PropertyField(_unlockedSkill, new GUIContent("해금된 스킬"));
        EditorGUILayout.PropertyField(_basicSkill, new GUIContent("기본 스킬"));
    }

    private void DrawLevelSettings(bool isActive, bool isPassive, bool isUtility)
    {
        DrawLevelSelector();
        SerializedProperty level = _levelSettings.GetArrayElementAtIndex(_selectedLevelIndex);

        bool isUsableUtility =
            isUtility &&
            _specialEffect.enumValueIndex == (int)PlayerAbilitySpecialEffect.None;

        if (isUtility)
        {
            EditorGUILayout.PropertyField(_specialEffect, new GUIContent("특수 효과"));
        }

        if (isActive || isUsableUtility)
        {
            EditorGUILayout.PropertyField(
                level.FindPropertyRelative("cooldownSeconds"),
                new GUIContent("쿨타임"));
            EditorGUILayout.PropertyField(
                level.FindPropertyRelative("staminaCost"),
                new GUIContent("스태미나 소모량"));
        }

        if (isActive)
        {
            EditorGUILayout.PropertyField(
                level.FindPropertyRelative("damageMultiplier"),
                new GUIContent("스킬 레벨 배율"));
        }
        else if (isPassive)
        {
            EditorGUILayout.PropertyField(
                level.FindPropertyRelative("maxHealthBonus"),
                new GUIContent("최대 체력 보너스"));
            EditorGUILayout.PropertyField(
                level.FindPropertyRelative("maxStaminaBonus"),
                new GUIContent("최대 스태미나 보너스"));
            EditorGUILayout.PropertyField(
                level.FindPropertyRelative("defenseBonusPercent"),
                new GUIContent(
                    "방어율 보너스 (%)",
                    "현재 방어율에 합연산됩니다. 10 입력 시 10%, 100 입력 시 100%입니다."));
            EditorGUILayout.PropertyField(
                level.FindPropertyRelative("attackDamageBonusPercent"),
                new GUIContent(
                    "공격력 보너스 (%)",
                    "현재 공격력 증가율에 합연산됩니다. 10 입력 시 10%, 100 입력 시 100%입니다."));
        }
        else if (isUsableUtility)
        {
            EditorGUILayout.PropertyField(
                level.FindPropertyRelative("healthRestoreAmount"),
                new GUIContent("체력 회복량"));
            EditorGUILayout.PropertyField(
                level.FindPropertyRelative("staminaRestoreAmount"),
                new GUIContent("스태미나 회복량"));
        }
    }

    private void DrawLevelSelector()
    {
        EditorGUILayout.PropertyField(_maxLevel, new GUIContent("최대 레벨"));
        int maxLevel = Mathf.Clamp(_maxLevel.intValue, 1, byte.MaxValue);
        if (_maxLevel.intValue != maxLevel)
        {
            _maxLevel.intValue = maxLevel;
        }

        string[] levelLabels = new string[maxLevel];
        for (int i = 0; i < maxLevel; i++)
            levelLabels[i] = $"Lv.{i + 1}";

        _selectedLevelIndex = EditorGUILayout.Popup(
            "레벨 설정",
            Mathf.Clamp(_selectedLevelIndex, 0, maxLevel - 1),
            levelLabels);
    }

    private void EnsureLevelSettingsInitialized()
    {
        int maxLevel = Mathf.Clamp(_maxLevel.intValue, 1, byte.MaxValue);
        int previousSize = _levelSettings.arraySize;
        if (previousSize == maxLevel)
        {
            return;
        }

        _levelSettings.arraySize = maxLevel;
        for (int i = previousSize; i < maxLevel; i++)
        {
            SerializedProperty level = _levelSettings.GetArrayElementAtIndex(i);
            level.FindPropertyRelative("damageMultiplier").floatValue = 1f;
            level.FindPropertyRelative("cooldownSeconds").floatValue = 0f;
            level.FindPropertyRelative("staminaCost").floatValue = 0f;
            level.FindPropertyRelative("maxHealthBonus").floatValue = 0f;
            level.FindPropertyRelative("maxStaminaBonus").floatValue = 0f;
            level.FindPropertyRelative("defenseBonusPercent").floatValue = 0f;
            level.FindPropertyRelative("attackDamageBonusPercent").floatValue = 0f;
            level.FindPropertyRelative("healthRestoreAmount").floatValue = 0f;
            level.FindPropertyRelative("staminaRestoreAmount").floatValue = 0f;
        }
    }

    private void DrawPresentation()
    {
        EditorGUILayout.PropertyField(_animationClip, new GUIContent("애니메이션 클립"));
        EditorGUILayout.PropertyField(_animationStateName, new GUIContent("애니메이션 상태 이름"));
        EditorGUILayout.PropertyField(_animationTrigger, new GUIContent("애니메이션 트리거"));
        EditorGUILayout.PropertyField(_animationSpeed, new GUIContent("애니메이션 속도"));
    }

    private void DrawVfx()
    {
        EditorGUILayout.PropertyField(_effectPrefab, new GUIContent("이펙트 프리팹"));
        EditorGUILayout.PropertyField(_effectLocalOffset, new GUIContent("로컬 위치 오프셋"));
        EditorGUILayout.PropertyField(_parentEffectToPlayer, new GUIContent("플레이어에 연결"));
    }

    private void DrawHitbox()
    {
        DrawHitEventsList();

        // 💡 UX 최적화: Hit Events 리스트에 데이터가 없을 때만 기존 레거시 입력 칸을 노출시킵니다.
        if (_hitEvents.arraySize == 0)
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("기존 히트박스 프리팹", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_hitboxPrefab, new GUIContent("히트박스 프리팹"));
            EditorGUILayout.PropertyField(_hitboxLocalOffset, new GUIContent("로컬 위치 오프셋"));
            EditorGUILayout.PropertyField(_hitboxRevivePower, new GUIContent("부활 기여량"));
            EditorGUILayout.PropertyField(_hitboxDelay, new GUIContent("생성 지연"));
            EditorGUILayout.PropertyField(_hitboxLifetime, new GUIContent("유지 시간"));
        }
    }

    private void DrawHitEventsList()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("타격 이벤트", EditorStyles.boldLabel);
                if (GUILayout.Button("+", GUILayout.Width(28f)))
                {
                    int index = _hitEvents.arraySize;
                    _hitEvents.InsertArrayElementAtIndex(index);
                    SerializedProperty element = _hitEvents.GetArrayElementAtIndex(index);
                    InitializeHitEventElement(element, index);
                }
            }

            if (_hitEvents.arraySize == 0)
            {
                EditorGUILayout.HelpBox("Add hit events to use module-driven cylinder hit detection.", MessageType.Info);
                return;
            }

            for (int i = 0; i < _hitEvents.arraySize; i++)
            {
                SerializedProperty element = _hitEvents.GetArrayElementAtIndex(i);
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField(GetHitEventTitle(element, i), EditorStyles.boldLabel);
                        if (GUILayout.Button("-", GUILayout.Width(28f)))
                        {
                            _hitEvents.DeleteArrayElementAtIndex(i);
                            break;
                        }
                    }

                    EditorGUILayout.PropertyField(element.FindPropertyRelative("label"), new GUIContent("이름"));
                    EditorGUILayout.PropertyField(element.FindPropertyRelative("startNormalizedTime"), new GUIContent("판정 시작 시간"));
                    EditorGUILayout.PropertyField(element.FindPropertyRelative("endNormalizedTime"), new GUIContent("판정 종료 시간"));
                    EditorGUILayout.PropertyField(element.FindPropertyRelative("radius"), new GUIContent("반지름"));
                    EditorGUILayout.PropertyField(element.FindPropertyRelative("height"), new GUIContent("높이"));
                    EditorGUILayout.PropertyField(element.FindPropertyRelative("centerHeight"), new GUIContent("중심 높이"));
                    EditorGUILayout.PropertyField(
                        element.FindPropertyRelative("damageRate"),
                        new GUIContent("타격 데미지 배율"));
                    EditorGUILayout.PropertyField(element.FindPropertyRelative("groggyDamage"), new GUIContent("그로기 데미지"));
                    EditorGUILayout.PropertyField(element.FindPropertyRelative("revivePower"), new GUIContent("부활 기여량"));
                    EditorGUILayout.PropertyField(element.FindPropertyRelative("previewColor"), new GUIContent("미리보기 색상"));
                }
            }
        }
    }

    private static string GetHitEventTitle(SerializedProperty element, int index)
    {
        SerializedProperty label = element.FindPropertyRelative("label");
        return !string.IsNullOrWhiteSpace(label.stringValue)
            ? label.stringValue
            : $"Hit {index + 1}";
    }

    private static void InitializeHitEventElement(SerializedProperty element, int index)
    {
        element.FindPropertyRelative("label").stringValue = $"Hit {index + 1}";
        element.FindPropertyRelative("startNormalizedTime").floatValue = Mathf.Clamp01(0.35f + index * 0.15f);
        element.FindPropertyRelative("endNormalizedTime").floatValue = Mathf.Clamp01(0.45f + index * 0.15f);
        element.FindPropertyRelative("radius").floatValue = 1.4f;
        element.FindPropertyRelative("height").floatValue = 1.8f;
        element.FindPropertyRelative("centerHeight").floatValue = 0f;
        element.FindPropertyRelative("groggyDamage").floatValue = 10f;
        element.FindPropertyRelative("revivePower").floatValue = 34f;
        element.FindPropertyRelative("previewColor").colorValue = new Color(1f, 0.2f, 0f, 0.3f);
    }

    private void DrawSound()
    {
        EditorGUILayout.PropertyField(_soundClip, new GUIContent("사운드 클립"));
        EditorGUILayout.PropertyField(_soundVolume, new GUIContent("볼륨"));
        EditorGUILayout.PropertyField(_soundDelay, new GUIContent("재생 지연"));
    }

    private void DrawEmbeddedPreview()
    {
        EditorGUI.BeginChangeCheck();
        _previewPlayerPrefab = (GameObject)EditorGUILayout.ObjectField(
            "Preview Player Prefab",
            _previewPlayerPrefab,
            typeof(GameObject),
            false);
        if (EditorGUI.EndChangeCheck())
        {
            string path = AssetDatabase.GetAssetPath(_previewPlayerPrefab);
            EditorPrefs.SetString(PreviewPlayerPrefsKey, AssetDatabase.AssetPathToGUID(path));
            RebuildPreview();
        }

        float duration = GetPreviewDuration();
        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(_previewPlayerPrefab == null))
            {
                if (GUILayout.Button(_previewPlaying ? "Pause" : "Play", GUILayout.Width(70f)))
                {
                    TogglePreviewPlayback();
                }
            }

            if (GUILayout.Button("Stop", GUILayout.Width(55f)))
            {
                StopPreviewPlayback();
            }

            EditorGUI.BeginChangeCheck();
            _previewAutoPlay = GUILayout.Toggle(_previewAutoPlay, "Auto", GUILayout.Width(48f));
            _previewLoop = GUILayout.Toggle(_previewLoop, "Loop", GUILayout.Width(48f));
            if (EditorGUI.EndChangeCheck())
            {
                EditorPrefs.SetBool(PreviewAutoPlayPrefsKey, _previewAutoPlay);
                EditorPrefs.SetBool(PreviewLoopPrefsKey, _previewLoop);
                if (_previewAutoPlay && !_previewPlaying)
                {
                    _previewPlaying = true;
                    _lastEditorTime = EditorApplication.timeSinceStartup;
                }
            }

            EditorGUI.BeginChangeCheck();
            _previewTime = EditorGUILayout.Slider(_previewTime, 0f, duration);
            if (EditorGUI.EndChangeCheck())
            {
                _previewPlaying = false;
                RefreshPreview();
            }
        }

        DrawHitboxTimeline(duration);

        Rect previewRect = GUILayoutUtility.GetRect(10f, 220f, GUILayout.ExpandWidth(true));
        HandlePreviewInput(previewRect);
        DrawPreviewViewport(previewRect);

        if (_previewPlayerPrefab == null)
        {
            EditorGUILayout.HelpBox("Assign a player prefab to preview this skill inside the Inspector.", MessageType.Info);
        }
    }

    private void TogglePreviewPlayback()
    {
        EnsurePreview();

        // 💡 추가: 만약 재생 시간이 끝에 도달한 상태에서 Play를 누르면 0초부터 다시 시작
        float duration = GetPreviewDuration();
        if (_previewTime >= duration - 0.001f)
        {
            _previewTime = 0f;
        }

        _previewPlaying = !_previewPlaying;
        _lastEditorTime = EditorApplication.timeSinceStartup;
        RefreshPreview();
    }

    private void StopPreviewPlayback()
    {
        _previewPlaying = false;
        _previewTime = 0f;
        RefreshPreview();
    }

    private void UpdatePreviewPlayback()
    {
        // 프리뷰가 완전히 멈춰 있으면 매 에디터 프레임 길이 계산과 후속 작업을 생략한다.
        if (!_previewPlaying && !_previewAutoPlay)
        {
            return;
        }

        float duration = GetPreviewDuration();

        // 💡 수정: Auto가 켜져 있고 멈춰있을 때, 시간이 끝에 도달해 있다면 0초로 리셋하면서 재생 시작
        if (_previewAutoPlay && !_previewPlaying)
        {
            if (_previewTime >= duration - 0.001f)
            {
                _previewTime = 0f;
            }
            _previewPlaying = true;
            _lastEditorTime = EditorApplication.timeSinceStartup;
        }

        if (!_previewPlaying)
        {
            return;
        }

        double now = EditorApplication.timeSinceStartup;
        _previewTime += Mathf.Max(0f, (float)(now - _lastEditorTime));
        _lastEditorTime = now;

        if (_previewTime > duration)
        {
            if (_previewLoop)
            {
                _previewTime %= Mathf.Max(0.01f, duration);
                RebuildVfx();
            }
            else
            {
                _previewTime = duration;
                _previewPlaying = false;
            }
        }

        RefreshPreview();
        Repaint();
    }

    private void DrawHitboxTimeline(float duration)
    {
        Rect rect = GUILayoutUtility.GetRect(1f, 18f, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(rect, new Color(0.16f, 0.16f, 0.16f));

        AbilityHitEvent[] hitEvents = Module.HitEvents;

        // 💡 원본 형태 보존: 최적화에 안전한 가벼운 연산문으로 감싸, hitEvents 배열이 비어있을 때만 레거시 주황색 바를 그립니다.
        if (hitEvents == null || hitEvents.Length == 0)
        {
            float start = duration > 0f ? Module.HitboxDelay / duration : 0f;
            float end = duration > 0f ? (Module.HitboxDelay + Module.HitboxLifetime) / duration : 0f;
            Rect hitRect = new Rect(
                rect.x + rect.width * Mathf.Clamp01(start),
                rect.y + 3f,
                rect.width * Mathf.Max(0.01f, Mathf.Clamp01(end) - Mathf.Clamp01(start)),
                rect.height - 6f);
            EditorGUI.DrawRect(hitRect, new Color(1f, 0.22f, 0.08f, 0.75f));
        }

        if (hitEvents != null)
        {
            for (int i = 0; i < hitEvents.Length; i++)
            {
                AbilityHitEvent hitEvent = hitEvents[i];
                if (hitEvent == null)
                {
                    continue;
                }

                float eventStart = Mathf.Clamp01(hitEvent.StartNormalizedTime);
                float eventEnd = Mathf.Clamp01(hitEvent.EndNormalizedTime);
                Rect eventRect = new Rect(
                    rect.x + rect.width * eventStart,
                    rect.y + 3f,
                    rect.width * Mathf.Max(0.01f, eventEnd - eventStart),
                    rect.height - 6f);
                Color color = GetHitEventPreviewColor(hitEvent, true);
                color.a = 0.85f;
                EditorGUI.DrawRect(eventRect, color);
            }
        }

        float cursor = duration > 0f ? _previewTime / duration : 0f;
        Rect cursorRect = new Rect(rect.x + rect.width * Mathf.Clamp01(cursor) - 1f, rect.y, 2f, rect.height);
        EditorGUI.DrawRect(cursorRect, Color.white);
    }

    private void DrawPreviewViewport(Rect rect)
    {
        EnsurePreview();
        if (_previewUtility == null || _previewPlayer == null)
        {
            EditorGUI.DrawRect(rect, new Color(0.12f, 0.12f, 0.12f));
            return;
        }

        RefreshPreview();
        Vector3 center = GetPreviewCameraCenter();
        float radius = _previewOrbitRadius;
        UpdatePreviewFloor(center);
        Quaternion orbit = Quaternion.Euler(_previewPitch, _previewYaw, 0f);
        float distance = Mathf.Clamp(_previewDistance, radius * 1.25f, radius * 7f);

        Camera camera = _previewUtility.camera;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = PreviewBackgroundColor;
        camera.fieldOfView = 35f;
        camera.nearClipPlane = 0.01f;
        camera.farClipPlane = 100f;
        camera.transform.position = center + orbit * new Vector3(0f, radius * 0.2f, -distance);
        camera.transform.LookAt(center);

        EditorGUI.DrawRect(rect, PreviewBackgroundColor);
        _previewUtility.BeginPreview(rect, GetPreviewBackgroundStyle());
        ClearPreviewRenderTarget(camera);
        camera.clearFlags = CameraClearFlags.Depth;
        _previewUtility.Render(true);
        Texture texture = _previewUtility.EndPreview();
        GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill, false);

        Rect labelRect = new Rect(rect.x + 8f, rect.yMax - 22f, rect.width - 16f, 18f);
        GUI.Label(labelRect, GetPreviewTimeLabel(), EditorStyles.whiteMiniLabel);
    }

    private void HandlePreviewInput(Rect rect)
    {
        Event current = Event.current;
        int controlId = GUIUtility.GetControlID("SkillModulePreviewOrbit".GetHashCode(), FocusType.Passive, rect);

        switch (current.GetTypeForControl(controlId))
        {
            case EventType.MouseDown:
                if (current.button == 0 && rect.Contains(current.mousePosition))
                {
                    GUIUtility.hotControl = controlId;
                    EditorGUIUtility.SetWantsMouseJumping(1);
                    current.Use();
                }
                break;

            case EventType.MouseDrag:
                if (GUIUtility.hotControl == controlId && current.button == 0)
                {
                    _previewYaw -= current.delta.x * 0.5f;
                    _previewPitch = Mathf.Clamp(_previewPitch + current.delta.y * 0.5f, -20f, 60f);
                    current.Use();
                    Repaint();
                }
                break;

            case EventType.MouseUp:
                if (GUIUtility.hotControl == controlId && current.button == 0)
                {
                    GUIUtility.hotControl = 0;
                    EditorGUIUtility.SetWantsMouseJumping(0);
                    current.Use();
                }
                break;

            case EventType.ScrollWheel:
                if (rect.Contains(current.mousePosition))
                {
                    float radius = _previewOrbitRadius;
                    _previewDistance = Mathf.Clamp(_previewDistance + current.delta.y * radius * 0.08f, radius * 1.25f, radius * 7f);
                    current.Use();
                    Repaint();
                }
                break;
        }
    }

    private void EnsurePreview()
    {
        if (_previewPlayerPrefab == null)
        {
            return;
        }

        if (_previewUtility != null && _previewPlayer != null)
        {
            return;
        }

        RebuildPreview();
    }

    private void RebuildPreview()
    {
        CleanupPreview();

        if (_previewPlayerPrefab == null)
        {
            return;
        }

        _previewUtility = new PreviewRenderUtility();
        _previewUtility.lights[0].intensity = 1.2f;
        _previewUtility.lights[0].transform.rotation = Quaternion.Euler(40f, 40f, 0f);
        _previewUtility.lights[1].intensity = 0.8f;

        RebuildPreviewFloor();

        _previewPlayer = (GameObject)PrefabUtility.InstantiatePrefab(_previewPlayerPrefab);
        if (_previewPlayer == null)
        {
            _previewPlayer = Instantiate(_previewPlayerPrefab);
        }

        _previewPlayer.hideFlags = HideFlags.HideAndDontSave;
        _previewPlayer.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        _previewAnimator = _previewPlayer.GetComponentInChildren<Animator>();
        PreparePreviewAnimator();
        _previewUtility.AddSingleGO(_previewPlayer);
        _playerPreviewBounds = CalculateBounds(_previewPlayer);
        ConfigurePreviewCenter(_playerPreviewBounds);
        _previewDistance = Mathf.Clamp(_previewOrbitRadius * 2.8f, 2f, 8f);
        RebuildVfx();
        RebuildHitboxVisual();
        RebuildHitEventVisuals();
        if (_previewAutoPlay)
        {
            _previewPlaying = true;
            _lastEditorTime = EditorApplication.timeSinceStartup;
        }

        RefreshPreview();
    }

    private void RebuildPreviewFloor()
    {
        DestroyPreviewObject(_previewFloor);
        DestroyPreviewObject(_floorMaterial);
        DestroyPreviewObject(_floorMesh);
        DestroyPreviewObject(_floorTexture);
        _previewFloor = null;
        _floorMaterial = null;
        _floorMesh = null;
        _floorTexture = null;

        if (_previewUtility == null)
        {
            return;
        }

        _previewFloor = new GameObject("Preview Floor Grid");
        _previewFloor.name = "Preview Floor";
        _previewFloor.hideFlags = HideFlags.HideAndDontSave;
        _floorMesh = CreateFloorGridMesh(12, 1f);
        _previewFloor.AddComponent<MeshFilter>().sharedMesh = _floorMesh;
        _previewFloor.AddComponent<MeshRenderer>();

        _floorMaterial = CreatePreviewMaterial(new Color(0.48f, 0.48f, 0.48f, 0.85f));
        _previewFloor.GetComponent<MeshRenderer>().sharedMaterial = _floorMaterial;
        _previewUtility.AddSingleGO(_previewFloor);
    }

    private static void ClearPreviewRenderTarget(Camera camera)
    {
        RenderTexture targetTexture = camera != null ? camera.targetTexture : null;
        if (targetTexture == null)
        {
            return;
        }

        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = targetTexture;
        GL.Clear(true, true, PreviewBackgroundColor);
        RenderTexture.active = previous;
    }

    private void UpdatePreviewFloor(Vector3 followCenter)
    {
        if (_previewFloor == null)
        {
            return;
        }

        _previewFloor.transform.position = new Vector3(followCenter.x, -0.01f, followCenter.z);
    }

    private void RebuildVfx()
    {
        DestroyPreviewObject(_previewVfx);
        _previewVfx = null;
        _particles = Array.Empty<ParticleSystem>();

        if (Module.EffectPrefab == null || _previewUtility == null || _previewPlayer == null)
        {
            return;
        }

        _previewVfx = (GameObject)PrefabUtility.InstantiatePrefab(Module.EffectPrefab);
        if (_previewVfx == null)
        {
            _previewVfx = Instantiate(Module.EffectPrefab);
        }

        _previewVfx.hideFlags = HideFlags.HideAndDontSave;
        _previewVfx.transform.SetPositionAndRotation(
            GetPreviewLocalPoint(Module.EffectLocalOffset),
            GetPreviewPlayerRotation());
        if (Module.ParentEffectToPlayer)
        {
            _previewVfx.transform.SetParent(_previewPlayer.transform, true);
        }
        else
        {
            _previewUtility.AddSingleGO(_previewVfx);
        }

        _particles = _previewVfx.GetComponentsInChildren<ParticleSystem>(true);
    }

    private void RebuildHitboxVisual()
    {
        DestroyPreviewObject(_hitboxVisual);
        _hitboxVisual = null;

        if (Module.HitboxPrefab == null || _previewUtility == null)
        {
            return;
        }

        _hitboxVisual = CreateHitboxVisual(Module.HitboxPrefab);
        if (_hitboxVisual != null)
        {
            _hitboxVisual.hideFlags = HideFlags.HideAndDontSave;
            _previewUtility.AddSingleGO(_hitboxVisual);
        }
    }

    private void PreparePreviewAnimator()
    {
        if (_previewPlayer == null)
        {
            return;
        }

        // 네트워크 제어 컴포넌트 간섭 방지용 차단
        foreach (NetworkPlayerController controller in _previewPlayer.GetComponentsInChildren<NetworkPlayerController>(true))
        {
            controller.enabled = false;
        }

        if (_previewAnimator == null)
        {
            return;
        }

        _previewAnimator.enabled = true;
        _previewAnimator.applyRootMotion = true;
        _previewAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
    }

    private bool EvaluateAnimatorPreviewState()
    {
        if (_previewAnimator == null || string.IsNullOrWhiteSpace(Module.AnimationStateName))
        {
            return false;
        }

        _previewPlayer.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        _previewAnimator.Rebind();
        _previewAnimator.Update(0f);
        _previewAnimator.Play(Module.AnimationStateName, 0, 0f);
        _previewAnimator.Update(0f);

        float targetTime = Mathf.Clamp(_previewTime, 0f, GetPreviewDuration());
        int steps = Mathf.Clamp(Mathf.CeilToInt(targetTime * 60f), 1, 180);
        float stepTime = targetTime / steps;
        for (int i = 0; i < steps; i++)
        {
            Vector3 before = _previewPlayer.transform.position;
            _previewAnimator.Update(stepTime);
            Vector3 appliedDelta = _previewPlayer.transform.position - before;
            Vector3 deltaPosition = Vector3.ProjectOnPlane(_previewAnimator.deltaPosition, Vector3.up);
            if (appliedDelta.sqrMagnitude <= 0.000001f && deltaPosition.sqrMagnitude > 0.000001f)
            {
                _previewPlayer.transform.position += deltaPosition;
            }
        }

        return true;
    }

    private void RefreshPreview()
    {
        if (_previewPlayer == null)
        {
            return;
        }

        if (!EvaluateAnimatorPreviewState() && Module.AnimationClip != null)
        {
            Module.AnimationClip.SampleAnimation(_previewPlayer, GetClipSampleTime());
        }

        UpdatePreviewGroundCenter();

        if (_previewVfx == null && Module.EffectPrefab != null)
        {
            RebuildVfx();
        }

        if (_previewVfx != null)
        {
            _previewVfx.transform.SetPositionAndRotation(
                GetPreviewLocalPoint(Module.EffectLocalOffset),
                GetPreviewPlayerRotation());
            foreach (ParticleSystem particle in _particles)
            {
                if (particle != null)
                {
                    particle.Simulate(Mathf.Max(0f, _previewTime), true, true, true);
                }
            }
        }

        if (_hitboxVisual == null && Module.HitboxPrefab != null)
        {
            RebuildHitboxVisual();
        }

        RefreshHitboxVisual();
        RefreshHitEventVisuals();
    }

    private void RefreshHitboxVisual()
    {
        if (_hitboxVisual == null || _hitboxMaterial == null)
        {
            return;
        }

        _hitboxVisual.transform.SetPositionAndRotation(
            GetPreviewLocalPoint(Module.HitboxLocalOffset),
            GetPreviewPlayerRotation());

        bool active = _previewTime >= Module.HitboxDelay &&
                      _previewTime <= Module.HitboxDelay + Module.HitboxLifetime;
        _hitboxMaterial.color = active
            ? new Color(1f, 0.12f, 0.04f, 0.35f)
            : new Color(1f, 0.85f, 0.05f, 0.22f);
    }

    private void RebuildHitEventVisuals()
    {
        ClearHitEventVisuals();

        if (_previewUtility == null || _previewPlayer == null)
        {
            return;
        }

        int count = Module.HitEvents != null ? Module.HitEvents.Length : 0;
        for (int i = 0; i < count; i++)
        {
            GameObject visual = new GameObject($"Hit Event Preview {i + 1}");
            Mesh mesh = CreateCylinderGuideMesh(48);
            visual.name = $"Hit Event Preview {i + 1}";
            visual.hideFlags = HideFlags.HideAndDontSave;
            visual.AddComponent<MeshFilter>().sharedMesh = mesh;

            // 💡 최적화: 빌드 타이밍에 Renderer를 미리 셋업하고 리스트에 추가합니다.
            Renderer renderer = visual.AddComponent<MeshRenderer>();

            Material material = CreatePreviewMaterial(new Color(1f, 0.28f, 0.02f, 0.85f));
            renderer.sharedMaterial = material;

            _previewUtility.AddSingleGO(visual);
            _hitEventVisuals.Add(visual);
            _hitEventMaterials.Add(material);
            _hitEventMeshes.Add(mesh);
            _hitEventRenderers.Add(renderer); // 추가: 렌더러 캐싱 리스트 채우기
        }
    }

    private void RefreshHitEventVisuals()
    {
        if (_previewPlayer == null)
        {
            return;
        }

        AbilityHitEvent[] hitEvents = Module.HitEvents;
        int eventCount = hitEvents != null ? hitEvents.Length : 0;

        // 💡 안전 예방 가드: 타이밍 꼬임으로 인한 무한 루프 빌드 크래시 방지 제어문 추가
        if (_hitEventVisuals.Count != eventCount)
        {
            RebuildHitEventVisuals();
            if (_hitEventVisuals.Count != eventCount) return;
        }

        float normalizedTime = GetPreviewNormalizedTime();
        for (int i = 0; i < _hitEventVisuals.Count; i++)
        {
            GameObject visual = _hitEventVisuals[i];
            AbilityHitEvent hitEvent = hitEvents[i];
            if (visual == null || hitEvent == null)
            {
                continue;
            }

            bool active = normalizedTime >= hitEvent.StartNormalizedTime &&
                          normalizedTime <= hitEvent.EndNormalizedTime;

            // 💡 초특급 최적화: 매 프레임 GetComponent<Renderer>() 조회를 파괴하고, 미리 캐싱한 _hitEventRenderers 데이터를 참조하여 연산 효율 극대화
            if (i < _hitEventRenderers.Count && _hitEventRenderers[i] != null)
            {
                Color color = GetHitEventPreviewColor(hitEvent, active);
                _hitEventMaterials[i].color = color;
            }

            // 💡 수정 전: GetPreviewGroundCenter() + Vector3.up * hitEvent.CenterHeight
            // 💡 수정 후: 실린더 메쉬의 절반 높이를 보정하여 'CenterHeight = 0'일 때 딱 발바닥에 붙게 만듭니다.
            visual.transform.SetPositionAndRotation(
                GetPreviewGroundCenter() + Vector3.up * (hitEvent.CenterHeight + hitEvent.Height * 0.5f),
                GetPreviewPlayerRotation());
            visual.transform.localScale = new Vector3(
                hitEvent.Radius * 2f,
                Mathf.Max(0.01f, hitEvent.Height) * 0.5f,
                hitEvent.Radius * 2f);
            visual.SetActive(hitEvent.Radius > 0f && hitEvent.Height > 0f);
        }
    }

    private static Color GetHitEventPreviewColor(AbilityHitEvent hitEvent, bool active)
    {
        Color color = hitEvent != null ? hitEvent.PreviewColor : new Color(1f, 0.28f, 0.02f, 1f);
        if (color.r > 0.9f && color.g > 0.9f && color.b > 0.9f)
        {
            color = new Color(1f, 0.28f, 0.02f, color.a);
        }

        color.a = active ? 0.9f : 0.22f;
        return color;
    }

    private static Mesh CreateCylinderGuideMesh(int segments)
    {
        segments = Mathf.Max(8, segments);
        Vector3[] vertices = new Vector3[segments * 2];
        int[] indices = new int[segments * 6];

        for (int i = 0; i < segments; i++)
        {
            float angle = (float)i / segments * Mathf.PI * 2f;
            float x = Mathf.Cos(angle) * 0.5f;
            float z = Mathf.Sin(angle) * 0.5f;
            vertices[i] = new Vector3(x, -1f, z);
            vertices[i + segments] = new Vector3(x, 1f, z);

            int next = (i + 1) % segments;
            int index = i * 6;
            indices[index] = i;
            indices[index + 1] = next;
            indices[index + 2] = i + segments;
            indices[index + 3] = next + segments;
            indices[index + 4] = i;
            indices[index + 5] = i + segments;
        }

        Mesh mesh = new Mesh
        {
            name = "Hit Event Cylinder Guide",
            hideFlags = HideFlags.HideAndDontSave
        };
        mesh.vertices = vertices;
        mesh.SetIndices(indices, MeshTopology.Lines, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    private static Mesh CreateFloorGridMesh(int halfLineCount, float spacing)
    {
        halfLineCount = Mathf.Max(1, halfLineCount);
        spacing = Mathf.Max(0.01f, spacing);

        int lineCount = halfLineCount * 2 + 1;
        Vector3[] vertices = new Vector3[lineCount * 4];
        int[] indices = new int[vertices.Length];
        float halfSize = halfLineCount * spacing;
        int vertexIndex = 0;

        for (int i = -halfLineCount; i <= halfLineCount; i++)
        {
            float offset = i * spacing;
            vertices[vertexIndex] = new Vector3(-halfSize, 0f, offset);
            vertices[vertexIndex + 1] = new Vector3(halfSize, 0f, offset);
            vertices[vertexIndex + 2] = new Vector3(offset, 0f, -halfSize);
            vertices[vertexIndex + 3] = new Vector3(offset, 0f, halfSize);

            indices[vertexIndex] = vertexIndex;
            indices[vertexIndex + 1] = vertexIndex + 1;
            indices[vertexIndex + 2] = vertexIndex + 2;
            indices[vertexIndex + 3] = vertexIndex + 3;
            vertexIndex += 4;
        }

        Mesh mesh = new Mesh
        {
            name = "Preview Floor Grid",
            hideFlags = HideFlags.HideAndDontSave
        };
        mesh.vertices = vertices;
        mesh.SetIndices(indices, MeshTopology.Lines, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    private GameObject CreateHitboxVisual(GameObject prefab)
    {
        Collider collider = prefab.GetComponentInChildren<Collider>();
        if (collider == null)
        {
            return null;
        }

        PrimitiveType primitiveType = PrimitiveType.Cube;
        Vector3 center = Vector3.zero;
        Vector3 scale = Vector3.one;

        if (collider is BoxCollider box)
        {
            center = box.center;
            scale = box.size;
        }
        else if (collider is SphereCollider sphere)
        {
            primitiveType = PrimitiveType.Sphere;
            center = sphere.center;
            scale = Vector3.one * sphere.radius * 2f;
        }
        else if (collider is CapsuleCollider capsule)
        {
            primitiveType = PrimitiveType.Capsule;
            center = capsule.center;
            scale = new Vector3(capsule.radius * 2f, capsule.height * 0.5f, capsule.radius * 2f);
        }

        GameObject visual = GameObject.CreatePrimitive(primitiveType);
        DestroyPreviewObject(visual.GetComponent<Collider>());
        visual.name = "Hitbox Preview";
        visual.transform.localPosition = center;
        visual.transform.localScale = scale;

        _hitboxMaterial = CreatePreviewMaterial(new Color(1f, 0.85f, 0.05f, 0.22f));
        visual.GetComponent<Renderer>().sharedMaterial = _hitboxMaterial;
        return visual;
    }

    private static Material CreatePreviewMaterial(Color color)
    {
        Material material = new Material(Shader.Find("Hidden/Internal-Colored"));
        material.hideFlags = HideFlags.HideAndDontSave;
        material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
        material.SetInt("_ZWrite", 0);
        material.color = color;
        return material;
    }

    private float GetPreviewDuration()
    {
        float duration = 1f;
        if (Module.AnimationClip != null)
        {
            duration = Mathf.Max(duration, GetAnimationPreviewDuration());
        }

        return Mathf.Max(duration, Module.HitboxDelay + Module.HitboxLifetime + 0.25f);
    }

    private float GetAnimationPreviewDuration()
    {
        if (Module.AnimationClip == null)
        {
            return 0f;
        }

        return Module.AnimationClip.length / Mathf.Max(0.01f, Module.AnimationSpeed);
    }

    private float GetClipSampleTime()
    {
        if (Module.AnimationClip == null)
        {
            return 0f;
        }

        float normalizedTime = GetAnimationPreviewDuration() > 0f
            ? _previewTime / GetAnimationPreviewDuration()
            : 0f;
        return Mathf.Clamp01(normalizedTime) * Module.AnimationClip.length;
    }

    private float GetPreviewNormalizedTime()
    {
        float animationDuration = GetAnimationPreviewDuration();
        return animationDuration > 0f ? Mathf.Clamp01(_previewTime / animationDuration) : 0f;
    }

    private string GetPreviewTimeLabel()
    {
        float duration = GetPreviewDuration();
        float normalizedTime = duration > 0f ? Mathf.Clamp01(_previewTime / duration) : 0f;
        int frame = Module.AnimationClip != null
            ? Mathf.RoundToInt(GetClipSampleTime() * Module.AnimationClip.frameRate)
            : 0;

        return $"{_previewTime:0.00}s / {duration:0.00}s ({normalizedTime:P1}) Frame {frame}";
    }

    private void ConfigurePreviewCenter(Bounds bounds)
    {
        Vector3 rootPosition = _previewPlayer != null ? _previewPlayer.transform.position : Vector3.zero;
        _previewGroundY = rootPosition.y;
        _previewGroundCenter = new Vector3(rootPosition.x, _previewGroundY, rootPosition.z);
        _previewCameraGroundCenter = _previewGroundCenter;
        _previewPivotHeight = GetPreviewCharacterHeight() * 0.5f;
        _previewOrbitRadius = Mathf.Max(1f, bounds.extents.magnitude);
    }

    private void UpdatePreviewGroundCenter()
    {
        if (_previewPlayer == null)
        {
            return;
        }

        Vector3 rootPosition = _previewPlayer.transform.position;
        _previewGroundCenter = new Vector3(rootPosition.x, _previewGroundY, rootPosition.z);
        _previewCameraGroundCenter = _previewGroundCenter;
    }

    private float GetPreviewCharacterHeight()
    {
        CharacterController characterController = _previewPlayer != null
            ? _previewPlayer.GetComponent<CharacterController>()
            : null;
        return characterController != null ? Mathf.Max(0.01f, characterController.height) : 1.8f;
    }

    private Vector3 GetPreviewGroundCenter()
    {
        if (_previewPlayer == null)
        {
            return Vector3.up * _previewGroundY;
        }

        return _previewGroundCenter;
    }

    private Vector3 GetPreviewPlayerCenter()
    {
        return GetPreviewGroundCenter() + Vector3.up * _previewPivotHeight;
    }

    private Vector3 GetPreviewCameraCenter()
    {
        return _previewCameraGroundCenter + Vector3.up * _previewPivotHeight;
    }

    private Vector3 GetPreviewLocalPoint(Vector3 localOffset)
    {
        return GetPreviewGroundCenter() + GetPreviewPlayerRotation() * localOffset;
    }

    private Quaternion GetPreviewPlayerRotation()
    {
        return _previewPlayer != null ? _previewPlayer.transform.rotation : Quaternion.identity;
    }

    private static Bounds CalculateBounds(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            return new Bounds(root.transform.position + Vector3.up, Vector3.one * 2f);
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        return bounds;
    }

    private void CleanupPreview()
    {
        if (_previewAnimator != null)
        {
            _previewAnimator.Rebind();
            _previewAnimator.enabled = false;
        }

        if (_previewPlayer != null)
        {
            if (_previewPlayer.TryGetComponent<Animation>(out var animation))
            {
                animation.Stop();
            }
        }

        DestroyPreviewObject(_previewPlayer);
        DestroyPreviewObject(_previewFloor);
        DestroyPreviewObject(_floorMaterial);
        DestroyPreviewObject(_floorMesh);
        DestroyPreviewObject(_floorTexture);
        DestroyPreviewObject(_previewBackgroundTexture);
        DestroyPreviewObject(_previewVfx);
        DestroyPreviewObject(_hitboxVisual);
        DestroyPreviewObject(_hitboxMaterial);
        ClearHitEventVisuals();
        _previewPlayer = null;
        _previewAnimator = null;
        _previewFloor = null;
        _floorMaterial = null;
        _floorMesh = null;
        _floorTexture = null;
        _previewBackgroundTexture = null;
        _previewBackgroundStyle = null;
        _previewVfx = null;
        _hitboxVisual = null;
        _hitboxMaterial = null;
        _particles = Array.Empty<ParticleSystem>();

        if (_previewUtility != null)
        {
            _previewUtility.Cleanup();
            _previewUtility = null;
        }
    }

    private void ClearHitEventVisuals()
    {
        for (int i = 0; i < _hitEventVisuals.Count; i++)
        {
            DestroyPreviewObject(_hitEventVisuals[i]);
        }
        _hitEventVisuals.Clear();

        for (int i = 0; i < _hitEventMaterials.Count; i++)
        {
            DestroyPreviewObject(_hitEventMaterials[i]);
        }
        _hitEventMaterials.Clear();

        for (int i = 0; i < _hitEventMeshes.Count; i++)
        {
            DestroyPreviewObject(_hitEventMeshes[i]);
        }
        _hitEventMeshes.Clear();

        _hitEventRenderers.Clear(); // 추가: 캐싱 리스트 클리어
    }

    private static Texture2D CreateGridTexture(int size, int gridStep)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, true)
        {
            hideFlags = HideFlags.HideAndDontSave,
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Bilinear
        };

        Color baseColor = new Color(0.22f, 0.22f, 0.22f, 1f);
        Color lineColor = new Color(0.38f, 0.38f, 0.38f, 1f);
        Color centerLineColor = new Color(0.48f, 0.48f, 0.48f, 1f);
        int center = size / 2;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool centerLine = x == center || y == center;
                bool gridLine = x % gridStep == 0 || y % gridStep == 0;
                texture.SetPixel(x, y, centerLine ? centerLineColor : gridLine ? lineColor : baseColor);
            }
        }

        texture.Apply(false, true);
        return texture;
    }

    private GUIStyle GetPreviewBackgroundStyle()
    {
        if (_previewBackgroundTexture == null)
        {
            _previewBackgroundTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            _previewBackgroundTexture.SetPixel(0, 0, PreviewBackgroundColor);
            _previewBackgroundTexture.Apply(false, true);
        }

        if (_previewBackgroundStyle == null)
        {
            _previewBackgroundStyle = new GUIStyle
            {
                normal =
                {
                    background = _previewBackgroundTexture
                }
            };
        }

        return _previewBackgroundStyle;
    }

    private static void DestroyPreviewObject(UnityEngine.Object previewObject)
    {
        if (previewObject != null)
        {
            DestroyImmediate(previewObject);
        }
    }

    private static void DrawSection(string title, ref bool isOpen, Action drawContent)
    {
        EditorGUILayout.Space(4f);
        isOpen = EditorGUILayout.BeginFoldoutHeaderGroup(isOpen, title);
        if (isOpen)
        {
            EditorGUI.indentLevel++;
            drawContent();
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndFoldoutHeaderGroup();
    }
}
