using Fusion;
using UnityEngine;

public class BossHitbox : MonoBehaviour
{
    public enum HitboxType { Head, Body, Claw, Mouth, Tail }

    [Header("부위 설정")]
    public HitboxType hitboxType;

    [Header("피격 (보스가 맞는) 설정")]
    public bool isHurtbox = true;
    public float damageMultiplier = 1.0f;

    [Header("공격 (플레이어를 때리는) 설정")]
    public bool isAttackHitbox = false;
    public float baseDamage = 10f;

    private Collider _collider;

    public AudioClip audioClip_hit;

    // [핵심] 콜라이더를 끄는 대신, 이 변수로 데미지 판정 여부를 결정합니다.
    public bool _isCurrentlyAttacking = false;

    // 데미지를 전달할 최상위 보스 스크립트
    private NetworkBossCore _bossScript;

    private void Awake()
    {
        _collider = GetComponent<Collider>();
        _collider.isTrigger = true;
        _collider.enabled = true; // 무조건 켜둠 (언제든 맞을 수 있게)

        // 시작할 때 부모 오브젝트에 있는 공통 보스 코어를 찾아둡니다.
        _bossScript = GetComponentInParent<NetworkBossCore>();
    }

    // ==========================================
    // 외부(DragonVisual)에서 애니메이션 이벤트로 호출할 함수
    // ==========================================
    public void StartAttack()
    {
        if (isAttackHitbox) _isCurrentlyAttacking = true;
    }

    public void StopAttack()
    {
        if (isAttackHitbox) _isCurrentlyAttacking = false;
    }

    // ==========================================
    // 1. 플레이어를 때렸을 때
    // ==========================================
    private void OnTriggerEnter(Collider other)
    {
        if (!isAttackHitbox || !_isCurrentlyAttacking) return;

        if (other.CompareTag("Player"))
        {
            PlayerStats playerStats = other.GetComponent<PlayerStats>();
            if (playerStats != null)
            {
                // 🔥 보스의 버프 상태(공업 등)를 읽어와서 최종 데미지 계산!
                float finalDamage = baseDamage;
                if (_bossScript != null)
                {
                    finalDamage *= _bossScript.GetOutgoingDamageMultiplier();
                }

                playerStats.TakeDamage(finalDamage);
                Debug.Log($"[Hit] 보스 공격 적중! 최종 딜: {finalDamage}");

            }

            _isCurrentlyAttacking = false;
        }
    }

    // ==========================================
    // 2. 보스가 맞았을 때 (무기 스크립트에서 이 함수를 호출할 예정)
    // ==========================================
    public void OnHitByPlayer(float playerDamage, NetworkObject attacker)
    {
        if (!isHurtbox) return;
        float finalDamage = playerDamage * damageMultiplier;
        Debug.Log($"[Damaged] 보스 {hitboxType} 피격! (배율: {damageMultiplier}x, 최종 데미지: {finalDamage})");

        // 부모(NetworkBossCore)에게 네트워크로 체력을 깎으라고 명령합니다.
        if (_bossScript != null)
        {
            _bossScript.RPC_TakeDamage(finalDamage);
            if (audioClip_hit != null)
            {
                SoundManager.Instance.PlaySFX_3D(audioClip_hit, transform.position, SoundCategory.CombatHurt);
            }
        }
    }
}
