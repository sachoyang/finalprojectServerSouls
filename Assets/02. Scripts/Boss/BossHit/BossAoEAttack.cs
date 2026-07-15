using UnityEngine;
using System.Collections.Generic;

public class BossAoEAttack : MonoBehaviour
{
    public enum AoEShape { Sphere, Box }

    [System.Serializable]
    public class AoEHitZone
    {
        [Header("형태 및 크기")]
        public AoEShape hitShape = AoEShape.Sphere;
        public float hitRadius = 5f;
        public Vector3 boxSize = new Vector3(5f, 5f, 5f);
        public Vector3 hitOffset = Vector3.zero;

        [Header("타이밍 (초 단위)")]
        public float startTime = 0f;
        public float duration = 0.5f;
    }

    [Header("연쇄 판정(Cascading) 구역 리스트")]
    public List<AoEHitZone> hitZones = new List<AoEHitZone>();

    // 🔥 [핵심 추가] 파티클 시스템을 끌어다 넣을 칸
    [Header("파티클 동기화 (선택사항)")]
    [Tooltip("파티클을 넣으면, 이 판정 스크립트는 파티클의 실제 재생 시간과 100% 동기화됩니다!")]
    public ParticleSystem syncParticle;

    [Header("공통 데미지 설정")]
    public float baseDamage = 20f;
    public bool isMultiHit = false;
    public float hitInterval = 0.5f;
    public LayerMask targetLayer;

    [Header("장판 중첩 처리")]
    [Tooltip("같은 그룹 이름을 가진 AoE끼리는, 판정 구역이 겹쳐도 hitInterval 동안 플레이어당 한 번만 데미지가 들어간다.\n" +
             "네이팜처럼 여러 장판이 동시에 깔려 겹치는 패턴에서 중첩 피해(2배)를 막는다.\n" +
             "빈 값이면 이 AoE는 다른 인스턴스와 정산하지 않고 각자 독립 판정한다(기존 동작). isMultiHit의 인스턴스 내부 틱과는 별개 개념.")]
    public string overlapDamageGroup = "";

    [Header("사운드")]
    [Tooltip("이펙트가 나타나는 순간 1회 재생. 이펙트 위치에서 울린다.\n" +
             "데미지 권한과 무관하게 모든 클라이언트에서 들린다.")]
    public AudioClip spawnSound;
    [Range(0f, 2f)] public float spawnSoundVolume = 1.0f;

    [Tooltip("플레이어가 맞았을 때 피격 지점에서 재생. 모든 클라이언트에서 들린다.\n" +
             "(판정은 각 클라이언트가 로컬로 돌리고, 데미지만 권한자가 적용한다)\n" +
             "isMultiHit이면 hitInterval마다 반복 재생되니 짧은 클립을 권장.")]
    public AudioClip hitSound;
    [Range(0f, 2f)] public float hitSoundVolume = 1.0f;

    [Tooltip("ON: 이펙트가 살아있는 동안 loopSound를 계속 반복 재생한다.\n" +
             "불장판(WildFire) 타닥거림, 브레스 분사음처럼 지속되는 소리에 쓴다.\n" +
             "파티클 방출이 끝나면(또는 판정 구간이 끝나면) 자동으로 멈춘다.")]
    public bool useLoopSound = false;

    [Tooltip("반복 재생할 소리. 앞뒤가 자연스럽게 이어지는 루프 클립이어야 뚝뚝 끊기지 않는다.")]
    public AudioClip loopSound;
    [Range(0f, 2f)] public float loopSoundVolume = 1.0f;

    [Tooltip("SoundManager의 카테고리별 볼륨/거리 설정에 사용된다.")]
    public SoundCategory soundCategory = SoundCategory.BossGimmick;

    // 루프 사운드 전용 AudioSource. SFX 풀은 원샷 전용이라 루프를 걸면 슬롯을 영영 점유한다.
    // 그래서 이펙트 오브젝트가 자기 것을 하나 들고 다닌다(풀 재사용 시 그대로 재활용).
    private AudioSource _loopSource;

    // 데미지 판정을 돌릴지 여부. 원격 클라이언트는 false(시각/사운드만).
    // 예전엔 이 컴포넌트를 통째로 껐는데, 그러면 풀에서 재사용될 때 OnEnable이 오지 않아
    // 스폰 사운드가 울리지 않고 상태 초기화도 누락된다.
    private bool _damageEnabled = true;

