using UnityEngine;

public class DragonVisual : MonoBehaviour
{
    public Animator anim;

    [Header("이펙트")]
    public ParticleSystem fireBreath;

    [Header("히트박스 연결")]
    public BossHitbox mouthHitbox;
    public BossHitbox leftClawHitbox;
    public BossHitbox rightClawHitbox;

    // (기존 변수들 아래에 추가)
    [Header("광역기 이펙트 프리팹")]
    public GameObject jumpSlamEffectPrefab;
    public Transform jumpSlamSpawnPoint; // 보스 발밑 위치 (보스 자식으로 빈 오브젝트 하나 만들어서 할당)

    private void Awake()
    {
        // 이제 같은 오브젝트에 있으므로 알아서 Animator를 찾아 연결합니다.
        if (anim == null)
        {
            anim = GetComponent<Animator>();
        }
    }

    public void SetSpeed(float speedValue) { anim.SetFloat("MoveSpeed", speedValue); }
    public void SetAnimSpeed(float multiplier) { anim.speed = multiplier; }

    public void DoBiteAttack()
    {
        anim.SetTrigger("DoBite");
        if (fireBreath != null) fireBreath.Play();
    }

    public void DoClawAttack() { anim.SetTrigger("DoClaw"); }
    public void DoHornAttack() { anim.SetTrigger("DoHorn"); }
    public void DoJump() { anim.SetTrigger("DoJump"); }
    public void DoScream() { anim.SetTrigger("DoScream"); }
    public void SetSleep(bool isSleeping) { anim.SetBool("DoSleep", isSleeping); }
    // ==========================================
    // [애니메이션 이벤트용 함수]
    // ==========================================
    public void EnableMouthHitbox() { if (mouthHitbox != null) mouthHitbox.StartAttack(); }
    public void DisableMouthHitbox() { if (mouthHitbox != null) mouthHitbox.StopAttack(); }

    public void EnableClawHitbox()
    {
        if (leftClawHitbox != null) leftClawHitbox.StartAttack();
        if (rightClawHitbox != null) rightClawHitbox.StartAttack();
    }
    public void DisableClawHitbox()
    {
        if (leftClawHitbox != null) leftClawHitbox.StopAttack();
        if (rightClawHitbox != null) rightClawHitbox.StopAttack();
    }

    // ==========================================
    // [애니메이션 이벤트용 함수 추가]
    // ==========================================
    public void SpawnJumpSlamEffect()
    {
        if (jumpSlamEffectPrefab != null && jumpSlamSpawnPoint != null)
        {
            // 설정해둔 보스 발밑 위치와 회전값 그대로 이펙트 생성
            Instantiate(jumpSlamEffectPrefab, jumpSlamSpawnPoint.position, jumpSlamSpawnPoint.rotation);
        }
    }
}