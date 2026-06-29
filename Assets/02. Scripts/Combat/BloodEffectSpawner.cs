using System.Collections.Generic;
using BFX;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Serialization;

[System.AttributeUsage(System.AttributeTargets.Field)]
public sealed class BloodEffectIdAttribute : PropertyAttribute
{
}

[System.AttributeUsage(System.AttributeTargets.Field)]
public sealed class BloodEffectPrefabAttribute : PropertyAttribute
{
}

/// <summary>
/// 타격 종류별 KriptoFX 혈액 효과와 3D 타격음을 함께 생성한다.
/// </summary>
public class BloodEffectSpawner : MonoBehaviour
{
    public const string BasicAttack1Id = "BasicAttack1";
    public const string BasicAttack2Id = "BasicAttack2";
    public const string BasicAttack3Id = "BasicAttack3";

    [System.Serializable]
    public class BloodEffectData
    {
        [InspectorName("효과 ID")]
        [Tooltip("기본 공격 타수 또는 프로젝트에 등록된 Active 스킬의 AbilityId를 선택합니다.")]
        [BloodEffectId]
        public string effectId;
        [InspectorName("피 이펙트 프리팹")]
        [Tooltip("KriptoFX의 URP 패치가 적용된 Prefabs 폴더의 프리팹을 사용합니다.")]
        [BloodEffectPrefab]
        public GameObject prefab;
        [InspectorName("타격 사운드")]
        [Tooltip("피 이펙트가 생성될 때 같은 위치에서 재생할 3D 사운드입니다.")]
        public AudioClip hitSound;
        [InspectorName("사운드 볼륨")]
        [Tooltip("SoundManager의 CombatHit 카테고리 볼륨에 추가로 곱할 값입니다.")]
        [Range(0f, 2f)] public float soundVolume = 1f;
        [InspectorName("기본 크기")]
        [Tooltip("프리팹 원본 크기에 곱할 배율입니다.")]
        [Min(0.01f)] public float scale = 1f;
        [InspectorName("무작위 크기 범위")]
        [Tooltip("생성할 때 기본 크기에 곱할 최소·최대 무작위 배율입니다.")]
        public Vector2 randomScaleRange = new Vector2(0.9f, 1.1f);
        [InspectorName("색상 변경")]
        [Tooltip("체크한 경우에만 아래 색상으로 원본 KriptoFX 색상을 덮어씁니다.")]
        public bool overrideColor;
        [InspectorName("피 색상")]
        [Tooltip("색상 변경을 체크했을 때 피 분출 메시와 URP 데칼에 적용할 색상입니다.")]
        public Color color = new Color(0.5f, 0f, 0f, 1f);
        [InspectorName("유지 시간")]
        [Tooltip("생성된 피 이펙트가 삭제되기까지의 시간입니다.")]
        [Min(0.1f)] public float lifetime = 5f;
    }

    [Header("피격 종류별 이펙트 설정")]
    [InspectorName("피 이펙트 목록")]
    [Tooltip("기본 공격 1~3타와 각 스킬 AbilityId에 대응하는 피 이펙트를 등록합니다.")]
    [SerializeField] private List<BloodEffectData> bloodEffects = new List<BloodEffectData>();

    [Header("전체 공통 보정")]
    [InspectorName("생성 위치 보정")]
    [Tooltip("계산된 피격 위치에서 추가로 이동시킬 월드 좌표 오프셋입니다.")]
    [SerializeField] private Vector3 spawnOffset = Vector3.zero;
    [InspectorName("전체 크기 배율")]
    [Tooltip("목록에 등록된 모든 피 이펙트 크기에 공통으로 곱합니다.")]
    [SerializeField, Min(0.01f)] private float globalScaleMultiplier = 1f;
    [InspectorName("전체 애니메이션 속도")]
    [Tooltip("KriptoFX 피 애니메이션 속도에 공통으로 곱합니다.")]
    [SerializeField, Min(0.01f)] private float animationSpeedMultiplier = 1f;

    [Header("공통 부착 혈흔")]
    [InspectorName("부착 혈흔 프리팹")]
    [Tooltip("모든 공격에서 공통으로 사용할 AttachedBloodDecal 프리팹입니다. 비워두면 부착 혈흔을 생성하지 않습니다.")]
    [SerializeField] private GameObject attachedBloodDecalPrefab;

