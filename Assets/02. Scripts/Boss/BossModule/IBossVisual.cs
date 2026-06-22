using UnityEngine;

public interface IBossVisual
{
    // enum이나 ID가 아닌, SO에 등록된 State 이름(string)을 직접 받아(해시값) CrossFade 합니다.
    void PlayAction(int stateHash, float crossFadeTime = 0.1f);

    void SetDirection(float dirX, float dirY);
    
    void SetLookAtTarget(Vector3 targetPos);

    void ResetLookAt();

    void SetAnimSpeed(float multiplier);
    void DoLocomotion();

    void PlayWakeUp(int wakeUpHash);
    void PlayPhaseTransition(int wakeUpHash);
    void PlayGroggy(float speedMultiplier);
    void PlayDie();

    // 루트모션 이동 처리
    // 호스트가 루트모션 액션 시작/종료 시 캡처를 켜고 끈다.
    void SetRootMotionCapture(bool enabled);
    // 호스트가 매 틱 모아둔 루트모션 이동량을 가져간다(가져가면 0으로 리셋).
    Vector3 ConsumeRootMotionDelta();
}