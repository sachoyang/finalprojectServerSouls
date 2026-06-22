using Fusion;
using UnityEngine;

// [역할] 보스의 뼈(Bone)에 붙어서, 플레이어의 공격을 대신 맞아주고 부모(Core)에게 데미지를 전달합니다.
[RequireComponent(typeof(Collider))]
public class BossHurtbox : MonoBehaviour
{
    public enum BodyPart { Head, Chest, Arm, Leg, Tail, Wings }

    [Header("피격 부위 설정")]
    public BodyPart bodyPart;

    [Tooltip("이 부위를 때렸을 때 들어가는 데미지 배율 (예: 머리 1.5배, 꼬리 0.8배)")]
    public float damageMultiplier = 1.0f;

    [Header("그로기 누적 설정")]
    [Tooltip("이 부위를 때렸을 때 누적되는 그로기(Stagger) 수치 배율")]
    public float groggyMultiplier = 1.0f;

    // 데미지를 전달할 최상위 보스 스크립트
    private NetworkBossCore _bossScript;

    private void Awake()
    {
        // 시작할 때 부모 최상단에 있는 NetworkBossCore를 자동으로 찾습니다.
        _bossScript = GetComponentInParent<NetworkBossCore>();
        
        // 피격용 콜라이더는 물리적으로 밀어내지 않고 겹치도록(Trigger) 설정합니다.
        // (밀어내는 역할은 PhysicBase의 원통 콜라이더가 이미 하고 있습니다)
        Collider col = GetComponent<Collider>();
        col.isTrigger = true; 
    }

    // ==========================================
    // 플레이어의 무기 스크립트에서 때렸을 때 이 함수를 호출하게 됩니다.
    // ==========================================
    public void OnHitByPlayer(float baseDamage, float baseGroggy = 10f, NetworkObject attacker = null)
    {
        if (_bossScript == null) return;

        // 부위별 약점 배율 적용
        float finalDamage = baseDamage * damageMultiplier;
        float finalGroggy = baseGroggy * groggyMultiplier;

        Debug.Log($"[Hurtbox] {bodyPart} 피격! (데미지: {finalDamage}, 그로기: {finalGroggy})");

        // 부모에게 최종 데미지 전달 (네트워크 동기화)
        _bossScript.RPC_TakeDamage(finalDamage, finalGroggy, attacker);
    }
}