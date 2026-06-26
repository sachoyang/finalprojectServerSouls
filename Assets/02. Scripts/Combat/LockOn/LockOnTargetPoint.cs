using UnityEngine;

/// <summary>
/// 락온이 실제로 바라볼 위치.
/// priority가 낮을수록 같은 대상 안에서 우선 선택된다. 예: 머리 0, 몸통 10.
/// </summary>
public class LockOnTargetPoint : MonoBehaviour
{
    [SerializeField] private int priority = 10;

    public int Priority => priority;
}
