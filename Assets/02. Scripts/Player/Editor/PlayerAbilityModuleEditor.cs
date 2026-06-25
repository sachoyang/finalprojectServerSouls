using System;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PlayerAbilityModule))]
public class PlayerAbilityModuleEditor : Editor
{
    private const string PreviewPlayerPrefsKey = "ServerSouls.SkillPreview.PlayerPrefab";

    private static bool _rewardOpen = true;
    private static bool _activeSettingsOpen = true;
    private static bool _effectOpen = true;
    private static bool _passiveStatsOpen = true;
    private static bool _presentationOpen = true;
    private static bool _vfxOpen = true;
    private static bool _hitboxOpen = true;
    private static bool _previewOpen = true;
    private static bool _soundOpen = true;

    private SerializedProperty _abilityId;
    private SerializedProperty _displayName;
    private SerializedProperty _description;
    private SerializedProperty _icon;
    private SerializedProperty _abilityType;
    private SerializedProperty _minBossStage;
    private SerializedProperty _maxBossStage;
    private SerializedProperty _includeInRewardPool;
    private SerializedProperty _staminaCost;
    private SerializedProperty _cooldownSeconds;
    private SerializedProperty _healthRestoreAmount;
    private SerializedProperty _staminaRestoreAmount;
    private SerializedProperty _specialEffect;
    private SerializedProperty _maxHealthBonus;
    private SerializedProperty _maxStaminaBonus;
    private SerializedProperty _defenseRateBonus;
    private SerializedProperty _attackDamageBonusPercent;
    private SerializedProperty _animationClip;
    private SerializedProperty _animationStateName;
    private SerializedProperty _animationTrigger;
    private SerializedProperty _effectPrefab;
    private SerializedProperty _effectLocalOffset;
    private SerializedProperty _parentEffectToPlayer;
    private SerializedProperty _hitboxPrefab;
    private SerializedProperty _hitboxLocalOffset;
    private SerializedProperty _hitboxDamage;
    private SerializedProperty _hitboxRevivePower;
    private SerializedProperty _hitboxDelay;
    private SerializedProperty _hitboxLifetime;
    private SerializedProperty _bitIndex;
    private SerializedProperty _soundClip;
    private SerializedProperty _soundVolume;
    private SerializedProperty _soundDelay;





    private PreviewRenderUtility _previewUtility;
    private GameObject _previewPlayerPrefab;
    private GameObject _previewPlayer;
    private GameObject _previewVfx;
    private GameObject _hitboxVisual;
    private Material _hitboxMaterial;
    private ParticleSystem[] _particles = Array.Empty<ParticleSystem>();
    private Bounds _playerPreviewBounds = new Bounds(Vector3.up, Vector3.one * 2f);
    private bool _previewPlaying;
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
        _includeInRewardPool = serializedObject.FindProperty("includeInRewardPool");
        _staminaCost = serializedObject.FindProperty("staminaCost");
        _cooldownSeconds = serializedObject.FindProperty("cooldownSeconds");
        _healthRestoreAmount = serializedObject.FindProperty("healthRestoreAmount");
        _staminaRestoreAmount = serializedObject.FindProperty("staminaRestoreAmount");
        _specialEffect = serializedObject.FindProperty("specialEffect");
        _maxHealthBonus = serializedObject.FindProperty("maxHealthBonus");
        _maxStaminaBonus = serializedObject.FindProperty("maxStaminaBonus");
        _defenseRateBonus = serializedObject.FindProperty("defenseRateBonus");
        _attackDamageBonusPercent = serializedObject.FindProperty("attackDamageBonusPercent");
        _animationClip = serializedObject.FindProperty("animationClip");
        _animationStateName = serializedObject.FindProperty("animationStateName");
        _animationTrigger = serializedObject.FindProperty("animationTrigger");
        _effectPrefab = serializedObject.FindProperty("effectPrefab");
        _effectLocalOffset = serializedObject.FindProperty("effectLocalOffset");
        _parentEffectToPlayer = serializedObject.FindProperty("parentEffectToPlayer");
        _hitboxPrefab = serializedObject.FindProperty("hitboxPrefab");
        _hitboxLocalOffset = serializedObject.FindProperty("hitboxLocalOffset");
        _hitboxDamage = serializedObject.FindProperty("hitboxDamage");
        _hitboxRevivePower = serializedObject.FindProperty("hitboxRevivePower");
        _hitboxDelay = serializedObject.FindProperty("hitboxDelay");
        _hitboxLifetime = serializedObject.FindProperty("hitboxLifetime");
        _bitIndex = serializedObject.FindProperty("bitIndex");
        _soundClip = serializedObject.FindProperty("soundClip");
        _soundVolume = serializedObject.FindProperty("soundVolume");
        _soundDelay = serializedObject.FindProperty("soundDelay");

