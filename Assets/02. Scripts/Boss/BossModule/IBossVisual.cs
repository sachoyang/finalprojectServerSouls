using UnityEngine;

public interface IBossVisual
{
    // enum이나 ID가 아닌, SO에 등록된 State 이름(string)을 직접 받아(해시값) CrossFade 합니다.
    void PlayAction(int stateHash, float crossFadeTime = 0.1f);
    
    void SetSpeed(float speedValue);
    void SetAnimSpeed(float multiplier);
    void DoLocomotion();

    void PlayWakeUp(int wakeUpHash);
    void PlayPhaseTransition(int wakeUpHash);
    void PlayGroggy(float speedMultiplier);
    void PlayDie();
}