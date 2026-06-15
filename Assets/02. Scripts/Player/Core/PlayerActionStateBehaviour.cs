using UnityEngine;

public enum PlayerActionLockType
{
    // Animator State가 어떤 종류의 액션락을 소유하는지 구분한다.
    // State 이름 문자열에 의존하지 않기 때문에 slash2 같은 이름이 바뀌어도 타입만 맞으면 락이 유지된다.
    None,
    Attack,
    Parry,
    Impact,
    Roll,
    Jump,
    Skill
}

public class PlayerActionStateBehaviour : StateMachineBehaviour
{
    // 이 State가 시작할 때 걸고, 종료할 때 풀려고 시도할 액션락 타입.
    // 현재 플레이어의 락 타입과 다를 경우 NetworkPlayerController가 해제 요청을 무시한다.
    [SerializeField] private PlayerActionLockType actionLockType = PlayerActionLockType.None;

    // 기본 공격 선입력용 옵션.
    // slash2/slash3처럼 다음 기본 공격 입력을 허용해야 하는 State에서만 켠다.
    [SerializeField] private bool opensComboInput;
    // 입력 가능 창은 클립 초가 아니라 Animator State 진행률로 연다.
    // 애니메이션 속도를 바꿔도 같은 포즈 구간에서 콤보 입력을 받기 위한 값이다.
    [SerializeField, Range(0f, 1f)] private float comboInputOpenNormalizedTime = 0.72f;
    [SerializeField] private bool enablesParryGuard;

    private bool _comboInputOpened;

    public void Configure(PlayerActionLockType lockType, bool shouldOpenComboInput, float openNormalizedTime, bool shouldEnableParryGuard = false)
    {
        // Editor Setup Tool에서 State를 만들거나 갱신할 때 호출된다.
        // 수동 입력 대신 도구가 타입/콤보 입력 시점을 일관되게 세팅하게 하기 위한 진입점이다.
        actionLockType = lockType;
        opensComboInput = shouldOpenComboInput;
        comboInputOpenNormalizedTime = Mathf.Clamp01(openNormalizedTime);
        enablesParryGuard = shouldEnableParryGuard;
    }

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        // State에 실제로 진입한 순간 액션락을 켠다.
        // StartAction에서 이미 잠갔더라도 Animator 기준 상태 진입을 다시 확인하는 보강 역할을 한다.
        _comboInputOpened = false;
        NetworkPlayerController controller = GetController(animator);
        controller?.BeginActionAnimation(actionLockType);
        if (enablesParryGuard)
        {
            controller?.SetParryGuardActive(true);
        }
    }

    public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        // 콤보 입력은 애니메이션 초반에는 막고, 지정한 State 진행률 이후에 한 번만 연다.
        // 실제 입력 저장/실행은 NetworkPlayerController가 담당하고, 이 Behaviour는 "지금부터 받아도 된다"는 신호만 보낸다.
        if (!opensComboInput || _comboInputOpened)
        {
            return;
        }

        if (GetStateNormalizedTime(stateInfo) < comboInputOpenNormalizedTime)
        {
            return;
        }

        _comboInputOpened = true;
        GetController(animator)?.OpenComboInputWindow();
    }

    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        // State가 끝났다고 무조건 락을 풀면, 피격으로 넘어간 직후 이전 공격 State가 새 Impact 락을 풀어버릴 수 있다.
        // 그래서 자신의 actionLockType을 넘기고, 컨트롤러가 현재 락 타입과 맞을 때만 해제한다.
        _comboInputOpened = false;
        NetworkPlayerController controller = GetController(animator);
        if (enablesParryGuard)
        {
            controller?.SetParryGuardActive(false);
        }

        controller?.EndActionAnimation(actionLockType);
    }

    private static NetworkPlayerController GetController(Animator animator)
    {
        return animator != null ? animator.GetComponentInParent<NetworkPlayerController>() : null;
    }

    private static float GetStateNormalizedTime(AnimatorStateInfo stateInfo)
    {
        // 루프 State는 normalizedTime이 1을 넘어 계속 증가하므로 소수부만 사용한다.
        // 일반 액션 State는 0~1로 고정해 진행률로 사용한다.
        return stateInfo.loop
            ? stateInfo.normalizedTime - Mathf.Floor(stateInfo.normalizedTime)
            : Mathf.Clamp01(stateInfo.normalizedTime);
    }
}
