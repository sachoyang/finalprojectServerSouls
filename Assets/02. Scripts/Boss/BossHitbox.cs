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
    
    // [핵심] 콜라이더를 끄는 대신, 이 변수로 데미지 판정 여부를 결정합니다.
    public bool _isCurrentlyAttacking = false;

    // 데미지를 전달할 최상위 보스 스크립트
    private DragonBoss _bossScript;

    private void Awake()
    {
        _collider = GetComponent<Collider>();
        _collider.isTrigger = true;
        _collider.enabled = true; // 무조건 켜둠 (언제든 맞을 수 있게)

        // 시작할 때 부모 오브젝트에 있는 DragonBoss 스크립트를 찾아둡니다.
        _bossScript = GetComponentInParent<DragonBoss>();
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
        // 공격 중이 아니거나 공격 부위가 아니면 무시
        if (!isAttackHitbox || !_isCurrentlyAttacking) return;

        if (other.CompareTag("Player"))
        {
            // PlayerStats 스크립트를 가져와서 데미지 함수를 호출합니다!
            PlayerStats playerStats = other.GetComponent<PlayerStats>();
            if (playerStats != null)
            {
                playerStats.TakeDamage(baseDamage);
                Debug.Log($"[Hit] 보스의 {hitboxType} 공격 적중! 플레이어에게 {baseDamage} 데미지 줌!");
            }
            
            // 다단히트 방지: 한 대 치면 이번 공격 턴에서는 데미지 판정 오프
            _isCurrentlyAttacking = false; 
        }
    }

    // ==========================================
    // 2. 보스가 맞았을 때 (무기 스크립트에서 이 함수를 호출할 예정)
    // ==========================================
    public void OnHitByPlayer(float playerDamage)
    {
        if (!isHurtbox) return;
        float finalDamage = playerDamage * damageMultiplier;
        Debug.Log($"[Damaged] 보스 {hitboxType} 피격! (배율: {damageMultiplier}x, 최종 데미지: {finalDamage})");

        // 부모(DragonBoss)에게 네트워크로 체력을 깎으라고 명령합니다!
        if (_bossScript != null)
        {
            _bossScript.RPC_TakeDamage(finalDamage);
        }
    }
}