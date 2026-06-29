using UnityEngine;

// [역할] 폴라 드래곤의 얼음창(icelance_one) — 얼음이 사라지는 순간 폭발시키는 트리거.
//  핵심 아이디어: 얼음이 '맞고 사라질 때' 터지는 조각 파티클(TinyShards)에 이 스크립트를 붙인다.
//   - 얼음창은 파티클이 알아서 날아가고/박히고, 수명이 끝나면 죽으면서 서브 이미터(TinyShards)로 조각을 흩뿌린다.
//   - 그 TinyShards가 '나타나는 순간' = 얼음이 실제로 사라지는 순간이므로, 그때 그 자리에 폭발 프리팹을 띄운다.
//   - 비행 거리와 무관하게 "맞고 사라지는 바로 그 시점"에 폭발이 동기화된다. (충돌 타이머/IsAlive 지연 불필요)
//  - 데미지는 "박힐 때"가 아니라 "폭발할 때"만 발생한다. 데미지는 폭발 프리팹의 BossAoEAttack가 담당한다.
//  - 멀티플레이 중복 데미지를 막기 위해, 폭발의 AoE 데미지는 보스 권한(호스트)에서만 활성화한다.
//
//  ★ 부착 위치: 얼음이 사라질 때 재생되는 조각 파티클(TinyShards) 오브젝트에 붙인다.
//    (이 파티클이 살아나는 시점 = 얼음이 사라지는 시점)
public class IceLanceProjectile : MonoBehaviour
{
    [Header("폭발")]
    [Tooltip("얼음이 사라질 때 생성할 폭발 프리팹 (energyExplosion). 비워두면 발사 시 코드가 넣어준다.")]
    public GameObject explosionPrefab;

    [Tooltip("폭발 생성 후 얼음창 오브젝트 전체를 정리하기까지의 여유 시간(초). 조각/미스트가 자연스럽게 끝나도록.")]
    public float cleanupDelay = 3f;

    private ParticleSystem _ps;            // 사라짐 조각 파티클(TinyShards) — 이 오브젝트에 있는 것
    private bool _applyDamage;             // 이 인스턴스가 데미지를 줄 권한(호스트)인지
    private float _damageMultiplier = 1f;  // 보스 외부 데미지 배율

    private bool _armed;                   // 처음 '비어있음'을 한 번 확인했는지(시작 직후 오발 방지)
    private bool _exploded;
    private ParticleSystem.Particle[] _buffer; // 조각 위치 샘플용 버퍼

    private void Awake()
    {
        _ps = GetComponent<ParticleSystem>();
        if (_ps == null) _ps = GetComponentInChildren<ParticleSystem>();
    }

    // 생성 직후 PolarDragonVisual이 GetComponentInChildren로 찾아 1회 호출.
    public void Launch(Vector3 targetPos, GameObject explosion, bool applyDamage, float damageMultiplier)
    {
        if (explosion != null) explosionPrefab = explosion;
        _applyDamage = applyDamage;
        _damageMultiplier = damageMultiplier;
    }

    private void Update()
    {
        if (_exploded || _ps == null) return;

        bool alive = _ps.IsAlive(false);

        // 시작 직후엔 아직 조각이 안 뿌려진 상태(빈 파티클)다. 먼저 '비어있음'을 한 번 확인해 무장한다.
        if (!_armed)
        {
            if (!alive) _armed = true;
            return;
        }

        // 무장 후 조각이 처음으로 나타나는 순간 = 얼음이 사라지는 순간 → 그 자리에 폭발.
        if (alive)
        {
            Vector3 pos = TryGetParticleCenter(out Vector3 center) ? center : transform.position;
            Explode(pos);
        }
    }

    // 현재 살아있는 조각들의 중심 위치(월드)를 구한다. 조각들이 박힌 자리에 모여 있으므로 폭발 지점으로 적합.
    private bool TryGetParticleCenter(out Vector3 center)
    {
        center = transform.position;

        int count = _ps.particleCount;
        if (count <= 0) return false;

        if (_buffer == null || _buffer.Length < count)
            _buffer = new ParticleSystem.Particle[Mathf.Max(count, 16)];

        int n = _ps.GetParticles(_buffer);
        if (n <= 0) return false;

        Vector3 sum = Vector3.zero;
        for (int i = 0; i < n; i++) sum += _buffer[i].position;
        center = sum / n;

        // 파티클 좌표는 시뮬레이션 공간 기준 — Local이면 월드로 변환.
        if (_ps.main.simulationSpace == ParticleSystemSimulationSpace.Local)
            center = transform.TransformPoint(center);

        return true;
    }

    // 폭발 발생. 데미지는 여기서만(폭발 프리팹의 BossAoEAttack) 발생한다.
    private void Explode(Vector3 pos)
    {
        if (_exploded) return;
        _exploded = true;

        if (explosionPrefab != null)
        {
            // 풀에서 꺼내 재사용(파티클 끝나면 자동 회수). 풀이 없으면 폴백 생성.
            // → 더 이상 Destroy로 직접 정리하지 않는다(풀이 수명 관리).
            GameObject fx = EffectPoolManager.SpawnPooled(explosionPrefab, pos, Quaternion.identity);

            BossAoEAttack aoe = fx.GetComponent<BossAoEAttack>();
            if (aoe != null)
            {
                if (_applyDamage)
                {
                    aoe.Initialize(_damageMultiplier); // 호스트: 데미지 활성화
                }
                else
                {
                    aoe.enabled = false; // 그 외 클라이언트: 시각 폭발만, 데미지 끔(중복 방지)
                }
            }
        }

        // 조각/미스트가 자연스럽게 끝나도록 얼음창 오브젝트 전체를 잠시 뒤 정리.
        Destroy(transform.root.gameObject, cleanupDelay);
    }
}
