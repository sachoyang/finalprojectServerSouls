using UnityEngine;

[RequireComponent(typeof(Collider))]
public class DamageEffect : MonoBehaviour
{
    [Header("데미지 설정")]
    public float damage = 2000f;

    [Header("타이밍 설정 (초 단위)")]
    [Tooltip("이펙트 생성 후 몇 초 뒤에 판정을 켤 것인가?")]
    public float hitboxDelay = 0.2f;    
    
    [Tooltip("판정을 켠 후 몇 초 동안 유지할 것인가?")]
    public float hitboxDuration = 0.5f; 
    
    [Tooltip("이펙트가 완전히 사라지는 시간")]
    public float destroyTime = 3.0f;    

    private Collider _collider;
    private bool _canDamage = false;

    private void Awake()
    {
        _collider = GetComponent<Collider>();
        _collider.isTrigger = true;
        _collider.enabled = false; // 시작할 때는 일단 데미지 판정을 꺼둡니다.
    }

    private void Start()
    {
        // 정해진 시간표대로 콜라이더를 켜고 끄고, 스스로를 파괴(Destroy)합니다.
        Invoke(nameof(EnableHitbox), hitboxDelay);
        Invoke(nameof(DisableHitbox), hitboxDelay + hitboxDuration);
        
        // 메모리 누수를 막기 위해 무조건 파괴
        Destroy(gameObject, destroyTime);
    }

    private void EnableHitbox()
    {
        _collider.enabled = true;
        _canDamage = true;
        Debug.Log($"[{gameObject.name}] 데미지 판정 시작!");
    }

    private void DisableHitbox()
    {
        _collider.enabled = false;
        _canDamage = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!_canDamage) return;

        // 플레이어 태그 확인
        if (other.CompareTag("Player"))
        {
            PlayerStats playerStats = other.GetComponent<PlayerStats>();
            if (playerStats != null)
            {
                // PlayerStats.TakeDamage 안에 자동으로 호스트로 전달되는 RPC가 구현되어 있으므로 그냥 찌르면 됩니다!
                playerStats.TakeDamage(damage);
                Debug.Log($"[Effect Hit] 이펙트 적중! 플레이어에게 {damage} 데미지!");
            }
        }
    }
}