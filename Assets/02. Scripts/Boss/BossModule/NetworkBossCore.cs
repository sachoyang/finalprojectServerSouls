using Fusion;
using UnityEngine;
using System.Collections.Generic;
using Unity.VisualScripting;

// 보스의 현재 상태를 아주 심플하게 통제하기 위한 열거형
public enum BossState
{
    Sleep,
    WakeUp,           // [여기에 새로 추가!] 깨어날 때 포효하는 상태
    PhaseTransition,  // [추가됨!] 2페이즈로 넘어가는 포효/무적 연출 상태
    Idle,
    Walk,
    ExecutingPattern, // SO 패턴을 실행 중일 때의 상태
    Die,
    Groggy,
}

// 퓨전 네트워크에서 리스트로 동기화하기 위한 구조체
public struct BossStatusData : INetworkStruct
{
    public int StatusId;      // BossStatusType을 int로 저장
    public float EndTime;     // 언제 끝나는지 (Runner.SimulationTime 기준)
    public float Power;       // 효과 수치 (예: 1.5면 1.5배 데미지)
}

// UI 파트에서 이 구조체 리스트를 통째로 가져가서 화면에 그릴 겁니다.
public struct ActiveStatusUIInfo
{
    public StatusEffectData Data; // SO 원본 (아이콘, 이름 등)
    public float RemainingTime;   // 남은 시간
    public float Power; // 배율 수치(임시)
}

public class NetworkBossCore : NetworkBehaviour
{
    [Tooltip("UI 체력바에 표시될 보스의 이름")]
    [Networked, Capacity(32)] public string bossName { get; set; }

    [Header("기본 설정")]
    public float moveSpeed = 2.5f;
    public float rotationSpeed = 5.0f;
    public float wakeUpRange = 10.0f;
    public float aggroRefreshTime = 5.0f;
    public float patternCooldown = 2.0f;

    [Header("상태이상 도감 (SO 리스트)")]
    [Tooltip("기획자가 만든 StatusEffectData SO들을 여기에 모두 넣어주세요.")]
    public List<StatusEffectData> statusDatabase;

    [Header("기상(Wake Up) 설정")]
    [Tooltip("잠에서 깰 때 재생할 애니메이션 이름 (예: Scream)")]
    public string wakeUpAnimName = "Scream";
    [Tooltip("포효 애니메이션의 지속 시간 (초)")]
    public float wakeUpDuration = 2.8f;
    private int _wakeUpAnimHash; // 최적화용 해시 변수

    [Header("그로기(Stagger) 설정")]
    public float maxGroggy = 40f;
    public float groggyDuration = 3.0f; // 그로기 지속 시간
    [Tooltip("getHit 애니메이션의 원래 길이 (초 단위)")]
    public float groggyAnimLength = 1.4f;
    [Networked] public float CurrentGroggy { get; set; }


    // 최대 8개의 상태이상을 동시에 가질 수 있는 네트워크 배열 (UI에서 이걸 읽어가면 됩니다!)
    [Networked, Capacity(8)]
    public NetworkArray<BossStatusData> ActiveStatuses { get; }

    [Header("체력 설정")]
    [Tooltip("에디터에서 기획자가 설정하는 1층 기준 기본 체력")]
    public float baseMaxHP = 100000f;

    [Networked] public float maxHP { get; set; }
    [Networked] public float CurrentHP { get; set; }

    [Header("벽 충돌 설정 (미끄러짐)")]
    public LayerMask wallLayerMask;
    public float bodyRadius = 2.0f;
    public float castHeightOffset = 2.0f;

    [Header("지형(Y축) 설정")]
    [Tooltip("바닥(지형)으로 인식할 레이어 (ground Layer)")]
    public LayerMask groundLayerMask;
    [Tooltip("보스가 타고 올라갈 수 있는 최대 계단/경사로 높이")]
    public float stepHeight = 0.5f;
    [Tooltip("보스가 공중에 떴을 때 떨어지는 중력 속도")]
    public float gravitySpeed = 15.0f;

    [Header("패턴 데이터 (ScriptableObject 리스트)")]
    [Tooltip("1페이즈에서 사용할 패턴 모듈들을 넣어주세요.")]
    public List<BossPatternModule> phase1Patterns;

