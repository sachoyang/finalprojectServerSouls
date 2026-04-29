using UnityEngine;

public class DragonVisual : MonoBehaviour
{
    public Animator anim;
    
    [Header("이펙트")]
    public ParticleSystem fireBreath;

    public void SetSpeed(float speedValue)
    {
        anim.SetFloat("MoveSpeed", speedValue);
    }

    // [추가됨] 애니메이션 재생 배속 조절
    public void SetAnimSpeed(float multiplier)
    {
        anim.speed = multiplier;
    }

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
}