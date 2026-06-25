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

    [Tooltip("2페이즈 중 갑자기 이륙(TakeOff) 패턴을 쓸 확률 (0~100%)")]
    [Range(0f, 100f)] public float flightRandomChance = 10f;

    [Tooltip("비행 패턴 리스트(flyingPatterns) 내에서 이륙(TakeOff) 패턴이 있는 인덱스. 반드시 0번으로 하세요!")]
    public int flyingTakeOffPatternIndex = 0;

    [Tooltip("공중에 떠 있을 때만 사용할 비행 전용 패턴 리스트")]
    public List<BossPatternModule> flyingPatterns;

    [Header("루트모션 턴(회전) 설정")]
    [Tooltip("타겟이 몇 도 이상 틀어져 있을 때 제자리 턴을 할까요? (예: 60)")]
    public float turnAngleThreshold = 60f; 
    
    [Tooltip("좌측 90도 턴 (Turn90L_RM) 패턴 인덱스 (phase1/2 기준)")]
    public int turnLeftPatternIndex = -1;
    
    [Tooltip("우측 90도 턴 (Turn90R_RM) 패턴 인덱스 (phase1/2 기준)")]
    public int turnRightPatternIndex = -1;

    [Header("폴라 드래곤 전용 기믹")]
    public int phase2FrostBuffId = 2;
    public int longRangePatternIndex = -1; 
    public float longRangeThreshold = 15f;

    // 🔥 [버그 픽스 핵심] 버프가 끝나도 패턴 도중엔 추락하지 않게 잡아주는 변수
    [Networked] public NetworkBool IsFlightActive { get; set; }

    public override void Spawned()
    {
        base.Spawned();
        Debug.Log("[PolarDragonBoss] 폴라 드래곤 스폰 완료!");
    }

    protected override List<BossPatternModule> CurrentAvailablePatterns
    {
        get
        {
            // 버프 유무가 아니라, 안전장치가 걸린 IsFlightActive 기준으로 리스트 반환
            if (IsFlightActive) return flyingPatterns;
            return base.CurrentAvailablePatterns;
        }
    }

    // 🔥 [버그 픽스] 비행 버프가 켜진 동안 부모(NetworkBossCore)의 중력/바닥밀착 로직을 우회시킨다.
    protected override bool IsAirborne => IsFlightActive;

    public override void FixedUpdateNetwork()
    {
        base.FixedUpdateNetwork();
        if (!HasStateAuthority) return;

        // ==========================================
        // 🔥 [버그 픽스] 패턴 도중 추락(리스트 꼬임) 방지 로직
        // ==========================================
        bool hasBuff = HasStatus(flightBuffId);
        if (hasBuff)
        {
            IsFlightActive = true;
        }
        else if (CurrentState != BossState.ExecutingPattern)
        {
            // 버프가 끝났고, 현재 공격 중인 패턴도 완전히 끝났을 때만 비행 모드 해제!
            IsFlightActive = false;
        }

        // 2페이즈(체력 50%) 진입 시 무조건 비행 버프 켜기
        if (CurrentState == BossState.PhaseTransition && !IsGimmickActive)
        {
            ActivatePhase2Gimmick();
        }
    }

    private void ActivatePhase2Gimmick()
    {
        IsGimmickActive = true;
        ApplyStatus(flightBuffId); 
    }

    private bool IsValidPatternIndex(int index, List<BossPatternModule> targetList)
    {
        return index >= 0 && index < targetList.Count;
    }

    // ==========================================
    // [특수 AI] 턴 계산 및 고유 패턴 가로채기
    // ==========================================
    protected override int SelectPatternBasedOnRange(float currentDistance)
    {
        // 🔥 [기믹 수정] 1페이즈 금지, 2페이즈일 때만 낮은 확률로 다시 비행
        if (CurrentPhase == 2 && !IsFlightActive && Random.Range(0f, 100f) <= flightRandomChance)
        {
            if (IsValidPatternIndex(flyingTakeOffPatternIndex, flyingPatterns))
            {
                Debug.Log("[기믹] 2페이즈 비행 확률 당첨! 이륙합니다!");
                ApplyStatus(flightBuffId); // 버프를 주면 리스트가 flyingPatterns로 바뀜!
                return flyingTakeOffPatternIndex; // flyingPatterns의 TakeOff 패턴 리턴
            }
        }

        // 1. 루트모션 90도 턴 판단 (땅에 있을 때만)
        if (!IsFlightActive && AggroTarget != null)
        {
            Vector3 toTarget = (AggroTarget.transform.position - transform.position);
            toTarget.y = 0;
            Vector3 forward = transform.forward;
            forward.y = 0;

            float angle = Vector3.SignedAngle(forward.normalized, toTarget.normalized, Vector3.up);

            if (angle < -turnAngleThreshold && IsValidPatternIndex(turnLeftPatternIndex, CurrentAvailablePatterns))
            {
                return turnLeftPatternIndex;
            }
            else if (angle > turnAngleThreshold && IsValidPatternIndex(turnRightPatternIndex, CurrentAvailablePatterns))
            {
                return turnRightPatternIndex;
            }
        }

        // 2. 원거리 패턴 검사
        if (currentDistance >= longRangeThreshold && IsValidPatternIndex(longRangePatternIndex, CurrentAvailablePatterns))
        {
            return longRangePatternIndex; 
        }

        // 3. 그 외 기본 룰렛 
        return base.SelectPatternBasedOnRange(currentDistance);
    }
}