    [Tooltip("2페이즈(광폭화 등)에서 사용할 패턴 모듈들을 넣어주세요.")]
    public List<BossPatternModule> phase2Patterns;

    // 현재 맵에서 허락된 최대 페이즈 (매니저가 주입해 줄 예정)
    [Networked] public int AllowedMaxPhase { get; set; } = 1;

    // 스테이지 난이도에 따른 데미지 뻥튀기 계수
    [Networked] public float DamageMultiplier { get; set; } = 1.0f;

    // 현재 진행 중인 페이즈 상태
    [Networked] public int CurrentPhase { get; set; } = 1;

    // 기존의 CurrentAvailablePatterns를 프로퍼티로 변경하여, 현재 페이즈에 맞는 리스트를 자동으로 내뱉게 합니다.
    protected List<BossPatternModule> CurrentAvailablePatterns => (CurrentPhase == 2) ? phase2Patterns : phase1Patterns;

    // ==========================================
    // [네트워크 동기화 변수들]
    // ==========================================
    [Networked] public BossState CurrentState { get; set; }
    [Networked] public NetworkObject AggroTarget { get; set; }

    // 패턴 실행용 네트워크 변수
    [Networked] public int CurrentPatternIndex { get; set; } = -1;
    [Networked] public int CurrentStepIndex { get; set; } = -1;
    [Networked] private float PreviousCurveValue { get; set; } // 프레임 간 이동량 계산용

    [Networked] public TickTimer StateTimer { get; set; }
    [Networked] public TickTimer AttackCooldown { get; set; }
    [Networked] private TickTimer AggroTimer { get; set; }

    // 딜미터기 장부
    protected Dictionary<NetworkObject, float> _damageTracker = new Dictionary<NetworkObject, float>();

    // 시각화 인터페이스 (자식 클래스나 Awake에서 할당)
    protected IBossVisual _visual;
    private int _lastPatternIndex = -1;
    private int _lastStepIndex = -1;

    // 방금 전 프레임의 상태를 기억하여 중복 실행을 막는 플래그
    private BossState _lastState = (BossState)(-1);

    // 기믹 설정
    [Networked] public NetworkBool IsGimmickActive { get; set; }
    [Tooltip("기믹 중에 들어가는 데미지 배율 (0.1 = 10%만 데미지 들어감)")]
    public float gimmickDamageReduction = 0.3f;
    private bool _localGimmickActive = false;

    public override void Spawned()
    {
        _visual = GetComponentInChildren<IBossVisual>();
        _wakeUpAnimHash = Animator.StringToHash(wakeUpAnimName);

        if (HasStateAuthority)
        {
            CurrentHP = maxHP;
            ChangeState(BossState.Sleep);
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority || CurrentHP <= 0) return;

        // 매 프레임 만료된 버프/디버프 청소
        ProcessStatuses();

        // 그로기 상태일 때는 타이머만 체크하고 아무것도 안 함!
        if (CurrentState == BossState.Groggy)
        {
            if (StateTimer.Expired(Runner))
            {
                CurrentGroggy = 0; // 그로기 수치 초기화
                AttackCooldown = TickTimer.CreateFromSeconds(Runner, patternCooldown);
                ChangeState(BossState.Idle);
            }
            return;
        }

        // 10초 딜미터기 정산
        if (CurrentState != BossState.Sleep && AggroTimer.Expired(Runner))
        {
            UpdateAggroByDamage();
        }

        UpdateBossAI();

        // 걷기 중일 때 타겟을 향해 이동
        if (CurrentState == BossState.Walk && AggroTarget != null)
        {
            MoveTowardsTarget();
        }

        // 패턴 실행 중일 때의 그래프 기반 이동 (Curve-Driven Movement) 연산
        if (CurrentState == BossState.ExecutingPattern)
        {
            ProcessPatternMovement();
        }
    }

