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

    [Header("착륙(Landing) 기믹 설정")]
    [Tooltip("착륙 패턴 인덱스 (지상 리스트인 phase1/phase2 기준)")]
    public int landingPatternIndex = -1;
    // 다음 룰렛에서 무조건 착륙을 고르도록 지시하는 비밀 플래그
    private bool _needsLanding = false;


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
        // 🔥 [버그 픽스] 죽으면 즉시 비행 해제 → 공중 시체 방지(땅으로 내려와서 죽음)
        //    비행 버프도 제거해서 IsFlightActive가 다시 켜지지 않게 한다.
        //    IsFlightActive=false 가 되면 Visual의 높이가 0으로 lerp되며 시체가 바닥으로 착지.
        // ==========================================
        if (CurrentState == BossState.Die || CurrentHP <= 0)
        {
            if (IsFlightActive)
            {
                IsFlightActive = false;
                RemoveStatus(flightBuffId);
                SnapToGroundOnDeath(); // 죽는 순간 현재 위치 바로 아래 지면으로 확정 안착 (공중 시체 방지)
            }
            return;
        }

        // ==========================================
        // 🔥 [버그 픽스] 패턴 도중 추락(리스트 꼬임) 방지 로직
        // ==========================================
        bool hasBuff = HasStatus(flightBuffId);
        if (hasBuff)
        {
            IsFlightActive = true;
            _needsLanding = false;
        }
        else if (IsFlightActive && CurrentState != BossState.ExecutingPattern)
        {
            // 버프가 끝났고, 현재 공격 중인 패턴도 완전히 끝났을 때만 비행 모드 해제!
            IsFlightActive = false; // 공중 판정 및 중력 무시 해제 (바닥으로 스르륵 내려옴)
            _needsLanding = true;   // 다음 룰렛에서 무조건 착륙 강제 발동!
        }

        // 2페이즈(체력 50%) 진입 시 무조건 비행 버프 켜기
        if (CurrentState == BossState.PhaseTransition && !IsGimmickActive)
        {
            ActivatePhase2Gimmick();
        }
    }

    // 죽는 순간 보스 루트를 바로 아래 지면으로 내려 공중 시체를 방지.
    // (비행 중엔 FixedUpdateNetwork가 사망 후 조기 return하여 StickToGround가 더 못 돌기 때문에 여기서 1회 확정)
    private void SnapToGroundOnDeath()
    {
        Vector3 start = transform.position + Vector3.up * 2f;
        // 높이 띄워 날다 죽어도 한참 아래 지면까지 닿도록 충분히 긴 레이 사용
        if (Physics.Raycast(start, Vector3.down, out RaycastHit hit, 300f, groundLayerMask))
        {
            Vector3 p = transform.position;
            p.y = hit.point.y;
            transform.position = p;
        }
    }

    private void ActivatePhase2Gimmick()
    {
        IsGimmickActive = true;
        ApplyStatus(flightBuffId);
        IsFlightActive = true; // 2페이즈 진입 이륙도 같은 틱에 즉시 비행 판정
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
        // 🔥 0. 착륙 검사 (가장 최우선!)
        if (_needsLanding)
        {
            _needsLanding = false; // 한 번 골랐으니 끔
            
            // 주의: IsFlightActive가 꺼졌으므로 여기서 CurrentAvailablePatterns는 지상 리스트입니다!
            if (IsValidPatternIndex(landingPatternIndex, CurrentAvailablePatterns))
            {
                Debug.Log("[기믹] 비행 버프 만료! FlyStationaryToLanding 착륙 패턴을 실행합니다.");
                return landingPatternIndex;
            }
        }
        
        // ==========================================
        // 🔥 [비행 중] 특수 강제 패턴(재이륙/턴/롱레인지) 전부 금지!
        //    오직 SO의 사거리(min/maxRange)를 지키는 룰렛만 사용하고, 이륙(TakeOff)은 후보에서 제외한다.
        //    → 사거리 밖이면 후보 0개 → -1 반환 → 비행으로 접근(Walk)한 뒤, 사거리 안에서만 브레스/스핏볼 발동.
        //    (기존엔 longRange 강제 경로가 SO maxRange를 무시하고 멀리서도 쏘던 버그를 제거)
        // ==========================================
        if (IsFlightActive)
        {
            return _patternSelector.SelectByRange(flyingPatterns, currentDistance, flyingTakeOffPatternIndex);
        }

        // ==========================================
        // [지상 전용] 아래는 모두 땅에 있을 때만 동작
        // ==========================================

        // 1. 2페이즈일 때만 낮은 확률로 다시 비행 (1페이즈 금지)
        if (CurrentPhase == 2 && Random.Range(0f, 100f) <= flightRandomChance)
        {
            if (IsValidPatternIndex(flyingTakeOffPatternIndex, flyingPatterns))
            {
                Debug.Log("[기믹] 2페이즈 비행 확률 당첨! 이륙합니다!");
                ApplyStatus(flightBuffId);
                // 🔥 같은 틱에 즉시 켜야 StartPattern이 이 틱부터 flyingPatterns를 인덱싱한다.
                //    (IsFlightActive를 FixedUpdateNetwork 끝에서만 갱신하면 한 틱 동안 지상 리스트를 봄)
                IsFlightActive = true;
                return flyingTakeOffPatternIndex; // flyingPatterns의 TakeOff 패턴 리턴
            }
        }

        // 2. 루트모션 90도 턴 판단
        if (AggroTarget != null)
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

        // 3. 원거리 패턴 검사 (지상 한정)
        if (currentDistance >= longRangeThreshold && IsValidPatternIndex(longRangePatternIndex, CurrentAvailablePatterns))
        {
            return longRangePatternIndex;
        }

        // 4. 그 외 기본 룰렛
        return base.SelectPatternBasedOnRange(currentDistance);
    }
}