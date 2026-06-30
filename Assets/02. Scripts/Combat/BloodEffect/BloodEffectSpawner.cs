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
        [Tooltip("SoundManager의 CombatHit 카테고리 볼륨에 추가로 곱할 값입니다. 기본값은 1입니다.")]
        [Range(0.01f, 2f)] public float soundVolume = 1f;
        [InspectorName("기본 크기")]
        [Tooltip("프리팹 원본 크기에 곱할 배율입니다.")]
        [Min(0.01f)] public float scale = 1f;
        [InspectorName("무작위 크기 최소")]
        [Tooltip("생성할 때 기본 크기에 곱할 최소 무작위 배율입니다.")]
        [Min(0.01f)] public float randomScaleMin = 0.9f;
        [InspectorName("무작위 크기 최대")]
        [Tooltip("생성할 때 기본 크기에 곱할 최대 무작위 배율입니다.")]
        [Min(0.01f)] public float randomScaleMax = 1.1f;
        [FormerlySerializedAs("randomScaleRange")]
        [HideInInspector] public Vector2 legacyRandomScaleRange;
        [InspectorName("색상 변경")]
        [Tooltip("체크한 경우에만 아래 색상으로 원본 KriptoFX 색상을 덮어씁니다.")]
        public bool overrideColor;
        [InspectorName("피 색상")]
        [Tooltip("색상 변경을 체크했을 때 피 분출 메시와 URP 데칼에 적용할 색상입니다.")]
        public Color color = new Color(0.5f, 0f, 0f, 1f);
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

    [Header("피 분출 및 바닥 혈흔 제한")]
    [InspectorName("피 이펙트 최대 개수")]
    [Tooltip("피 분출과 그 안의 바닥 혈흔을 동시에 유지할 최대 개수입니다. 초과하면 가장 오래된 이펙트를 풀로 반환합니다.")]
    [SerializeField, Min(1)] private int maxActiveBloodEffects = 30;
    [InspectorName("피 이펙트 최대 수명")]
    [Tooltip("BFX 자연 종료가 되지 않아도 풀로 반환할 최대 시간(초)입니다.")]
    [SerializeField, Min(0.1f)] private float bloodEffectMaxLifetime = 30f;

    [InspectorName("플레이어 피격 연출 최소 간격")]
    [Tooltip("여러 보스 Hitbox가 거의 동시에 맞아도 피와 부착 혈흔은 이 간격마다 한 번만 생성합니다. 데미지 판정에는 영향이 없습니다.")]
    [SerializeField, Min(0f)] private float playerBloodSpawnInterval = 0.12f;

    [Header("기존 씬 호환 및 미등록 ID 대체값")]
    [FormerlySerializedAs("bloodEffectPrefab")]
    [InspectorName("기본 피 이펙트 프리팹")]
    [Tooltip("효과 ID가 목록에 없을 때 사용할 기본 피 이펙트입니다.")]
    [SerializeField] private GameObject fallbackBloodEffectPrefab;
    private static readonly int ColorProperty = Shader.PropertyToID("_Color");
    private static readonly int TintColorProperty = Shader.PropertyToID("_TintColor");

    private readonly Dictionary<string, BloodEffectData> _effectIdCache =
        new Dictionary<string, BloodEffectData>();
    private readonly LinkedList<GameObject> _activeBloodEffects =
        new LinkedList<GameObject>();
    private readonly Dictionary<GameObject, LinkedListNode<GameObject>> _bloodEffectNodes =
        new Dictionary<GameObject, LinkedListNode<GameObject>>();
    private readonly Dictionary<PlayerStats, float> _lastPlayerBloodSpawnTimes =
        new Dictionary<PlayerStats, float>();
    private MaterialPropertyBlock _propertyBlock;

    private void Awake()
    {
        NormalizeUnsetValues();
        RebuildCache();
    }

    private void OnEnable()
    {
        PlayerStats.DamageRendered += HandlePlayerDamageRendered;
    }

    private void OnDisable()
    {
        PlayerStats.DamageRendered -= HandlePlayerDamageRendered;
    }

    private void OnValidate()
    {
        NormalizeUnsetValues();

        if (Application.isPlaying)
        {
            RebuildCache();
        }
    }

    /// <summary>
    /// Unity가 새 리스트 항목을 모두 0으로 직렬화한 경우에만 사용 가능한 기본값으로 보정한다.
    /// 이미 사용자가 설정한 0 볼륨은 사운드 클립이 등록된 뒤에는 그대로 유지한다.
    /// </summary>
    private void NormalizeUnsetValues()
    {
        for (int i = 0; i < bloodEffects.Count; i++)
        {
            BloodEffectData data = bloodEffects[i];
            if (data == null)
            {
                continue;
            }

            if (data.soundVolume <= 0f)
            {
                data.soundVolume = 1f;
            }

            if (data.scale <= 0f)
            {
                data.scale = 1f;
            }

            if (data.randomScaleMin <= 0f && data.randomScaleMax <= 0f)
            {
                if (data.legacyRandomScaleRange.x > 0f &&
                    data.legacyRandomScaleRange.y > 0f)
                {
                    data.randomScaleMin = data.legacyRandomScaleRange.x;
                    data.randomScaleMax = data.legacyRandomScaleRange.y;
                }
                else
                {
                    data.randomScaleMin = 0.9f;
                    data.randomScaleMax = 1.1f;
                }
            }

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

        GameObject instance = SpawnSplash(prefab, position, normal, scale);

        if (data != null && data.overrideColor)
        {
            ApplyBloodColor(instance, data.color);
        }

        ApplyKriptoSettings(instance);
        RegisterBloodEffect(instance);
    }

    private void HandlePlayerDamageRendered(PlayerStats playerStats)
    {
        if (playerStats == null)
        {
            return;
        }

        if (_lastPlayerBloodSpawnTimes.TryGetValue(
                playerStats,
                out float lastSpawnTime) &&
            Time.time - lastSpawnTime < playerBloodSpawnInterval)
        {
            return;
        }

        _lastPlayerBloodSpawnTimes[playerStats] = Time.time;

        Vector3 sourcePosition = playerStats.transform.position - playerStats.transform.forward;
        NetworkBossCore boss = FindFirstObjectByType<NetworkBossCore>();
        if (boss != null)
        {
            sourcePosition = boss.transform.position;
        }

        PlayerRegistry.TryGetClosestHitCollider(
            playerStats.Object,
            sourcePosition,
            out Collider hitCollider);
        Vector3 hitPoint = playerStats.LastDamageHitPoint;
        Vector3 hitNormal = playerStats.LastDamageHitNormal;
        if (hitNormal.sqrMagnitude <= 0.000001f &&
            !TryGetSurfaceHit(
                hitCollider,
                sourcePosition,
                out hitPoint,
                out hitNormal))
        {
            hitPoint = playerStats.transform.position + Vector3.up;
            hitNormal = playerStats.transform.forward;
        }

        SpawnBlood(BasicAttack1Id, hitPoint, hitNormal.normalized);
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
        GameObject instance = EffectPoolManager.Instance != null
            ? EffectPoolManager.Instance.Get(prefab, position + spawnOffset, rotation)
            : Instantiate(prefab, position + spawnOffset, rotation);
        ResetBloodVisuals(instance);
        instance.transform.localScale = prefab.transform.localScale * scale;
        return instance;
    }

    private static bool TryGetSurfaceHit(
        Collider hitCollider,
        Vector3 sourcePosition,
        out Vector3 hitPoint,
        out Vector3 hitNormal)
    {
        hitPoint = default;
        hitNormal = default;
        if (hitCollider == null)
        {
            return false;
        }

        Vector3 toCenter = hitCollider.bounds.center - sourcePosition;
        float distance = toCenter.magnitude;
        if (distance > 0.0001f)
        {
            Ray ray = new Ray(sourcePosition, toCenter / distance);
            float maxDistance = distance + hitCollider.bounds.extents.magnitude;
            if (hitCollider.Raycast(ray, out RaycastHit hit, maxDistance))
            {
                hitPoint = hit.point;
                hitNormal = hit.normal;
                return true;
            }
        }

        hitPoint = hitCollider.ClosestPoint(sourcePosition);
        hitNormal = hitPoint - hitCollider.bounds.center;
        if (hitNormal.sqrMagnitude <= 0.000001f)
        {
            hitNormal = -toCenter;
        }

        return hitNormal.sqrMagnitude > 0.000001f;
    }

    private static void ResetBloodVisuals(GameObject instance)
    {
        if (instance == null)
        {
            return;
        }

        Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
            {
                renderers[i].enabled = true;
            }
        }

        DecalProjector[] projectors =
            instance.GetComponentsInChildren<DecalProjector>(true);
        for (int i = 0; i < projectors.Length; i++)
        {
            if (projectors[i] != null)
            {
                projectors[i].enabled = true;
            }
        }
    }

    private void RegisterBloodEffect(GameObject instance)
    {
        int limit = Mathf.Max(1, maxActiveBloodEffects);
        while (_activeBloodEffects.Count >= limit)
        {
            GameObject oldest = _activeBloodEffects.First.Value;
            RemoveBloodEffectTracking(oldest);
            ReturnBloodEffect(oldest);
        }

        LinkedListNode<GameObject> node = _activeBloodEffects.AddLast(instance);
        _bloodEffectNodes[instance] = node;

        BloodEffectAutoCleanup cleanup = instance.GetComponent<BloodEffectAutoCleanup>();
        if (cleanup == null)
        {
            cleanup = instance.AddComponent<BloodEffectAutoCleanup>();
        }

        cleanup.Begin(this, Mathf.Max(0.1f, bloodEffectMaxLifetime));
    }

    internal void ReturnBloodEffect(GameObject instance)
    {
        if (instance == null)
        {
            return;
        }

        RemoveBloodEffectTracking(instance);

        PooledInstance pooledInstance = instance.GetComponent<PooledInstance>();
        if (pooledInstance != null && pooledInstance.manager != null)
        {
            pooledInstance.ReturnToPool();
        }
        else
        {
            Destroy(instance);
        }
    }

    internal void NotifyBloodEffectDisabled(GameObject instance)
    {
        RemoveBloodEffectTracking(instance);
    }

    private void RemoveBloodEffectTracking(GameObject instance)
    {
        if (instance != null &&
            _bloodEffectNodes.TryGetValue(instance, out LinkedListNode<GameObject> node))
        {
            _activeBloodEffects.Remove(node);
            _bloodEffectNodes.Remove(instance);
        }
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

    private void ApplyKriptoSettings(GameObject instance)
    {
        BFX_BloodSettings settings =
            instance.GetComponent<BFX_BloodSettings>() ??
            instance.GetComponentInChildren<BFX_BloodSettings>(true);

        if (settings == null)
        {
            return;
        }

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

        float min = Mathf.Min(data.randomScaleMin, data.randomScaleMax);
        float max = Mathf.Max(data.randomScaleMin, data.randomScaleMax);
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

}
