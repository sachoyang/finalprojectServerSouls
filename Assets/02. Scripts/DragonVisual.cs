using UnityEngine;

public class DragonVisual : MonoBehaviour
{
    public Animator anim;
    
    [Header("이펙트 (나중에 연결)")]
    public ParticleSystem fireBreath;

    // 1. 걷기/대기 전환용 (speed 값이 0.1을 넘으면 걷기 애니메이션 재생)
    public void SetSpeed(float speedValue)
    {
        anim.SetFloat("MoveSpeed", speedValue);
    }

    // 2. 일회성 패턴 애니메이션들 (Trigger 작동)
    public void DoBiteAttack() 
    { 
        anim.SetTrigger("DoBite"); 
        
        // 이펙트가 연결되어 있다면 재생
        if (fireBreath != null) fireBreath.Play(); 
    }

    public void DoClawAttack() 
    { 
        anim.SetTrigger("DoClaw"); 
    }

    public void DoHornAttack() 
    { 
        anim.SetTrigger("DoHorn"); 
    }

    public void DoJump() 
    { 
        anim.SetTrigger("DoJump"); 
    }

    public void DoScream() 
    { 
        anim.SetTrigger("DoScream"); 
    }
    
    public void DoSleep()
    {
        // 당장 연결은 안 했지만 나중에 쓸 Sleep (필요시 세팅)
        anim.SetTrigger("DoSleep");
    }
}