        string prefabGuid = EditorPrefs.GetString(PreviewPlayerPrefsKey, string.Empty);
        string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuid);
        _previewPlayerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        EditorApplication.update += UpdatePreviewPlayback;
    }

    private void OnDisable()
    {
        EditorApplication.update -= UpdatePreviewPlayback;
        CleanupPreview();
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(_abilityType);
        bool isActive = _abilityType.enumValueIndex == (int)AbilityType.Active;

        DrawSection("Reward", ref _rewardOpen, DrawReward);

        if (isActive)
        {
            DrawSection("Active Settings", ref _activeSettingsOpen, DrawActiveSettings);
        }

        DrawSection("Effect", ref _effectOpen, DrawEffect);

        if (!isActive)
        {
            DrawSection("Passive Stat Bonuses", ref _passiveStatsOpen, DrawPassiveStats);
        }

        DrawSection(isActive ? "Presentation" : "Acquisition Presentation", ref _presentationOpen, DrawPresentation);
        DrawSection(isActive ? "VFX" : "Acquisition VFX", ref _vfxOpen, DrawVfx);
        DrawSection("Sound", ref _soundOpen, DrawSound);

        if (isActive)
        {
            DrawSection("Hitbox", ref _hitboxOpen, DrawHitbox);
        }

        serializedObject.ApplyModifiedProperties();

        DrawSection("Preview", ref _previewOpen, DrawEmbeddedPreview);
    }

    private void DrawReward()
    {
        EditorGUILayout.PropertyField(_bitIndex);
        EditorGUILayout.PropertyField(_abilityId);
        EditorGUILayout.PropertyField(_displayName);
        EditorGUILayout.PropertyField(_description);
        EditorGUILayout.PropertyField(_icon);
        EditorGUILayout.PropertyField(_minBossStage);
        EditorGUILayout.PropertyField(_maxBossStage);
        EditorGUILayout.PropertyField(_includeInRewardPool);
    }

    private void DrawActiveSettings()
    {
        EditorGUILayout.PropertyField(_staminaCost);
        EditorGUILayout.PropertyField(_cooldownSeconds);
    }

    private void DrawEffect()
    {
        EditorGUILayout.PropertyField(_healthRestoreAmount);
        EditorGUILayout.PropertyField(_staminaRestoreAmount);
        EditorGUILayout.PropertyField(_specialEffect);
    }

    private void DrawPassiveStats()
    {
        EditorGUILayout.PropertyField(_maxHealthBonus);
        EditorGUILayout.PropertyField(_maxStaminaBonus);
        EditorGUILayout.PropertyField(_defenseRateBonus);
        EditorGUILayout.PropertyField(_attackDamageBonusPercent);
    }

    private void DrawPresentation()
    {
        EditorGUILayout.PropertyField(_animationClip);
        EditorGUILayout.PropertyField(_animationStateName);
        EditorGUILayout.PropertyField(_animationTrigger);
    }

    private void DrawVfx()
    {
        EditorGUILayout.PropertyField(_effectPrefab);
        EditorGUILayout.PropertyField(_effectLocalOffset);
        EditorGUILayout.PropertyField(_parentEffectToPlayer);
    }

    private void DrawHitbox()
    {
        EditorGUILayout.PropertyField(_hitboxPrefab);
        EditorGUILayout.PropertyField(_hitboxLocalOffset);
        EditorGUILayout.PropertyField(_hitboxDamage);
        EditorGUILayout.PropertyField(_hitboxRevivePower);
        EditorGUILayout.PropertyField(_hitboxDelay);
        EditorGUILayout.PropertyField(_hitboxLifetime);
    }

    private void DrawSound()
    {
        EditorGUILayout.PropertyField(_soundClip);
        EditorGUILayout.PropertyField(_soundVolume);
        EditorGUILayout.PropertyField(_soundDelay);
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
        if (!_previewPlaying)
        {
            return;
        }

        double now = EditorApplication.timeSinceStartup;
        _previewTime += Mathf.Max(0f, (float)(now - _lastEditorTime));
        _lastEditorTime = now;

        float duration = GetPreviewDuration();
        if (_previewTime > duration)
        {
            _previewTime %= Mathf.Max(0.01f, duration);
            RebuildVfx();
        }

        RefreshPreview();
        Repaint();
    }

    private void DrawHitboxTimeline(float duration)
    {
        Rect rect = GUILayoutUtility.GetRect(1f, 18f, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(rect, new Color(0.16f, 0.16f, 0.16f));

        float start = duration > 0f ? Module.HitboxDelay / duration : 0f;
        float end = duration > 0f ? (Module.HitboxDelay + Module.HitboxLifetime) / duration : 0f;
        Rect hitRect = new Rect(
            rect.x + rect.width * Mathf.Clamp01(start),
            rect.y + 3f,
            rect.width * Mathf.Max(0.01f, Mathf.Clamp01(end) - Mathf.Clamp01(start)),
            rect.height - 6f);
        EditorGUI.DrawRect(hitRect, new Color(1f, 0.22f, 0.08f, 0.75f));

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
        Vector3 center = _playerPreviewBounds.center;
        float radius = Mathf.Max(1f, _playerPreviewBounds.extents.magnitude);
        Quaternion orbit = Quaternion.Euler(_previewPitch, _previewYaw, 0f);
        float distance = Mathf.Clamp(_previewDistance, radius * 1.25f, radius * 7f);

        Camera camera = _previewUtility.camera;
        camera.clearFlags = CameraClearFlags.Color;
        camera.backgroundColor = new Color(0.13f, 0.13f, 0.13f);
        camera.fieldOfView = 35f;
        camera.nearClipPlane = 0.01f;
        camera.farClipPlane = 100f;
        camera.transform.position = center + orbit * new Vector3(0f, radius * 0.2f, -distance);
        camera.transform.LookAt(center);

        _previewUtility.BeginPreview(rect, GUIStyle.none);
        _previewUtility.Render(true);
        Texture texture = _previewUtility.EndPreview();
        GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill, false);

        Rect labelRect = new Rect(rect.x + 8f, rect.yMax - 22f, rect.width - 16f, 18f);
        GUI.Label(labelRect, $"{_previewTime:0.00}s / {GetPreviewDuration():0.00}s", EditorStyles.whiteMiniLabel);
    }

    private void HandlePreviewInput(Rect rect)
    {
        Event current = Event.current;
        if (!rect.Contains(current.mousePosition))
        {
            return;
        }

        if (current.type == EventType.MouseDrag && current.button == 0)
        {
            _previewYaw += current.delta.x * 0.5f;
            _previewPitch = Mathf.Clamp(_previewPitch - current.delta.y * 0.5f, -20f, 60f);
            current.Use();
            Repaint();
        }

        if (current.type == EventType.ScrollWheel)
        {
            float radius = Mathf.Max(1f, _playerPreviewBounds.extents.magnitude);
            _previewDistance = Mathf.Clamp(_previewDistance + current.delta.y * radius * 0.08f, radius * 1.25f, radius * 7f);
            current.Use();
            Repaint();
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

        _previewPlayer = (GameObject)PrefabUtility.InstantiatePrefab(_previewPlayerPrefab);
        if (_previewPlayer == null)
        {
            _previewPlayer = Instantiate(_previewPlayerPrefab);
        }

        _previewPlayer.hideFlags = HideFlags.HideAndDontSave;
        _previewPlayer.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        _previewUtility.AddSingleGO(_previewPlayer);
        _playerPreviewBounds = CalculateBounds(_previewPlayer);
        _previewDistance = Mathf.Clamp(_playerPreviewBounds.extents.magnitude * 2.8f, 2f, 8f);
        RebuildVfx();
        RebuildHitboxVisual();
        RefreshPreview();
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
            _previewPlayer.transform.TransformPoint(Module.EffectLocalOffset),
            _previewPlayer.transform.rotation);
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

    private void RefreshPreview()
    {
        if (_previewPlayer == null)
        {
            return;
        }

        if (Module.AnimationClip != null)
        {
            Module.AnimationClip.SampleAnimation(_previewPlayer, Mathf.Clamp(_previewTime, 0f, Module.AnimationClip.length));
        }

        if (_previewVfx == null && Module.EffectPrefab != null)
        {
            RebuildVfx();
        }

        if (_previewVfx != null)
        {
            _previewVfx.transform.SetPositionAndRotation(
                _previewPlayer.transform.TransformPoint(Module.EffectLocalOffset),
                _previewPlayer.transform.rotation);
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
    }

    private void RefreshHitboxVisual()
    {
        if (_hitboxVisual == null || _hitboxMaterial == null)
        {
            return;
        }

        _hitboxVisual.transform.SetPositionAndRotation(
            _previewPlayer.transform.TransformPoint(Module.HitboxLocalOffset),
            _previewPlayer.transform.rotation);

        bool active = _previewTime >= Module.HitboxDelay &&
                      _previewTime <= Module.HitboxDelay + Module.HitboxLifetime;
        _hitboxMaterial.color = active
            ? new Color(1f, 0.12f, 0.04f, 0.35f)
            : new Color(1f, 0.85f, 0.05f, 0.22f);
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

        _hitboxMaterial = new Material(Shader.Find("Hidden/Internal-Colored"));
        _hitboxMaterial.hideFlags = HideFlags.HideAndDontSave;
        _hitboxMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        _hitboxMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        _hitboxMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
        _hitboxMaterial.SetInt("_ZWrite", 0);
        visual.GetComponent<Renderer>().sharedMaterial = _hitboxMaterial;
        return visual;
    }

    private float GetPreviewDuration()
    {
        float duration = 1f;
        if (Module.AnimationClip != null)
        {
            duration = Mathf.Max(duration, Module.AnimationClip.length);
        }

        return Mathf.Max(duration, Module.HitboxDelay + Module.HitboxLifetime + 0.25f);
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
        // 추가: 플레이어 오브젝트가 삭제되기 전에 애니메이션/플레이어블 그래프 상태를 안전하게 해제합니다.
        if (_previewPlayer != null)
        {
            // 1. Animator 컴포넌트가 있다면 비활성화하여 내부 그래프 정리 유도
            if (_previewPlayer.TryGetComponent<Animator>(out var animator))
            {
                animator.Rebind(); // 상태 초기화
                animator.enabled = false;
            }

            // 2. 만약 기존 legacy Animation 컴포넌트를 쓰고 있다면 정지
            if (_previewPlayer.TryGetComponent<Animation>(out var animation))
            {
                animation.Stop();
            }
        }

        // 기존 파괴 로직 진행
        DestroyPreviewObject(_previewPlayer);
        DestroyPreviewObject(_previewVfx);
        DestroyPreviewObject(_hitboxVisual);
        DestroyPreviewObject(_hitboxMaterial);
        _previewPlayer = null;
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