    [Header("기존 씬 호환 및 미등록 ID 대체값")]
    [FormerlySerializedAs("bloodEffectPrefab")]
    [InspectorName("기본 피 이펙트 프리팹")]
    [Tooltip("효과 ID가 목록에 없을 때 사용할 기본 피 이펙트입니다.")]
    [SerializeField] private GameObject fallbackBloodEffectPrefab;
    [FormerlySerializedAs("destroyDelay")]
    [InspectorName("기본 유지 시간")]
    [Tooltip("미등록 ID에 기본 피 이펙트를 사용할 때의 유지 시간입니다.")]
    [SerializeField, Min(0.1f)] private float fallbackLifetime = 5f;
    private static readonly int ColorProperty = Shader.PropertyToID("_Color");
    private static readonly int TintColorProperty = Shader.PropertyToID("_TintColor");

    private readonly Dictionary<string, BloodEffectData> _effectIdCache =
        new Dictionary<string, BloodEffectData>();
    private readonly List<Transform> _boneCache = new List<Transform>(128);
    private MaterialPropertyBlock _propertyBlock;

    private void Awake()
    {
        RebuildCache();
    }

    private void OnValidate()
    {
        if (Application.isPlaying)
        {
            RebuildCache();
        }
    }

    public void SpawnBlood(
        string effectId,
        Vector3 position,
        Vector3 surfaceNormal)
    {
        BloodEffectData data = GetEffectData(effectId);
        GameObject prefab = data != null ? data.prefab : fallbackBloodEffectPrefab;

        PlayHitSound(data, position);

        if (prefab == null)
        {
            return;
        }

        Vector3 normal = surfaceNormal.sqrMagnitude > 0.001f
            ? surfaceNormal.normalized
            : transform.forward;
        float scale = GetScale(data);
        float lifetime = data != null ? data.lifetime : fallbackLifetime;

        GameObject instance = SpawnSplash(prefab, position, normal, scale);

        if (data != null && data.overrideColor)
        {
            ApplyBloodColor(instance, data.color);
        }

        ApplyKriptoSettings(instance, lifetime);
        Destroy(instance, Mathf.Max(0.1f, lifetime));
    }

    /// <summary>
    /// 데모의 BloodAttach 방식처럼 공통 혈흔 하나를 피격점과 가장 가까운 본에 붙인다.
    /// 색상, 크기, 유지 시간은 해당 공격 ID의 설정을 함께 사용한다.
    /// </summary>
    public void SpawnAttachedBloodDecal(
        string effectId,
        Vector3 position,
        Vector3 surfaceNormal,
        Transform attachRoot)
    {
        if (attachedBloodDecalPrefab == null || attachRoot == null)
        {
            return;
        }

        BloodEffectData data = GetEffectData(effectId);
        Vector3 normal = surfaceNormal.sqrMagnitude > 0.001f
            ? surfaceNormal.normalized
            : transform.forward;
        float scale = GetScale(data);
        float lifetime = data != null ? data.lifetime : fallbackLifetime;

        GameObject instance = SpawnAttachedDecal(
            attachedBloodDecalPrefab,
            position,
            normal,
            attachRoot,
            scale);

        if (data != null && data.overrideColor)
        {
            ApplyBloodColor(instance, data.color);
        }

        ApplyKriptoSettings(instance, lifetime);
        Destroy(instance, Mathf.Max(0.1f, lifetime));
    }

    private GameObject SpawnSplash(
        GameObject prefab,
        Vector3 position,
        Vector3 surfaceNormal,
        float scale)
    {
        // 피가 옆으로 눕지 않도록 KriptoFX 데모처럼 표면 법선의 수평 방향만 사용한다.
        float angle = Mathf.Atan2(surfaceNormal.x, surfaceNormal.z) * Mathf.Rad2Deg + 180f;
        Quaternion rotation = Quaternion.Euler(0f, angle + 90f, 0f);
        GameObject instance = Instantiate(prefab, position + spawnOffset, rotation);
        instance.transform.localScale = prefab.transform.localScale * scale;
        return instance;
    }