    // ==========================================
    // [그래프 기반 강제 이동 연산]
    // ==========================================
    private void ProcessPatternMovement()
    {
        if (CurrentPatternIndex < 0 || CurrentPatternIndex >= CurrentAvailablePatterns.Count) return;

        BossPatternModule pattern = CurrentAvailablePatterns[CurrentPatternIndex];
        BossActionModule action = pattern.GetAction(CurrentStepIndex);

        // 현재 애니메이션이 몇 퍼센트(0~1) 진행되었는지 계산
        float duration = action.duration;
        float remaining = StateTimer.RemainingTime(Runner) ?? 0f;
        float progress = Mathf.Clamp01(1f - (remaining / duration));

        // 꺾은선 그래프(Curve)에서 현재 퍼센트에 해당하는 이동 완료 비율(Y값) 추출
        float currentCurveVal = action.moveCurve.Evaluate(progress);
        float deltaCurve = currentCurveVal - PreviousCurveValue; // 이번 프레임에 움직여야 할 비율
        PreviousCurveValue = currentCurveVal;

        if (deltaCurve > 0.001f && action.moveOffset != Vector3.zero)
        {
            // 목표 이동량(Vector3)에 이번 프레임 비율을 곱해 실제 이동 거리 도출
            Vector3 worldMoveOffset = transform.TransformDirection(action.moveOffset);
            Vector3 frameDisplacement = worldMoveOffset * deltaCurve;

            // 벽 미끄러짐 함수를 호출하여 안전하게 이동
            PerformWallSlideDisplacement(frameDisplacement);
        }
    }

