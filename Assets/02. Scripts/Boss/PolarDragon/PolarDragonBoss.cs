using Fusion;
using UnityEngine;
using System.Collections.Generic;

public class PolarDragonBoss : NetworkBossCore
{
    [Header("비행(Flying) 기믹 설정")]
    [Tooltip("비행 상태를 의미하는 상태이상 ID (예: 3)")]
    public int flightBuffId = 3; 
    
    [Tooltip("비행 시 Y축으로 올라갈 높이")]
    public float flightHeight = 3.5f; 

    [Tooltip("갑자기 이륙(TakeOff) 패턴을 쓸 확률 (0~100%)")]
    [Range(0f, 100f)] public float flightRandomChance = 10f;

    [Tooltip("이륙(TakeOff) 패턴이 들어있는 인덱스 (phase1Patterns 기준)")]
    public int takeOffPatternIndex = 0;

    [Tooltip("공중에 떠 있을 때만 사용할 비행 전용 패턴 리스트")]
    public List<BossPatternModule> flyingPatterns;

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
    // [핵심] 버프 상태에 따른 패턴 리스트 스위칭
    // ==========================================
    protected override List<BossPatternModule> CurrentAvailablePatterns 
    {
        get
        {
            // 비행 버프가 켜져 있다면? 지상 패턴 무시하고 비행 전용 패턴 리스트 반환!
            if (HasStatus(flightBuffId)) return flyingPatterns;
            
            // 땅에 있다면 기존 규칙(1, 2페이즈) 따름
            return base.CurrentAvailablePatterns;
        }
    }

    public override void FixedUpdateNetwork()
    {
        base.FixedUpdateNetwork();
        if (!HasStateAuthority) return;

        // 2페이즈(체력 50%) 진입 시 무조건 비행 버프 켜기 (오크 어쌔신 방식)
        if (CurrentState == BossState.PhaseTransition && !IsGimmickActive)
        {
            ActivatePhase2Gimmick();
        }
    }

    private void ActivatePhase2Gimmick()
    {
        IsGimmickActive = true;
        
        // 2페이즈 진입 시 무조건 비행 버프(예: 20초) 부여!
        // (StatusDatabase SO에 3번 ID를 '비행'으로 만들고 지속시간 세팅 필요)
        ApplyStatus(flightBuffId); 
    }

    // ==========================================
    // [헬퍼] 인덱스가 현재 사용 가능한 리스트 내에 있는지 검사합니다.
    // ==========================================
    private bool IsValidPatternIndex(int index)
    {
        return index >= 0 && index < CurrentAvailablePatterns.Count;
    }

    // ==========================================
    // [특수 AI] 턴 계산 및 고유 패턴 가로채기
    // ==========================================
    protected override int SelectPatternBasedOnRange(float currentDistance)
    {
        if (!HasStatus(flightBuffId) && Random.Range(0f, 100f) <= flightRandomChance)
        {
            if (IsValidPatternIndex(takeOffPatternIndex))
            {
                Debug.Log("[기믹] 확률 당첨! 드래곤이 비행을 시작합니다!");
                ApplyStatus(flightBuffId);
                return takeOffPatternIndex; 
            }
        }

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