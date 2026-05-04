using UnityEngine;

public class BossHitbox : MonoBehaviour
{
    public enum HitboxType { Head, Body, Claw, Mouth, Tail }
    
    [Header("부위 설정")]
    public HitboxType hitboxType;

    [Header("피격 (보스가 맞는) 설정")]
    public bool isHurtbox = true;         // 플레이어의 공격을 허용하는 부위인가?
    public float damageMultiplier = 1.0f; // 데미지 배율 (기획: 머리 1.2, 몸통 1.0)

    [Header("공격 (플레이어를 때리는) 설정")]
    public bool isAttackHitbox = false;   // 플레이어에게 데미지를 주는 부위인가?
    public float baseDamage = 10f;        // 닿았을 때 깎을 체력

    private Collider _collider;

    private void Awake()
    {
        _collider = GetComponent<Collider>();
        
        // [중요] 공격 전용 부위(예: 이빨, 발톱)라면 평소에는 판정을 꺼둡니다.
        // 나중에 보스가 공격 애니메이션을 할 때만 켤 것입니다.
        if (isAttackHitbox && !isHurtbox)
        {
            _collider.enabled = false;
        }
    }

    // 1. 보스의 공격 부위가 플레이어에게 닿았을 때 (트리거 판정)
    private void OnTriggerEnter(Collider other)
    {
        if (!isAttackHitbox) return;

        // 플레이어인지 확인 (Tag가 Player로 설정되어 있어야 합니다)
        if (other.CompareTag("Player"))
        {
            Debug.Log($"[Hit] 보스의 {hitboxType} 공격 적중! 유저 데미지: {baseDamage}");
            
            // TODO: 다음 단계에서 실제 플레이어 체력을 깎는 코드 연결
            // other.GetComponent<Player>().TakeDamage(baseDamage);
            
            // 다단히트 방지: 한 번 긁히면 콜라이더를 잠시 끕니다. 
            // (애니메이션이 끝날 때나 다음 공격 시 다시 켜질 예정)
            _collider.enabled = false; 
        }
    }

    // 2. 플레이어가 무기로 보스의 이 부위를 때렸을 때 호출할 함수
    public void OnHitByPlayer(float playerDamage)
    {
        if (!isHurtbox) return;

        // 부위별 배율 적용
        float finalDamage = playerDamage * damageMultiplier;
        
        Debug.Log($"[Damaged] 보스 {hitboxType} 피격! (배율:{damageMultiplier}x) / 최종 데미지: {finalDamage}");
        
        // TODO: 다음 단계에서 DragonBoss 본체의 체력(HP)을 깎도록 네트워크로 전달
        // GetComponentInParent<DragonBoss>().TakeBossDamage(finalDamage);
    }
}