    private GameObject SpawnAttachedDecal(
        GameObject prefab,
        Vector3 position,
        Vector3 surfaceNormal,
        Transform attachRoot,
        float scale)
    {
        // 움직이는 보스를 따라가도록 피격점과 가장 가까운 본을 찾는다.
        Transform nearestBone = GetNearestObjectCached(attachRoot, position);
        if (nearestBone == null)
        {
            nearestBone = attachRoot;
        }

        GameObject instance = Instantiate(prefab);
        Transform instanceTransform = instance.transform;
        instanceTransform.position = position + spawnOffset;
        instanceTransform.localRotation = Quaternion.identity;
        instanceTransform.localScale = prefab.transform.localScale * scale;

        // 데칼 앞면이 피격 표면 바깥쪽을 향하도록 법선에 맞춰 회전한다.
        Vector3 up = Mathf.Abs(Vector3.Dot(surfaceNormal, Vector3.up)) > 0.98f
            ? Vector3.forward
            : Vector3.up;
        instanceTransform.LookAt(instanceTransform.position + surfaceNormal, up);
        instanceTransform.Rotate(90f, 0f, 0f);
        instanceTransform.SetParent(nearestBone, true);
        return instance;
    }

    private void PlayHitSound(BloodEffectData data, Vector3 position)
    {
        if (data == null || data.hitSound == null || SoundManager.Instance == null)
        {
            return;
        }

        SoundManager.Instance.PlaySFX_3D(
            data.hitSound,
            position + spawnOffset,
            SoundCategory.CombatHit,
            data.soundVolume);
    }

    private void ApplyKriptoSettings(GameObject instance, float lifetime)
    {
        BFX_BloodSettings settings =
            instance.GetComponent<BFX_BloodSettings>() ??
            instance.GetComponentInChildren<BFX_BloodSettings>(true);

        if (settings == null)
        {
            return;
        }

        settings.DecalLifeTimeSeconds = Mathf.Max(5f, lifetime);
        settings.AnimationSpeed *= animationSpeedMultiplier;
    }

    private void ApplyBloodColor(GameObject effectRoot, Color color)
    {
        // 일반 피 메시에는 머티리얼을 복제하지 않고 색상을 적용한다.
        _propertyBlock ??= new MaterialPropertyBlock();

        Renderer[] renderers = effectRoot.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            renderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetColor(ColorProperty, color);
            _propertyBlock.SetColor(TintColorProperty, color);
            renderer.SetPropertyBlock(_propertyBlock);
            _propertyBlock.Clear();
        }

        // URP 데칼은 일반 Renderer가 아니므로 따로 색상을 적용한다.
        DecalProjector[] projectors = effectRoot.GetComponentsInChildren<DecalProjector>(true);
        for (int i = 0; i < projectors.Length; i++)
        {
            Material material = projectors[i].material;
            if (material == null)
            {
                continue;
            }

            if (material.HasProperty(ColorProperty))
            {
                material.SetColor(ColorProperty, color);
            }

            if (material.HasProperty(TintColorProperty))
            {
                material.SetColor(TintColorProperty, color);
            }
        }
    }

    private float GetScale(BloodEffectData data)
    {
        if (data == null)
        {
            return globalScaleMultiplier;
        }

        float min = Mathf.Min(data.randomScaleRange.x, data.randomScaleRange.y);
        float max = Mathf.Max(data.randomScaleRange.x, data.randomScaleRange.y);
        return Mathf.Max(0.01f, data.scale * Random.Range(min, max) * globalScaleMultiplier);
    }

    private BloodEffectData GetEffectData(string effectId)
    {
        if (!string.IsNullOrWhiteSpace(effectId) &&
            _effectIdCache.TryGetValue(effectId, out BloodEffectData idData))
        {
            return idData;
        }

        return null;
    }

    private void RebuildCache()
    {
        // 전투 중 문자열 목록을 반복 검색하지 않도록 ID별 사전을 미리 만든다.
        _effectIdCache.Clear();
        for (int i = 0; i < bloodEffects.Count; i++)
        {
            BloodEffectData data = bloodEffects[i];
            if (data == null)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(data.effectId) &&
                !_effectIdCache.ContainsKey(data.effectId))
            {
                _effectIdCache.Add(data.effectId, data);
            }

        }
    }

    private Transform GetNearestObjectCached(Transform root, Vector3 hitPosition)
    {
        _boneCache.Clear();
        root.GetComponentsInChildren(true, _boneCache);

        float closestDistanceSqr = float.MaxValue;
        Transform closest = root;
        for (int i = 0; i < _boneCache.Count; i++)
        {
            Transform child = _boneCache[i];
            if (child == null)
            {
                continue;
            }

            float distanceSqr = (child.position - hitPosition).sqrMagnitude;
            if (distanceSqr < closestDistanceSqr)
            {
                closestDistanceSqr = distanceSqr;
                closest = child;
            }
        }

        _boneCache.Clear();
        return closest;
    }
}