    private float _timer = 0f;
    private float _bossDamageMultiplier = 1.0f;
    private float _maxEndTime = 0f; 
    
    // 자식 콜라이더가 여러 개 있어도 PlayerStats 기준으로 다단 히트 간격을 관리한다.
    private Dictionary<PlayerStats, float> _hitTargets = new Dictionary<PlayerStats, float>();

    // 같은 overlapDamageGroup을 공유하는(겹쳐 깔린) 장판들이 같은 플레이어에게 동시에 중복 데미지를 주지 않도록,
    // 그룹별 '플레이어가 마지막으로 이 그룹에서 데미지를 받은 시각'을 인스턴스 간 전역 공유한다.
    // (인스턴스 내부의 isMultiHit 틱 간격과는 별개로, 겹친 구역에서 한쪽에서만 데미지가 들어가게 한다.)
    private static readonly Dictionary<string, Dictionary<PlayerStats, float>> s_groupHitTimes
        = new Dictionary<string, Dictionary<PlayerStats, float>>();

    // 씬 전환 등으로 파괴된 PlayerStats가 static 딕셔너리에 남아 GC를 막지 않도록 정리할 때 쓰는 스크래치.
    private static readonly List<PlayerStats> s_pruneScratch = new List<PlayerStats>();

    // 판정 종료 표시. this.enabled를 끄면 풀 재사용 시 OnEnable이 안 와서 초기화가 누락되므로,
    // 컴포넌트는 켜둔 채 이 플래그로 멈춘다.
    private bool _finished;

    // OverlapSphere/BoxNonAlloc용 고정 크기 버퍼 (매 프레임 새 배열 할당으로 인한 GC 스파이크 방지)
    private const int MaxHitsPerZone = 16;
    private readonly Collider[] _hitBuffer = new Collider[MaxHitsPerZone];
    private readonly float[] _hitDistances = new float[MaxHitsPerZone];

    // 풀에서 재활성화될 때마다 호출되어 상태를 초기화한다. (컴포넌트를 끄지 않으므로 매 재활성화마다 옴)
    private void OnEnable()
    {
        _timer = 0f;
        _finished = false;
        _hitTargets.Clear();

        // 풀에서 재사용될 때마다 판정을 다시 켠다. 스폰 직후 호출되는 Initialize/SetDamageEnabled가
        // 원격 클라이언트에서만 이 값을 다시 끈다.
        _damageEnabled = true;

        // 원격 클라이언트는 Initialize()를 받지 않는다. 여기서도 종료 시각을 구해두지 않으면
        // _maxEndTime이 0으로 남아 판정 루프가 스스로 멈추지 못한다.
        CacheMaxEndTime();

        PlayClip(spawnSound, transform.position, spawnSoundVolume);
        StartLoopSound();
    }

    // 풀에 반납되거나 파괴될 때 소리가 남지 않도록 확실히 끈다.
    private void OnDisable()
    {
        StopLoopSound();
    }

    private void StartLoopSound()
    {
        if (!useLoopSound || loopSound == null || !SoundManager.HasInstance) return;

        if (_loopSource == null)
        {
            _loopSource = gameObject.AddComponent<AudioSource>();
            _loopSource.playOnAwake = false;
        }

        SoundManager.Instance.ApplyCategoryTo3DSource(_loopSource, soundCategory, loopSoundVolume);
        _loopSource.clip = loopSound;
        _loopSource.loop = true;
        _loopSource.Play();
    }

    private void StopLoopSound()
    {
        if (_loopSource != null && _loopSource.isPlaying) _loopSource.Stop();
    }

    private void CacheMaxEndTime()
    {
        _maxEndTime = 0f;
        foreach (var zone in hitZones)
        {
            float endTime = zone.startTime + zone.duration;
            if (endTime > _maxEndTime) _maxEndTime = endTime;
        }
    }

    // 원격 클라이언트에서 데미지 판정만 끈다(시각/사운드는 그대로).
    public void SetDamageEnabled(bool enabledFlag)
    {
        _damageEnabled = enabledFlag;
    }

    private void PlayClip(AudioClip clip, Vector3 position, float volume)
    {
        if (clip == null || !SoundManager.HasInstance) return;
        SoundManager.Instance.PlaySFX_3D(clip, position, soundCategory, volume);
    }

