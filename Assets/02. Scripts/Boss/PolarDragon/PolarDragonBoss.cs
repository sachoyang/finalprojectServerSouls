using Fusion;
using UnityEngine;

public class PolarDragonBoss : NetworkBossCore
{
    [Header("루트모션 턴(회전) 설정")]
    [Tooltip("타겟이 몇 도 이상 틀어져 있을 때 제자리 턴을 할까요? (예: 60)")]
    public float turnAngleThreshold = 60f; 
    
    [Tooltip("좌측 90도 턴 (Turn90L_RM) 패턴 인덱스")]
    public int turnLeftPatternIndex = -1;
    
    [Tooltip("우측 90도 턴 (Turn90R_RM) 패턴 인덱스")]
    public int turnRightPatternIndex = -1;

    [Header("폴라 드래곤 전용 기믹")]
    public int phase2FrostBuffId = 2;
    public int longRangePatternIndex = 1; 
    public float longRangeThreshold = 15f;

    public override void Spawned()
    {
        base.Spawned();
        Debug.Log("[PolarDragonBoss] 폴라 드래곤 스폰 완료!");
    }

    // ==========================================
    // [특수 AI] 턴 계산 및 고유 패턴 가로채기
    // ==========================================
    protected override int SelectPatternBasedOnRange(float currentDistance)
    {
        // 1. 루트모션 90도 턴 판단 (가장 최우선 검사)
        if (AggroTarget != null)
        {
            Vector3 toTarget = (AggroTarget.transform.position - transform.position);
            toTarget.y = 0;
            Vector3 forward = transform.forward;
            forward.y = 0;

            // 내 앞면을 기준으로 타겟이 좌/우측 몇 도에 있는지 구합니다 (-180 ~ 180)
            float angle = Vector3.SignedAngle(forward.normalized, toTarget.normalized, Vector3.up);

            // 타겟이 내 왼쪽(-각도)에 크게 치우쳐 있다면 좌회전 패턴 발동!
            if (angle < -turnAngleThreshold && turnLeftPatternIndex >= 0)
            {
                Debug.Log($"[Turn] 타겟이 왼쪽({angle:F1}도)에 있음! Turn90L_RM 발동!");
                return turnLeftPatternIndex;
            }
            // 타겟이 내 오른쪽(+각도)에 크게 치우쳐 있다면 우회전 패턴 발동!
            else if (angle > turnAngleThreshold && turnRightPatternIndex >= 0)
            {
                Debug.Log($"[Turn] 타겟이 오른쪽({angle:F1}도)에 있음! Turn90R_RM 발동!");
                return turnRightPatternIndex;
            }
        }

        // 2. 턴을 할 필요가 없다면(정면을 보고 있다면), 원거리 패턴 검사
        if (currentDistance >= longRangeThreshold)
        {
            return longRangePatternIndex; 
        }

        // 3. 그것도 아니라면 기본 룰렛(근접 패턴 등) 돌리기
        return base.SelectPatternBasedOnRange(currentDistance);
    }
}