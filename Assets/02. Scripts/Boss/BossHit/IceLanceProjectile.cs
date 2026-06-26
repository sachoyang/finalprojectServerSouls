using UnityEngine;

// [역할] 폴라 드래곤의 얼음창(icelance) 투사체.
//  - 생성 시 정해진 '목적지(어그로 플레이어 위치)' 방향으로 직선 비행한다. (호밍 X, 따라가지 않음)
//  - 땅(Ground) 또는 플레이어(Player)에 맞으면 그 자리에 폭발 이펙트(energyExplosion)를 생성하고 사라진다.
//  - 폭발의 광역 데미지는 폭발 프리팹에 붙은 BossAoEAttack가 담당한다.
//  - 멀티플레이 중복 데미지를 막기 위해, 데미지(폭발의 AoE)는 보스 권한(호스트)에서만 활성화한다.
//    (시각 효과인 얼음창/폭발 자체는 모든 클라이언트에서 보이도록 각자 생성됨)
public class IceLanceProjectile : MonoBehaviour
{
    [Header("비행 설정")]
    [Tooltip("초당 비행 속도")]
    public float speed = 25f;
    [Tooltip("아무것도 못 맞혔을 때 자동 소멸까지의 시간(초)")]
    public float maxLifeTime = 5f;

    [Header("충돌 설정")]
    [Tooltip("충돌로 인정할 레이어 (Ground + Player 를 체크)")]
    public LayerMask hitMask;
    [Tooltip("충돌 감지용 구체 반경 (얼음창 두께 정도)")]
    public float hitRadius = 0.4f;

    [Header("폭발")]
    [Tooltip("맞은 자리에 생성할 폭발 프리팹 (energyExplosion). 인스펙터에서 비워두면 발사 시 코드가 넣어준다.")]
    public GameObject explosionPrefab;

    private Vector3 _dir;
    private float _life;
    private bool _applyDamage;             // 이 인스턴스가 데미지를 줄 권한(호스트)인지
    private float _damageMultiplier = 1f;  // 보스 외부 데미지 배율
    private bool _exploded;

    // 생성 직후 PolarDragonVisual이 1회 호출.
    // targetPos = 생성 순간의 어그로 플레이어 위치(고정). applyDamage = 보스 권한 여부.
    public void Launch(Vector3 targetPos, GameObject explosion, bool applyDamage, float damageMultiplier)
    {
        _dir = (targetPos - transform.position);
        _dir.Normalize();
        if (_dir == Vector3.zero) _dir = transform.forward;

        transform.rotation = Quaternion.LookRotation(_dir); // 메쉬/파티클 정면(+Z)을 진행 방향으로
        if (explosion != null) explosionPrefab = explosion;
        _applyDamage = applyDamage;
        _damageMultiplier = damageMultiplier;
    }

    private void Update()
    {
        _life += Time.deltaTime;
        if (_life >= maxLifeTime)
        {
            // 아무것도 못 맞히고 수명 초과 → 폭발 없이 조용히 제거
            Destroy(gameObject);
            return;
        }

        float step = speed * Time.deltaTime;

        // 이번 프레임 이동 경로를 따라 충돌 검사 (빠른 속도로 인한 관통 방지)
        if (Physics.SphereCast(transform.position, hitRadius, _dir, out RaycastHit hit, step,
                               hitMask, QueryTriggerInteraction.Collide))
        {
            transform.position = hit.point;
            Explode(hit.point, hit.normal);
            return;
        }

        transform.position += _dir * step;
    }

    private void Explode(Vector3 pos, Vector3 normal)
    {
        if (_exploded) return;
        _exploded = true;

        if (explosionPrefab != null)
        {
            // 폭발 이펙트는 법선 방향으로 세워 생성 (땅이면 위로, 벽이면 벽 바깥쪽으로)
            Quaternion rot = (normal != Vector3.zero) ? Quaternion.LookRotation(normal) : Quaternion.identity;
            GameObject fx = Instantiate(explosionPrefab, pos, rot);

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

        Destroy(gameObject);
    }
}