    public void Initialize(float bossOutgoingMultiplier)
    {
        _bossDamageMultiplier = bossOutgoingMultiplier;

        // [풀 재사용 대비] 상태 초기화 (OnEnable과 중복돼도 무해)
        _timer = 0f;
        _finished = false;
        _hitTargets.Clear();

        CacheMaxEndTime();
    }

    private void Update()
    {
        if (_finished) return;

        // 🔥 [동기화 로직] 파티클이 연결되어 있다면 파티클의 시계를, 없다면 아날로그 시계를 씁니다.
        if (syncParticle != null)
        {
            _timer = syncParticle.time; // 파티클의 현재 재생 시간을 그대로 가져옴!

            // 🔁 방출(emission)이 끝나면 루프 사운드부터 끊는다.
            //    남은 파티클이 스러지는 동안(IsAlive) 타닥거리는 소리가 계속 나면 어색하다.
            if (!syncParticle.isEmitting) StopLoopSound();

            // 파티클 수명이 완전히 끝났다면 판정 정지
            if (!syncParticle.IsAlive(true))
            {
                _finished = true;
                StopLoopSound();
                return;
            }
        }
        else
        {
            _timer += Time.deltaTime; // 기존 방식

            if (_timer > _maxEndTime && _maxEndTime > 0f)
            {
                _finished = true;
                StopLoopSound();
                return;
            }
        }

        // 데미지도 안 주고 히트 사운드도 없으면 오버랩 검사를 돌릴 이유가 없다(원격 클라 부하 절약).
        if (!_damageEnabled && hitSound == null) return;

        DetectAndDamage();
    }

    // 판정은 모든 클라이언트가 로컬로 돌린다(히트 사운드를 전원이 듣기 위해).
    // 단 실제 데미지 적용은 _damageEnabled인 권한자만 한다 → 인원수만큼 중복 피해 방지.
    private void DetectAndDamage()
    {
        foreach (var zone in hitZones)
        {
            if (_timer < zone.startTime || _timer > zone.startTime + zone.duration) continue;

            Vector3 centerPosition = transform.position + (transform.rotation * zone.hitOffset);
            int hitCount = 0;

            // NonAlloc 버전 + 고정 버퍼로 매 프레임 배열 할당(GC)을 제거.
            if (zone.hitShape == AoEShape.Sphere)
            {
                hitCount = Physics.OverlapSphereNonAlloc(centerPosition, zone.hitRadius, _hitBuffer, targetLayer);
            }
            else if (zone.hitShape == AoEShape.Box)
            {
                hitCount = Physics.OverlapBoxNonAlloc(centerPosition, zone.boxSize / 2f, _hitBuffer, transform.rotation, targetLayer);
            }

            SortBySurfaceDistance(_hitBuffer, _hitDistances, hitCount, centerPosition);
            for (int h = 0; h < hitCount; h++)
            {
                Collider hit = _hitBuffer[h];
                PlayerHitbox playerHitbox = hit.GetComponentInParent<PlayerHitbox>();
                if (playerHitbox == null || !playerHitbox.Matches(hit))
                {
                    continue;
                }

                PlayerStats playerStats = hit.GetComponentInParent<PlayerStats>();
                if (playerStats == null || playerStats.IsDead)
                {
                    continue;
                }

                if (_hitTargets.TryGetValue(playerStats, out float lastHitTime))
                {
                    if (!isMultiHit) continue;
                    if (Time.time - lastHitTime < hitInterval) continue;
                }

                GetHitSurface(
                    hit,
                    centerPosition,
                    out Vector3 hitPoint,
                    out Vector3 hitNormal);

                // 데미지는 권한자만. 원격 클라이언트는 같은 지점에서 소리만 낸다.
                // 단, 같은 그룹의 다른 장판이 방금 이 플레이어를 때렸다면(겹친 구역) 데미지는 건너뛴다.
                if (_damageEnabled && !IsOverlapDamageBlocked(playerStats))
                {
                    float finalDamage = baseDamage * _bossDamageMultiplier;
                    playerStats.TakeDamage(finalDamage, hitPoint, hitNormal);
                    MarkOverlapDamage(playerStats);
                    BossLog.Info($"[AoE Hit] 광역 이펙트 연쇄 적중! 파티클 동기화 딜: {finalDamage}");
                }

                // 다단 히트 간격은 양쪽 다 관리해야 소리도 같은 주기로 울린다.
                _hitTargets[playerStats] = Time.time;

                PlayClip(hitSound, hitPoint, hitSoundVolume);
            }
        }
    }

