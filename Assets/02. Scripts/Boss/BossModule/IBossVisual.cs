using UnityEngine;

public interface IBossVisual
{
    // enum이나 ID가 아닌, SO에 등록된 State 이름(string)을 직접 받아(해시값) CrossFade 합니다.
    void PlayAction(int stateHash, float crossFadeTime = 0.1f);

    void SetDirection(float dirX, float dirY);

    // 제자리 회전(Turn-in-place) 블렌딩용. turnSign: -1(좌) ~ 0(정면) ~ +1(우).
    // Animator의 Turn 블렌드 트리에 연결해 발 스텝으로 회전을 표현(풋슬라이딩 제거).
    void SetTurn(float turnSign);

    void SetLookAtTarget(Vector3 targetPos);

    void ResetLookAt();

    void SetAnimSpeed(float multiplier);
    void DoLocomotion();

    void PlayWakeUp(int wakeUpHash);
    void PlayPhaseTransition(int wakeUpHash);
    // speedMultiplier: 코어가 groggyAnimLength 기준으로 미리 계산한 배속(지상 기본값).
    // groggyDuration: 그로기 총 지속시간(초). 비주얼이 자기 클립 길이가 다를 때 배속을 직접 재계산하는 용도.
    void PlayGroggy(float speedMultiplier, float groggyDuration);
    void PlayDie();

    // 루트모션 이동 처리
    // 호스트가 루트모션 액션 시작/종료 시 캡처를 켜고 끈다.
    void SetRootMotionCapture(bool enabled);
    // 호스트가 매 틱 모아둔 루트모션 이동량을 가져간다(가져가면 0으로 리셋).
    Vector3 ConsumeRootMotionDelta();
    // 호스트가 매 틱 모아둔 루트모션 회전량을 가져간다(90도 턴 등). 회전이 없으면 Quaternion.identity.
    Quaternion ConsumeRootMotionRotation();
}