    // ==========================================
    // [보스 AI 흐름 제어]
    // ==========================================
    private void UpdateBossAI()
    {
        // 1. 수면 상태 처리
        if (CurrentState == BossState.Sleep)
        {
            FindClosestTarget();
            if (AggroTarget != null && Vector3.Distance(transform.position, AggroTarget.transform.position) <= wakeUpRange)
            {
                ChangeState(BossState.WakeUp);
                StateTimer = TickTimer.CreateFromSeconds(Runner, wakeUpDuration);
                AggroTimer = TickTimer.CreateFromSeconds(Runner, aggroRefreshTime);


                // ==========================================
                // 전투 시작 시 방 잠금 (디버그 난입 방지 및 안전장치)
                // ==========================================
                if (Runner.SessionInfo != null && Runner.SessionInfo.IsOpen)
                {
                    Runner.SessionInfo.IsVisible = false;
                    Runner.SessionInfo.IsOpen = false;
                    Debug.Log("[네트워크] 보스가 깨어났습니다! 전투 중 난입을 막기 위해 방 문을 잠급니다.");
                }
            }
            return;
        }

        // 2. 기상(포효) 및 페이즈 변신 (연출 중일 땐 이동/타겟팅 무시)
        if (CurrentState == BossState.WakeUp)
        {
            if (StateTimer.Expired(Runner))
            {
                AttackCooldown = TickTimer.CreateFromSeconds(Runner, patternCooldown);
                ChangeState(BossState.Idle);
                AggroTimer = TickTimer.CreateFromSeconds(Runner, aggroRefreshTime);
            }
            return; 
        }

        if (CurrentState == BossState.PhaseTransition)
        {
            if (StateTimer.Expired(Runner))
            {
                AttackCooldown = TickTimer.CreateFromSeconds(Runner, patternCooldown);
                ChangeState(BossState.Idle);
            }
            return; 
        }

        // 3. 🔥 [수정 핵심 1] 패턴 진행 중일 때 타겟이 죽더라도 하던 공격은 끝내도록 순서 변경!
        if (CurrentState == BossState.ExecutingPattern)
        {
            if (StateTimer.Expired(Runner))
            {
                BossPatternModule pattern = CurrentAvailablePatterns[CurrentPatternIndex];
                CurrentStepIndex++;

                if (CurrentStepIndex < pattern.ActionCount)
                {
                    ExecuteCurrentPatternStep(pattern.GetAction(CurrentStepIndex));
                }
                else
                {
                    CurrentPatternIndex = -1;
                    CurrentStepIndex = -1;
                    AttackCooldown = TickTimer.CreateFromSeconds(Runner, patternCooldown);
                    ChangeState(BossState.Idle);
                }
            }
            return; // 공격 중에는 타겟이 죽거나 도망가도 하던 행동에 집중함
        }

        // ==========================================
        // 4.타겟 유효성 검사 (평상시)
        // ==========================================
        // 현재 잡고 있는 타겟이 죽어서 태그가 바뀌었다면 즉시 어그로 해제!
        if (AggroTarget != null && !AggroTarget.gameObject.CompareTag("Player"))
        {
            AggroTarget = null;
        }

        // 타겟이 없으면 가장 가까운 '살아있는' 플레이어 찾기
        if (AggroTarget == null) FindClosestTarget();

        // 맵에 살아있는 플레이어가 단 한 명도 없다면?
        if (AggroTarget == null) 
        {
            // 걷기를 멈추고 제자리에 서서 숨 고르기 (제자리 걷기 버그 완벽 해결!)
            if (CurrentState == BossState.Walk) 
            {
                ChangeState(BossState.Idle);
            }
            return;
        }

        // 5. 타겟을 향해 회전
        Vector3 dir = (AggroTarget.transform.position - transform.position).normalized;
        dir.y = 0;
        if (dir != Vector3.zero)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), rotationSpeed * Runner.DeltaTime);

        // 6. 패턴 시작 또는 걷기 결정
        if (AttackCooldown.ExpiredOrNotRunning(Runner))
        {
            float dist = Vector3.Distance(transform.position, AggroTarget.transform.position);
            int selectedPatternIdx = SelectPatternBasedOnRange(dist);

            if (selectedPatternIdx >= 0)
            {
                StartPattern(selectedPatternIdx);
            }
            else
            {
                if (CurrentState != BossState.Walk) ChangeState(BossState.Walk);
            }
        }
        else
        {
            if (CurrentState != BossState.Idle) ChangeState(BossState.Idle);
        }
    }
    // ==========================================
    // [패턴 룰렛 시스템]
    // ==========================================
    protected virtual int SelectPatternBasedOnRange(float currentDistance)
    {
        List<int> validPatternIndices = new List<int>();
        int totalWeight = 0;

        // 1. 현재 사거리에 발동 가능한 패턴만 추려냅니다.
        for (int i = 0; i < CurrentAvailablePatterns.Count; i++)
        {
            var pattern = CurrentAvailablePatterns[i];
            if (currentDistance >= pattern.minRange && currentDistance <= pattern.maxRange)
            {
                validPatternIndices.Add(i);
                totalWeight += pattern.weight;
            }
        }

        if (validPatternIndices.Count == 0) return -1;

        // 2. 가중치(Weight) 기반으로 룰렛을 돌려 패턴을 선택합니다.
        int randomVal = UnityEngine.Random.Range(0, totalWeight);
        int accumulatedWeight = 0;

        foreach (int idx in validPatternIndices)
        {
            accumulatedWeight += CurrentAvailablePatterns[idx].weight;
            if (randomVal <= accumulatedWeight)
                return idx;
        }

        return validPatternIndices[0];
    }

    private void StartPattern(int patternIndex)
    {
        CurrentState = BossState.ExecutingPattern;
        CurrentPatternIndex = patternIndex;
        CurrentStepIndex = 0;

        BossPatternModule pattern = CurrentAvailablePatterns[CurrentPatternIndex];
        ExecuteCurrentPatternStep(pattern.GetAction(0));
    }

    private void ExecuteCurrentPatternStep(BossActionModule action)
    {
        StateTimer = TickTimer.CreateFromSeconds(Runner, action.duration);
        PreviousCurveValue = 0f; // 이동량 계산 초기화
    }

    // ==========================================
    // [상태 관리 및 비주얼 동기화]
    // ==========================================
    protected void ChangeState(BossState newState)
    {
        CurrentState = newState;
    }

    public override void Render()
    {
        if (_visual == null) return;

        // 1. 패턴 실행 중일 때 (공격 등)
        if (CurrentState == BossState.ExecutingPattern && CurrentPatternIndex >= 0 && CurrentStepIndex >= 0)
        {
            if (_lastPatternIndex != CurrentPatternIndex || _lastStepIndex != CurrentStepIndex)
            {
                BossActionModule action = CurrentAvailablePatterns[CurrentPatternIndex].GetAction(CurrentStepIndex);

                // 해시값으로 애니메이션 실행
                // 해시값이 비어있으면 실시간으로 문자열을 찾아 해시로 변환하는 안전장치
                int targetHash = action.animationHash != 0 ? action.animationHash : Animator.StringToHash(action.animationStateName);
                _visual.PlayAction(targetHash);

                // [수정됨: 배속 버그 해결] (원본 클립 길이 / 기획자가 설정한 시간)으로 정확한 배속 도출
                if (action.animationClip != null && action.duration > 0f)
                {
                    float speedMult = action.animationClip.length / action.duration;
                    _visual.SetAnimSpeed(speedMult);
                }
                else
                {
                    _visual.SetAnimSpeed(1.0f); // 클립이 없으면 기본 속도
                }

                _lastPatternIndex = CurrentPatternIndex;
                _lastStepIndex = CurrentStepIndex;
                _lastState = CurrentState; // 상태 갱신
            }
        }
        else
        {
            // 2. 패턴 중이 아닐 때 (지속 상태 초기화)
            _lastPatternIndex = -1;
            _lastStepIndex = -1;

            // 걷기 블렌드 트리 속도 및 수면 상태 적용은 매 프레임 유지
            _visual.SetSpeed(CurrentState == BossState.Walk ? 1.0f : 0.0f);
            //_visual.SetSleep(CurrentState == BossState.Sleep || CurrentState == BossState.Groggy);

            // 상태가 '방금 딱 바뀌었을 때만' 1회 호출
            if (_lastState != CurrentState)
            {
                _visual.SetAnimSpeed(1.0f); // 패턴이 끝났으니 배속을 무조건 1.0(정상)으로 복구

                if (CurrentState == BossState.Groggy)
                {
                    // 그로기 진입 시 배속으로 느리게 재생!
                    float speedMult = groggyAnimLength / groggyDuration;
                    _visual.SetAnimSpeed(speedMult);

                    _visual.PlayAction(Animator.StringToHash("getHit"));
                }
                else if (CurrentState == BossState.Idle || CurrentState == BossState.Walk)
                {
                    _visual.DoLocomotion(); // 딱 한 번만 CrossFade 발동!
                }
                else if (CurrentState == BossState.Die)
                {
                    // 죽음 상태 진입 시 사망 애니메이션 재생
                    _visual.PlayAction(Animator.StringToHash("die"));
                }
                else if (CurrentState == BossState.WakeUp || CurrentState == BossState.PhaseTransition)
                {
                    // 변신할 때도 임시로 기상 포효(Scream) 애니메이션을 재활용합니다!
                    _visual.PlayAction(_wakeUpAnimHash);
                }

                _lastState = CurrentState;
            }
        }

        // ==========================================
        // 기믹 시각 효과(불 끄기/켜기) 클라이언트 동기화!
        // ==========================================
        // 서버에서 IsGimmickActive를 true로 바꿨는데 내 화면은 아직 안 켜졌다면?
        if (IsGimmickActive && !_localGimmickActive)
        {
            _localGimmickActive = true;
            if (DragonArenaGimmick.Instance != null)
                DragonArenaGimmick.Instance.PlayGimmickVisuals();
        }
        // 서버에서 IsGimmickActive를 false로 바꿨는데 내 화면은 아직 켜져있다면?
        else if (!IsGimmickActive && _localGimmickActive)
        {
            _localGimmickActive = false;
            if (DragonArenaGimmick.Instance != null)
                DragonArenaGimmick.Instance.StopGimmickVisuals();
        }
    }

    // ==========================================
    // 5초마다 호출되는 딜미터기 정산 로직
    // ==========================================
    private void UpdateAggroByDamage()
    {
        NetworkObject topDPSPlayer = null;
        float maxDamage = 0f;

        // 1. 장부를 훑어서 지난 5초 동안 가장 딜을 많이 넣은 사람을 찾음
        foreach (var kvp in _damageTracker)
        {
            if (kvp.Key != null && kvp.Key.gameObject.CompareTag("Player") && kvp.Value > maxDamage)
            {
                maxDamage = kvp.Value;
                topDPSPlayer = kvp.Key;
            }
        }

        // 2. 5초 동안 딜을 넣은 사람이 1명이라도 있으면 그 사람으로 타겟 고정!
        if (topDPSPlayer != null && maxDamage > 0)
        {
            AggroTarget = topDPSPlayer;
            Debug.Log($"[Aggro] 어그로 갱신! 대상: {topDPSPlayer.Id} (5초 누적 딜: {maxDamage})");
        }
        else
        {
            // 3. 5초 동안 아무도 때리지 않았다면(도망만 다녔다면) 제일 가까운 사람으로 타겟 갱신
            FindClosestTarget();
            Debug.Log("[Aggro] 5초간 누적 딜량 없음. 가장 가까운 대상으로 갱신.");
        }

        // 4. 다음 5초를 위해 장부 초기화 및 타이머 재시작
        _damageTracker.Clear();
        AggroTimer = TickTimer.CreateFromSeconds(Runner, aggroRefreshTime);
    }

    // ==========================================
    // 데미지와 함께 "누가 때렸는지(attacker)" 기록
    // ==========================================
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_TakeDamage(float damage, float groggyDamage = 10f, NetworkObject attacker = null)
    {
        // 1. 이미 죽어있는 상태면 때려도 무시 (중복 실행 방지)
        if (CurrentState == BossState.Die || CurrentHP <= 0) return;

        // 2. 방깎 디버프 등을 계산하여 최종 데미지 산출 후 체력 차감
        float finalDamage = damage * GetIncomingDamageMultiplier();
        CurrentHP -= finalDamage;

        // 3. 데미지를 입고 나서 체력이 0 이하가 되었는지 확인!!
        if (CurrentHP <= 0)
        {
            ExecuteDeath();
            return; // 죽었으면 아래 코드(그로기, 변신 등) 실행 안 하고 함수 종료!
        }

        // 4. 그로기 수치 누적 (무적 연출 중이 아닐 때만)
        if (CurrentState != BossState.PhaseTransition && CurrentState != BossState.Groggy)
        {
            CurrentGroggy += groggyDamage;
            if (CurrentGroggy >= maxGroggy)
            {
                Debug.Log("[보스] 그로기(Stagger) 발생!");
                // 그로기 발동 시 실행 중이던 패턴 강제 취소!
                CurrentPatternIndex = -1;
                CurrentStepIndex = -1;

                StateTimer = TickTimer.CreateFromSeconds(Runner, groggyDuration);
                ChangeState(BossState.Groggy);
            }
        }

        // 5. 체력이 50% 이하인데 아직 1페이즈고, 매니저가 2페이즈를 허락(5층 이상)했다면?!
        if (CurrentPhase == 1 && AllowedMaxPhase >= 2 && CurrentHP <= (maxHP * 0.5f))
        {
            CurrentPhase = 2; // 즉시 2페이즈 패턴 리스트로 교체!
            ChangeState(BossState.PhaseTransition);

            // 3초 동안 무적 & 포효 연출 진행
            StateTimer = TickTimer.CreateFromSeconds(Runner, 3.0f);
            Debug.Log("[보스] 체력 50% 이하! 2페이즈 광폭화 돌입!");
            return;
        }

        // 6. 어그로 딜미터기 기록
        if (attacker != null)
        {
            if (_damageTracker.ContainsKey(attacker)) _damageTracker[attacker] += damage;
            else _damageTracker[attacker] = damage;
        }
    }

    // ==========================================
    // 보스 사망 시 무조건 거쳐가는 공통 함수
    // ==========================================
    private void ExecuteDeath()
    {
        CurrentHP = 0f;
        CurrentPatternIndex = -1;
        CurrentStepIndex = -1;
        CurrentGroggy = 0f;

        Debug.Log("보스 처치 완료! (공통 사망 로직 실행)");
        ChangeState(BossState.Die);

        // 사망 시 조명 및 기믹 원상복구
        if (IsGimmickActive)
        {
            IsGimmickActive = false;
        }
    }

    // 컴포넌트 탐색 대신 유니티 태그("Player") 기반 탐색
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_DebugKillBoss()
    {
        if (CurrentState == BossState.Die)
        {
            return;
        }

        Debug.Log("[Boss Debug] 디버그 강제 킬 버튼 입력!");
        ExecuteDeath(); // 공통 사망 함수 호출!
    }

    private void FindClosestTarget()
    {
        float closestDistance = float.MaxValue;
        NetworkObject bestTarget = null;

        // 1. 씬에 있는 태그가 "Player"인 모든 게임 오브젝트를 싹 긁어옵니다.
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");

        foreach (var playerObj in players)
        {
            if (playerObj == null) continue;

            // 2. 퓨전 네트워크 동기화를 위해 오브젝트에 붙은 NetworkObject 컴포넌트를 가져옵니다.
            NetworkObject nObj = playerObj.GetComponent<NetworkObject>();
            if (nObj == null) continue;

            // 3. 거리를 계산해서 가장 가까운 대상을 선별합니다.
            float dist = Vector3.Distance(transform.position, playerObj.transform.transform.position);
            if (dist < closestDistance)
            {
                closestDistance = dist;
                bestTarget = nObj; // 최종 어그로 대상 타겟팅
            }
        }

        AggroTarget = bestTarget;
    }

    // ==========================================
    // 타겟을 향해 회전하며 걷기
    // ==========================================
    private void MoveTowardsTarget()
    {
        Vector3 direction = (AggroTarget.transform.position - transform.position).normalized;
        direction.y = 0;

        if (direction != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Runner.DeltaTime);

            // 초당 속도를 프레임당 이동 거리로 변환하여 물리 미끄러짐 함수에 전달
            Vector3 frameDisplacement = transform.forward * moveSpeed * Runner.DeltaTime;
            PerformWallSlideDisplacement(frameDisplacement);
        }
    }

    // ==========================================
    // 벽 미끄러짐 물리 연산 (y축 갱신도 추가함)
    // ==========================================
    private void PerformWallSlideDisplacement(Vector3 targetDisplacement)
    {
        if (targetDisplacement.sqrMagnitude < 0.000001f) return;

        Vector3 sphereCenter = transform.position + Vector3.up * castHeightOffset;

        if (Physics.SphereCast(sphereCenter, bodyRadius, targetDisplacement.normalized, out RaycastHit hit, targetDisplacement.magnitude, wallLayerMask))
        {
            float safeDistance = Mathf.Max(0f, hit.distance - 0.01f);
            Vector3 safeMove = targetDisplacement.normalized * safeDistance;
            transform.position += safeMove;

            Vector3 remainingDisplacement = targetDisplacement - safeMove;
            Vector3 slideDisplacement = Vector3.ProjectOnPlane(remainingDisplacement, hit.normal);
            slideDisplacement.y = 0;

            if (slideDisplacement.sqrMagnitude > 0.000001f)
            {
                Vector3 newSphereCenter = transform.position + Vector3.up * castHeightOffset;
                if (Physics.SphereCast(newSphereCenter, bodyRadius, slideDisplacement.normalized, out RaycastHit hit2, slideDisplacement.magnitude, wallLayerMask))
                {
                    float safeDistance2 = Mathf.Max(0f, hit2.distance - 0.01f);
                    transform.position += slideDisplacement.normalized * safeDistance2;
                }
                else
                {
                    transform.position += slideDisplacement;
                }
            }
        }
        else
        {
            transform.position += targetDisplacement;
        }

        StickToGround();
    }

    // ==========================================
    // Y축 지형에 보스를 밀착시키는 로직
    // ==========================================
    private void StickToGround()
    {
        // 1. 레이저를 쏠 시작점: 보스 발바닥에서 계단 높이(stepHeight)만큼 위로 올린 위치
        Vector3 rayStart = transform.position + (Vector3.up * stepHeight);

        // 2. 바닥을 향해 레이저를 쏩니다. (거리는 stepHeight + 여유분)
        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, stepHeight * 2f, groundLayerMask))
        {
            // 3. 바닥을 찾았다면? 보스의 Y축 위치를 바닥의 Y축과 정확히 일치시킵니다.
            Vector3 newPosition = transform.position;
            newPosition.y = hit.point.y;
            transform.position = newPosition;
        }
        else
        {
            // 4. 바닥이 안 닿는다면? (절벽이거나 공중에 떴을 때) -> 가짜 중력을 적용해 아래로 떨어뜨립니다.
            transform.position += Vector3.down * gravitySpeed * Runner.DeltaTime;
        }
    }

    // ------------------------------------------
    // 상태이상 부여 함수 (Enum을 버리고 int ID로만 받음!)
    // ------------------------------------------
    public void ApplyStatus(int statusId, float duration, float power = 1.0f)
    {
        if (!HasStateAuthority || CurrentState == BossState.Die) return;
        if (statusId == 0) return; // 0은 빈 칸을 의미하므로 무시

        // 이미 같은 상태이상이 있는지 찾아서 덮어씌우기 (시간 연장)
        for (int i = 0; i < ActiveStatuses.Length; i++)
        {
            if (ActiveStatuses[i].StatusId == statusId)
            {
                BossStatusData existing = ActiveStatuses[i];
                existing.EndTime = Runner.SimulationTime + duration;
                existing.Power = power;
                ActiveStatuses.Set(i, existing);
                return;
            }
        }

        // 없다면 빈자리(0)를 찾아서 새로 넣기
        for (int i = 0; i < ActiveStatuses.Length; i++)
        {
            if (ActiveStatuses[i].StatusId == 0) // None 대신 0 사용
            {
                ActiveStatuses.Set(i, new BossStatusData
                {
                    StatusId = statusId,
                    EndTime = Runner.SimulationTime + duration,
                    Power = power
                });
                return;
            }
        }
    }

    // ------------------------------------------
    // 매 프레임 시간 지난 상태이상 지우기 (FixedUpdateNetwork에 추가 필요)
    // ------------------------------------------
    private void ProcessStatuses()
    {
        for (int i = 0; i < ActiveStatuses.Length; i++)
        {
            if (ActiveStatuses[i].StatusId != 0) // 0이 아닐 때만 체크
            {
                if (Runner.SimulationTime >= ActiveStatuses[i].EndTime)
                {
                    // 빈 깡통 데이터(ID가 0)를 덮어씌워서 삭제 처리
                    ActiveStatuses.Set(i, new BossStatusData());
                }
            }
        }
    }

    // ------------------------------------------
    // 실시간 데미지 배율 계산기 (SO 완전 연동)
    // ------------------------------------------
    public float GetIncomingDamageMultiplier()
    {
        float multiplier = 1.0f;

        // 기믹이 켜져있다면, 기본적으로 약한 데미지만 들어가게 깎아버립니다!
        if (IsGimmickActive)
        {
            multiplier *= gimmickDamageReduction;
        }

        for (int i = 0; i < ActiveStatuses.Length; i++)
        {
            if (ActiveStatuses[i].StatusId != 0)
            {
                // 1. 도감에서 현재 적용 중인 SO 데이터를 찾는다
                StatusEffectData data = statusDatabase.Find(x => x.statusId == ActiveStatuses[i].StatusId);

                // 2. 이 SO가 '받는 피해(IncomingDamage)'에 영향을 주는 놈이면 배율을 곱한다!
                if (data != null && data.effectTarget == StatusEffectTarget.IncomingDamage)
                {
                    multiplier *= ActiveStatuses[i].Power;
                }
            }
        }
        return multiplier;
    }

    public float GetOutgoingDamageMultiplier()
    {
        float multiplier = DamageMultiplier; // 기존 스테이지 난이도 배율 베이스
        for (int i = 0; i < ActiveStatuses.Length; i++)
        {
            if (ActiveStatuses[i].StatusId != 0)
            {
                StatusEffectData data = statusDatabase.Find(x => x.statusId == ActiveStatuses[i].StatusId);

                // '주는 피해(OutgoingDamage)'에 영향을 주는 놈이면 배율을 곱한다!
                if (data != null && data.effectTarget == StatusEffectTarget.OutgoingDamage)
                {
                    multiplier *= ActiveStatuses[i].Power;
                }
            }
        }
        return multiplier;
    }

    // ==========================================
    // UI 쪽 스크립트에 전달할 함수
    // ==========================================
    public List<ActiveStatusUIInfo> GetActiveStatusesForUI()
    {
        List<ActiveStatusUIInfo> activeList = new List<ActiveStatusUIInfo>();

        for (int i = 0; i < ActiveStatuses.Length; i++)
        {
            if (ActiveStatuses[i].StatusId != 0)
            {
                // 도감에서 현재 걸린 상태이상의 SO 데이터를 찾아옵니다.
                StatusEffectData so = statusDatabase.Find(x => x.statusId == ActiveStatuses[i].StatusId);

                if (so != null)
                {
                    activeList.Add(new ActiveStatusUIInfo
                    {
                        Data = so,
                        // 현재 시간(SimulationTime)을 빼서 순수하게 '남은 시간'만 계산해서 넘겨줍니다.
                        RemainingTime = Mathf.Max(0, ActiveStatuses[i].EndTime - Runner.SimulationTime),
                        Power = ActiveStatuses[i].Power
                    });
                }
            }
        }

        return activeList;
    }
    // ... 나머지 기존 기능들
}