    // 겹친 장판 중복 방지: 같은 그룹이 hitInterval 안에 이미 이 플레이어를 때렸으면 true(→ 데미지 스킵).
    private bool IsOverlapDamageBlocked(PlayerStats player)
    {
        if (string.IsNullOrEmpty(overlapDamageGroup)) return false;
        if (s_groupHitTimes.TryGetValue(overlapDamageGroup, out var map)
            && map.TryGetValue(player, out float lastTime))
        {
            return Time.time - lastTime < hitInterval;
        }
        return false;
    }

    // 이 그룹에서 방금 이 플레이어에게 데미지를 줬음을 전역에 기록한다.
    private void MarkOverlapDamage(PlayerStats player)
    {
        if (string.IsNullOrEmpty(overlapDamageGroup)) return;

        if (!s_groupHitTimes.TryGetValue(overlapDamageGroup, out var map))
        {
            map = new Dictionary<PlayerStats, float>();
            s_groupHitTimes[overlapDamageGroup] = map;
        }
        map[player] = Time.time;

        // 파괴된(씬 전환 등) 플레이어 키를 정리해 static 딕셔너리가 무한정 커지지 않게 한다.
        s_pruneScratch.Clear();
        foreach (var key in map.Keys)
        {
            if (key == null) s_pruneScratch.Add(key);
        }
        for (int i = 0; i < s_pruneScratch.Count; i++) map.Remove(s_pruneScratch[i]);
    }

    private static void GetHitSurface(
        Collider hitCollider,
        Vector3 sourcePosition,
        out Vector3 hitPoint,
        out Vector3 hitNormal)
    {
        Vector3 toCenter = hitCollider.bounds.center - sourcePosition;
        float distance = toCenter.magnitude;
        if (distance > 0.0001f &&
            hitCollider.Raycast(
                new Ray(sourcePosition, toCenter / distance),
                out RaycastHit hit,
                distance + hitCollider.bounds.extents.magnitude))
        {
            hitPoint = hit.point;
            hitNormal = hit.normal;
            return;
        }

        hitPoint = hitCollider.ClosestPoint(sourcePosition);
        hitNormal = (hitPoint - hitCollider.bounds.center).normalized;
    }

    private static void SortBySurfaceDistance(
        Collider[] colliders,
        float[] distances,
        int count,
        Vector3 sourcePosition)
    {
        // ClosestPoint(물리 연산)는 콜라이더당 딱 한 번만 계산해서 캐싱.
        for (int i = 0; i < count; i++)
        {
            distances[i] =
                (colliders[i].ClosestPoint(sourcePosition) - sourcePosition).sqrMagnitude;
        }

        // 캐싱된 거리 기준 삽입 정렬 (count가 작아 충분히 빠름)
        for (int i = 1; i < count; i++)
        {
            Collider value = colliders[i];
            float valueDistance = distances[i];
            int j = i - 1;
            while (j >= 0 && distances[j] > valueDistance)
            {
                colliders[j + 1] = colliders[j];
                distances[j + 1] = distances[j];
                j--;
            }

            colliders[j + 1] = value;
            distances[j + 1] = valueDistance;
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);

        foreach (var zone in hitZones)
        {
            bool isActiveNow = false;

            if (Application.isPlaying)
            {
                if (_timer >= zone.startTime && _timer <= zone.startTime + zone.duration)
                {
                    isActiveNow = true;
                }
            }
            else
            {
                isActiveNow = true;
            }

            if (isActiveNow)
            {
                Gizmos.color = new Color(1f, 0.2f, 0f, 0.6f); 
            }
            else
            {
                Gizmos.color = new Color(0.5f, 0.5f, 0.5f, 0.1f); 
            }

            if (zone.hitShape == AoEShape.Sphere)
            {
                Gizmos.DrawSphere(zone.hitOffset, zone.hitRadius);
                if (isActiveNow) { Gizmos.color = Color.red; Gizmos.DrawWireSphere(zone.hitOffset, zone.hitRadius); }
            }
            else if (zone.hitShape == AoEShape.Box)
            {
                Gizmos.DrawCube(zone.hitOffset, zone.boxSize);
                if (isActiveNow) { Gizmos.color = Color.red; Gizmos.DrawWireCube(zone.hitOffset, zone.boxSize); }
            }
        }
    }
}
