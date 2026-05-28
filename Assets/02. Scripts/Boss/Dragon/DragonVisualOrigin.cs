using UnityEngine;

public class DragonVisualOrigin : MonoBehaviour
{
    public Animator anim;

    [Header("이펙트")]
    public ParticleSystem fireBreath;

    [Header("히트박스 연결")]
    public BossHitbox mouthHitbox;
    public BossHitbox leftClawHitbox;
    public BossHitbox rightClawHitbox;

    [Header("광역기 이펙트 프리팹")]
    public GameObject jumpSlamEffectPrefab;
    public Transform jumpSlamSpawnPoint; 

    [Header("경고 장판 (Telegraph)")]
    public GameObject jumpWarningPrefab;

    private void Awake()
    {
        if (anim == null)
        {
            anim = GetComponent<Animator>();
        }
    }

    public void SetSpeed(float speedValue) { anim.SetFloat("MoveSpeed", speedValue); }
    public void SetAnimSpeed(float multiplier) { anim.speed = multiplier; }

    // ==========================================
    // [수정됨] SetTrigger 대신 CrossFade를 사용하여 멀티플레이 애니메이션 씹힘 방지!
    // ==========================================
    public void DoBiteAttack()
    {
        anim.CrossFade("Basic Attack", 0.1f);
        if (fireBreath != null) fireBreath.Play();
    }

    public void DoClawAttack() { anim.CrossFade("Claw Attack", 0.1f); }
    public void DoHornAttack() { anim.CrossFade("Horn Attack", 0.1f); }
    public void DoJump()       { anim.CrossFade("Jump", 0.1f); }
    public void DoScream()     { anim.CrossFade("Scream", 0.1f); }
    public void DoDie()        { anim.CrossFade("die", 0.1f); }
    
    // ==========================================
    // [추가됨] 어딘가에 갇혀있을 때 강제로 걷기/대기로 끌고 오는 함수
    // ==========================================
    public void DoLocomotion() { anim.CrossFade("Locomotion", 0.1f); }

    // 수면 상태는 Bool로 유지
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

    public void SpawnJumpWarning()
    {
        if (jumpWarningPrefab != null && jumpSlamSpawnPoint != null)
        {
            Instantiate(jumpWarningPrefab, jumpSlamSpawnPoint.position, jumpWarningPrefab.transform.rotation);
        }
    }

    public void SpawnJumpSlamEffect()
    {
        if (jumpSlamEffectPrefab != null && jumpSlamSpawnPoint != null)
        {
            Instantiate(jumpSlamEffectPrefab, jumpSlamSpawnPoint.position, jumpSlamSpawnPoint.rotation);
        }
    }
}