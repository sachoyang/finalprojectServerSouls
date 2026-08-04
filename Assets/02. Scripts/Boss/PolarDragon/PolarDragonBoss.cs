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

    [Tooltip("2페이즈 중 갑자기 이륙(TakeOff) 패턴을 쓸 확률 (0~100%). 너무 자주 날지 않도록 낮게 유지(예: 5)")]
    [Range(0f, 100f)] public float flightRandomChance = 5f;

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

    // 폴라 드래곤만 2페이즈에서 아레나 기믹(조명 OFF + 제단 ON) 연출을 사용합니다.
    protected override bool UsesArenaGimmick => true;

    [Tooltip("지상 '원거리 전용' 패턴(스핏볼/브레스 등)의 인덱스 목록 (phase1/2 지상 리스트 기준).\n" +
             "여기 등록된 패턴은 근거리 룰렛에선 제외되고, 플레이어가 멀 때만 발동 후보가 됩니다.\n" +
             "사거리(어디서부터/어디까지 쏠지)는 각 패턴 SO의 min/maxRange로만 정합니다. (보스 인스펙터엔 별도 사거리 없음)\n" +
             "브레스 패턴을 phase2Patterns에 추가했다면 그 인덱스도 여기에 같이 넣어주세요.")]
    public List<int> longRangePatternIndices = new List<int>();

    [Tooltip("플레이어가 근거리 사거리 밖에 있을 때, '걸어서 접근' 대신 '원거리 패턴'을 쓸 확률(%).\n" +
             "33이면 원거리 : 접근(이동) ≈ 1:2 (나머지 67%는 걸어서 접근).")]
    [Range(0f, 100f)] public float longRangeChance = 33f;

    [Tooltip("원거리 추첨에서 떨어졌을 때, 다음 추첨까지 '무조건 접근(Walk)'에 전념하는 시간(초).\n" +
             "이게 없으면 매 틱 재추첨되어 사실상 원거리만 스팸하게 됨. (제자리 스팸 방지)")]
    public float approachCommitTime = 1.2f;

    [Header("후퇴 + 원거리(브레스) 패턴")]
    [Tooltip("플레이어에게서 멀어지며 원거리를 뱉는 패턴 인덱스 (phase1/2 지상 리스트 기준). 없으면 -1.\n" +
             "자동 룰렛에 안 섞이도록 이 패턴 SO의 weight는 0으로 두세요(여기 로직으로만 발동).")]
    public int retreatSpitPatternIndex = -1;

    [Tooltip("평상시(지상) 낮은 확률로 후퇴+원거리 패턴을 쓸 확률(%). 그로기에서 회복하는 순간엔 확률과 무관하게 1회 발동.")]
    [Range(0f, 100f)] public float retreatSpitChance = 5f;

    // 추첨 헬퍼 재사용 버퍼(매 호출 new 방지로 GC 절감)
    private readonly List<int> _selectBuffer = new List<int>();

    // 원거리 미당첨 시 이 시각 전까지는 재추첨 없이 무조건 접근(Walk) — 제자리 원거리 스팸 방지
    private float _approachReadyTime = 0f;

    // 그로기 회복 직후 후퇴+원거리 패턴을 1회 강제하기 위한 플래그/상태추적
    private bool _forceRetreatSpit = false;

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

    // 그로기 타이머용 피격 클립 길이.
    // 폴라드래곤은 지상(GetHit1)과 공중(FlyStationaryGetHit) 클립 길이가 달라서(예: 1.0s vs 0.833s),
    // 비행 중이면 Visual에 적어둔 공중 클립 길이를 돌려줘 루프 진입 타이밍을 정확하게 맞춘다.
    protected override float CurrentGroggyHitAnimLength =>
        (IsFlightActive && _visual is PolarDragonVisual polarVisual)
            ? polarVisual.flyingGetHitLength
            : groggyAnimLength;

    public override void FixedUpdateNetwork()
    {
        // ==========================================
        // 🔥 [순서 버그 픽스] 비행/착륙 상태 갱신을 base(=UpdateBossAI→패턴 선택) "앞"에서 처리한다.
        //    [기존 버그] 이 블록이 base 뒤에 있었다. 그래서 버프가 만료된 틱에 base의 패턴 선택이
        //    아직 갱신 안 된 옛 IsFlightActive(true)를 보고 → 착륙 대신 또 비행 패턴을 골라버렸다.
        //    결과적으로 이륙이 과하게 반복되고, 땅에 내려온 뒤 2페이즈 지상 패턴이 거의 안 나왔다.
        //    → base보다 먼저 IsFlightActive/_needsLanding을 확정해, 같은 틱의 패턴 선택이 옳은 값을 보게 한다.
        // ==========================================
        if (HasStateAuthority)
        {
            // ----- 죽으면 즉시 비행 해제 → 공중 시체 방지(땅으로 내려와서 죽음) -----
            //    비행 버프도 제거해서 IsFlightActive가 다시 켜지지 않게 한다.
            //    IsFlightActive=false 가 되면 Visual의 높이가 0으로 lerp되며 시체가 바닥으로 착지.
            if (CurrentState == BossState.Die || CurrentHP <= 0)
            {
                if (IsFlightActive)
                {
                    IsFlightActive = false;
                    RemoveStatus(flightBuffId);
                    SnapToGroundOnDeath(); // 죽는 순간 현재 위치 바로 아래 지면으로 확정 안착 (공중 시체 방지)
                }
                base.FixedUpdateNetwork(); // CurrentHP<=0이라 base는 즉시 return (안전)
                return;
            }

            // ----- 패턴 도중 추락(리스트 꼬임) 방지 + 착륙 트리거 -----
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

            // 그로기 회복 감지를 위해 base 호출 직전 상태를 기록.
            bool wasGroggyBeforeBase = (CurrentState == BossState.Groggy);

            base.FixedUpdateNetwork();

            // base가 그로기 타이머 만료 시 Idle로 바꾼다. 그로기에서 막 빠져나왔다면
            // 다음 패턴 선택에서 '후퇴+원거리' 패턴을 1회 강제하도록 예약. (지상에서만 의미 있음)
            if (wasGroggyBeforeBase && CurrentState != BossState.Groggy
                && CurrentState != BossState.Die && !IsFlightActive)
            {
                _forceRetreatSpit = true;
            }
            return;
        }

        base.FixedUpdateNetwork();
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
        List<BossPatternModule> ground = CurrentAvailablePatterns;

        // 1. 후퇴 + 원거리(브레스) 패턴
        //    그로기에서 회복하는 순간(_forceRetreatSpit) 1회, 또는 평상시 낮은 확률(retreatSpitChance)로 발동.
        //    플레이어와 거리를 벌리며 원거리를 뱉어, 근거리 패턴이 단조로운 점을 보완한다.
        if (IsValidPatternIndex(retreatSpitPatternIndex, ground)
            && (_forceRetreatSpit || Random.Range(0f, 100f) <= retreatSpitChance))
        {
            _forceRetreatSpit = false;
            return retreatSpitPatternIndex;
        }
        _forceRetreatSpit = false; // 패턴 미설정 등으로 발동 못해도 1회성 예약은 해제

        // 2. 2페이즈일 때만 낮은 확률로 다시 비행 (1페이즈 금지)
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

        // 3. 루트모션 90도 턴 판단
        if (AggroTarget != null)
        {
            Vector3 toTarget = (AggroTarget.transform.position - transform.position);
            toTarget.y = 0;
            Vector3 forward = transform.forward;
            forward.y = 0;

            float angle = Vector3.SignedAngle(forward.normalized, toTarget.normalized, Vector3.up);

            if (angle < -turnAngleThreshold && IsValidPatternIndex(turnLeftPatternIndex, ground))
            {
                return turnLeftPatternIndex;
            }
            else if (angle > turnAngleThreshold && IsValidPatternIndex(turnRightPatternIndex, ground))
            {
                return turnRightPatternIndex;
            }
        }

        // 4. 근거리(멜리) 룰렛 — 원거리 전용 패턴은 제외하고, 오직 SO의 min/maxRange로만 발동.
        //    플레이어가 걸어 들어와 근거리 사거리에 들어오면 자동으로 여기서 근거리 패턴이 나온다.
        int meleeIdx = SelectMeleeByRange(ground, currentDistance);
        if (meleeIdx >= 0) return meleeIdx;

        // 5. 근거리 사거리 밖(=플레이어가 멂) → '원거리' vs '걸어서 접근' 결정.
        //    원거리 패턴이 현재 거리에서 (SO 사거리 기준) 유효할 때만 원거리 선택지가 생긴다.
        int rangedIdx = PickRangedInRange(ground, currentDistance);
        if (rangedIdx >= 0)
        {
            // 접근 커밋 중이면 재추첨 없이 무조건 걸어서 접근 → 제자리 원거리 스팸 방지(핵심 버그 픽스).
            if (Runner.SimulationTime < _approachReadyTime) return -1;

            if (Random.Range(0f, 100f) <= longRangeChance)
                return rangedIdx; // 원거리 발동

            // 원거리 미당첨 → 잠시(approachCommitTime) 접근에 전념한 뒤 다시 판단. (이동:원거리 ≈ 2:1)
            _approachReadyTime = Runner.SimulationTime + approachCommitTime;
            return -1;
        }

        // 6. 원거리 사거리 밖 → 접근(Walk)
        return -1;
    }

    // 근거리(멜리) 가중치 룰렛. 원거리 전용 패턴(longRangePatternIndices)과
    // weight<=0(턴/착륙/이륙 등 자동선택 제외 패턴)은 후보에서 뺀다. 사거리 안 후보가 없으면 -1.
    private int SelectMeleeByRange(List<BossPatternModule> list, float dist)
    {
        _selectBuffer.Clear();
        int totalWeight = 0;
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i].weight <= 0) continue;
            if (longRangePatternIndices != null && longRangePatternIndices.Contains(i)) continue;
            if (dist >= list[i].minRange && dist <= list[i].maxRange)
            {
                _selectBuffer.Add(i);
                totalWeight += list[i].weight;
            }
        }
        if (_selectBuffer.Count == 0 || totalWeight <= 0) return -1;

        int r = Random.Range(0, totalWeight);
        int acc = 0;
        foreach (int idx in _selectBuffer)
        {
            acc += list[idx].weight;
            if (r < acc) return idx;
        }
        return _selectBuffer[0];
    }

    // 원거리 전용 패턴 중, 현재 거리가 그 패턴 SO의 min/maxRange 안에 드는 것들에서 무작위 1개. 없으면 -1.
    private int PickRangedInRange(List<BossPatternModule> list, float dist)
    {
        if (longRangePatternIndices == null || longRangePatternIndices.Count == 0) return -1;

        _selectBuffer.Clear();
        for (int i = 0; i < longRangePatternIndices.Count; i++)
        {
            int idx = longRangePatternIndices[i];
            if (!IsValidPatternIndex(idx, list)) continue;
            if (dist >= list[idx].minRange && dist <= list[idx].maxRange)
                _selectBuffer.Add(idx);
        }
        if (_selectBuffer.Count == 0) return -1;
        return _selectBuffer[Random.Range(0, _selectBuffer.Count)];
    }
}