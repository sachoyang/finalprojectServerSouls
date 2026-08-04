using Fusion;
using UnityEngine;

// [핵심] NetworkBossCore를 상속받습니다.
public class DragonBoss : NetworkBossCore
{
    [Header("드래곤 전용 기믹")]
    [Tooltip("플레이어가 등 뒤에 있을 때 강제로 발동할 패턴 리스트 인덱스 (예: 점프 내려찍기)")]
    public int rearAttackPatternIndex = 2; 

    [Tooltip("2페이즈 진입 시 드래곤에게 부여할 상태이상 ID (예: 1 = 광폭화)")]
    public int phase2BuffId = 1;

    // 2페이즈 진입 판정 헬퍼 (순수 로직 분리)
    private readonly DragonPhaseTransition _phaseTransition = new DragonPhaseTransition();

    // 2페이즈 기믹을 이미 발동했는지 기억하는 1회성 래치 (호스트 전용).
    // 드래곤은 아레나 기믹(조명/제단)을 쓰지 않으므로 IsGimmickActive를 래치로 쓰면 안 됩니다.
    // (제단이 없으면 그 값을 다시 false로 되돌려줄 주체가 없어 데미지 감소가 영구 적용됨)
    private bool _phase2Activated = false;

    // 부모의 스폰(Awake 같은 역할)을 그대로 가져다 씁니다.
    public override void Spawned()
    {
        base.Spawned();
        Debug.Log("[DragonBoss] 모듈형 드래곤 보스 스폰 완료!");
    }

    // ==========================================
    // [특수 AI] 드래곤만의 고유 패턴 가로채기
    // ==========================================
    protected override int SelectPatternBasedOnRange(float currentDistance)
    {
        // 1. 드래곤의 특수 기믹: 등 뒤에 플레이어가 있는지 확인!
        if (IsAnyPlayerBehind(currentDistance))
        {
            Debug.Log("[특수 기믹] 등 뒤의 플레이어 감지! 꼬리치기/점프 패턴 강제 발동!");
            // 확률 룰렛을 무시하고, 지정해둔 뒤잡기 방어 패턴(예: 인덱스 2번)을 즉시 리턴합니다.
            return rearAttackPatternIndex; 
        }

        // 2. 등 뒤에 아무도 없다면, 부모(NetworkBossCore)가 하던 대로 사거리/가중치 룰렛을 돌립니다.
        return base.SelectPatternBasedOnRange(currentDistance);
    }

    // 기존에 만드셨던 훌륭한 뒤잡기 판정 로직을 그대로 살렸습니다.
    private bool IsAnyPlayerBehind(float currentDistance)
    {
        // 최적화: 가장 가까운 타겟(AggroTarget) 하나만 검사해도 충분합니다.
        if (AggroTarget != null && currentDistance <= wakeUpRange)
        {
            Vector3 toPlayer = (AggroTarget.transform.position - transform.position);
            toPlayer.y = 0;

            Vector3 bossForward = transform.forward;
            bossForward.y = 0;

            // 내적(Dot)이 -0.1 이하이면 시야 밖(등 뒤)에 있다고 판정!
            if (Vector3.Dot(bossForward.normalized, toPlayer.normalized) < -0.1f)
            {
                return true;
            }
        }
        return false;
    }

    // FixedUpdateNetwork 안에 2페이즈 진입을 감지하는 부분을 하나 추가해줍니다.
    public override void FixedUpdateNetwork()
    {
        base.FixedUpdateNetwork();

        if (!HasStateAuthority) return;

        // 진입 판정은 헬퍼에 위임, 실제 상태 변경은 보스가 수행
        if (_phaseTransition.ShouldActivate(CurrentState, _phase2Activated))
        {
            ActivatePhase2Gimmick();
        }
    }

    // 드래곤 전용 2페이즈 기믹 발동 (아레나 조명/제단 연출 없이 버프만 부여)
    private void ActivatePhase2Gimmick()
    {
        _phase2Activated = true;

        // 드래곤 전용 버프 부여! (부모 클래스의 ApplyStatus 호출)
        ApplyStatus(phase2BuffId);
    }
}