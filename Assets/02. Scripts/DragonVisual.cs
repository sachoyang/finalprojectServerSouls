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
    public void DoSleep() { anim.SetTrigger("DoSleep"); }

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
}