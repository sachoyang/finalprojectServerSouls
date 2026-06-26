using UnityEngine;

/// <summary>
/// 락온 가능한 대상의 루트 표시자.
/// 전투 시스템이 PlayerStats/NetworkBossCore 같은 구체 타입을 몰라도 대상을 구분할 수 있게 한다.
/// </summary>
public class LockOnTargetRoot : MonoBehaviour
{
    [SerializeField] private bool targetable = true;

    public bool IsTargetable => targetable && isActiveAndEnabled;
}
