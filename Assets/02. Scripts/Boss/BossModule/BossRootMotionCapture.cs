using UnityEngine;

/// <summary>
/// 루트모션 델타를 모아서 보스 코어(호스트)가 틱에서 가져가게 하는 브리지 컴포넌트.
/// Animator 가 붙어 있는 GameObject 에 부착하세요. (OnAnimatorMove 는 Animator 와 같은 오브젝트에서만 호출됨)
///
/// 동작:
///  - applyRootMotion = true 로 두되 OnAnimatorMove 를 직접 구현해서 기본 적용을 가로챔.
///    → transform 은 절대 직접 안 움직임(메쉬 제자리). 이동량(deltaPosition)만 누적.
///  - SetCapture(true) 일 때만 누적하고, ConsumeDelta() 로 호스트가 가져가며 0으로 리셋.
///  - 클라이언트는 SetCapture 를 호출받지 않으므로 누적하지 않음(좌표는 NetworkTransform 동기화).
/// </summary>
[RequireComponent(typeof(Animator))]
public class BossRootMotionCapture : MonoBehaviour
{
    private Animator _anim;
    private bool _capturing;
    private Vector3 _accumulated;

    private void Awake()
    {
        _anim = GetComponent<Animator>();
        // 루트모션 델타 계산을 위해 필요. OnAnimatorMove 를 구현했으므로 자동 적용은 일어나지 않음.
        _anim.applyRootMotion = true;
    }

    /// 호스트의 보스 코어가 루트모션 동작 시작/종료 시 호출. 시작/종료마다 누적값을 리셋한다.
    public void SetCapture(bool enabled)
    {
        _capturing = enabled;
        _accumulated = Vector3.zero;
    }

    /// 모아둔 이동량을 가져가고 0으로 리셋 (호스트가 매 틱 호출)
    public Vector3 ConsumeDelta()
    {
        Vector3 d = _accumulated;
        _accumulated = Vector3.zero;
        return d;
    }

    // Animator 가 루트모션을 계산할 때마다 호출(렌더 레이트). transform 은 건드리지 않고 누적만.
    private void OnAnimatorMove()
    {
        if (!_capturing) return;
        _accumulated += _anim.deltaPosition; // 월드 공간 이동량
    }
}
