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
    public bool IsSpawnedReady { get; private set; }

    [Tooltip("UI 체력바에 표시될 보스의 이름")]
    [Networked, Capacity(32)] public string bossName { get; set; }

    [Header("기본 설정")]
    public float moveSpeed = 2.5f;
    public float rotationSpeed = 5.0f;
    public float wakeUpRange = 10.0f;
    public float aggroRefreshTime = 5.0f;
    public float patternCooldown = 2.0f;

    [Header("제자리 회전(Turn-in-place) 설정")]
    [Tooltip("타겟과의 각도 오차가 이 값(도) 이하면 회전을 멈춘다(데드존). 미세 떨림 방지.")]
    public float facingDeadzoneAngle = 10f;
    [Tooltip("제자리에서 몸이 도는 속도(도/초). 반드시 Turn 애니메이션의 발놀림 속도와 맞춰야 미끄러짐(Foot Sliding)이 사라진다.")]
    public float turnInPlaceSpeed = 80f;

    [Header("상태이상 도감 (SO 리스트)")]
    [Tooltip("기획자가 만든 StatusEffectData SO들을 여기에 모두 넣어주세요.")]
    public List<StatusEffectData> statusDatabase;

    [Header("기상(Wake Up) 설정")]
    [Tooltip("잠에서 깰 때 재생할 애니메이션 이름 (예: Scream)")]
    public string wakeUpAnimName = "Scream";
    [Tooltip("포효 애니메이션의 지속 시간 (초)")]
    public float wakeUpDuration = 2.8f;
    private int _wakeUpAnimHash; // 최적화용 해시 변수
    [Header("페이즈 전환 설정")]
    [Tooltip("2페이즈 진입(PhaseTransition) 연출 애니메이션의 지속 시간 (초)")]
    public float phaseTransitionDuration = 3.0f;

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
    protected virtual List<BossPatternModule> CurrentAvailablePatterns => (CurrentPhase == 2) ? phase2Patterns : phase1Patterns;

    // 🔥 [버그 픽스] 공중에 떠 있는지 여부. 기본 보스는 항상 false(지상).
    //    PolarDragonBoss처럼 비행하는 보스가 IsFlightActive로 오버라이드해서, 중력/바닥밀착을 끄게 한다.
    protected virtual bool IsAirborne => false;

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

    // 책임 분리된 헬퍼들 (Spawned에서 주입) - 갓 오브젝트 해체
    protected BossStatusEffectResolver _statusResolver; // 상태이상 DB/배율
    protected BossPatternSelector _patternSelector;     // 패턴 룰렛
    protected BossAggroTracker _aggroTracker;           // 딜미터기 장부
    private BossMovementController _movement;            // 벽 미끄러짐/지형 밀착

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
        // 헬퍼 주입을 IsSpawnedReady보다 먼저 (UI가 즉시 조회해도 안전)
        _statusResolver = new BossStatusEffectResolver(statusDatabase);
        _patternSelector = new BossPatternSelector();
        _aggroTracker = new BossAggroTracker();
        _movement = new BossMovementController(wallLayerMask, bodyRadius, castHeightOffset,
                                               groundLayerMask, stepHeight, gravitySpeed);

        IsSpawnedReady = true;

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

        // 🔥 [버그 픽스] 이동 연산 전에 현재 비행 상태를 컨트롤러에 주입.
        //    비행 중이면 MoveWithWallSlide 내부의 StickToGround(중력/바닥밀착)가 통째로 스킵된다.
        _movement.BypassGround = IsAirborne;

        UpdateBossAI();
        ProcessLocomotionPhysics();

        // 패턴 실행 중일 때의 그래프 기반 이동 (Curve-Driven Movement) 연산
        if (CurrentState == BossState.ExecutingPattern)
        {
            ProcessPatternMovement();
            ProcessPatternTracking();
        }
    }

    // 패턴 실행 중일 때 머리(헤드 IK)가 타겟을 쳐다봐도 되는지 판정.
    // 비-패턴 상태(Idle/Walk)에서는 항상 true. 패턴 중에는 액션의 트래킹 규칙을 따른다.
    private bool ShouldHeadTrackTarget()
    {
        if (CurrentState != BossState.ExecutingPattern) return true;
        if (CurrentPatternIndex < 0 || CurrentStepIndex < 0) return true;
        if (CurrentPatternIndex >= CurrentAvailablePatterns.Count) return true;

        BossPatternModule pattern = CurrentAvailablePatterns[CurrentPatternIndex];
        if (CurrentStepIndex >= pattern.ActionCount) return true; // 범위 밖 방어
        BossActionModule action = pattern.GetAction(CurrentStepIndex);

        if (!action.enableTracking) return false; // 트래킹 끈 패턴 → 머리도 고정

        // 트래킹을 켰더라도 trackingStopPercent를 지나면 시선도 고정(몸통 트래킹과 동일 타이밍)
        float remaining = StateTimer.RemainingTime(Runner) ?? 0f;
        float progress = (action.duration > 0f) ? Mathf.Clamp01(1f - (remaining / action.duration)) : 1f;
        return progress <= action.trackingStopPercent;
    }

    // [신규 추가] 모든 클라이언트가 각자의 화면에서 애니메이터를 갱신하는 함수
    private void UpdateLocomotionVisuals()
    {
        if (AggroTarget == null || CurrentState == BossState.Sleep || CurrentState == BossState.Die)
        {
            _visual?.ResetLookAt();
            _visual?.SetDirection(0f, 0f);
            _visual?.SetTurn(0f);
            return;
        }

        // 1. 고개 돌리기 — 단, 패턴 중에는 그 액션의 트래킹 설정을 존중한다.
        //    🔥 [버그 픽스] 기존엔 머리(헤드 IK)가 enableTracking과 무관하게 항상 타겟을 쳐다봤다.
        //    이제 enableTracking=false 거나 trackingStopPercent를 지난 액션이면 머리도 따라가지 않는다.
        //    (몸통 트래킹 ProcessPatternTracking과 동일한 조건 → 시선과 몸이 같은 타이밍에 고정됨)
        if (ShouldHeadTrackTarget())
            _visual?.SetLookAtTarget(AggroTarget.transform.position);
        else
            _visual?.ResetLookAt();

        // 2. 애니메이션 방향 블렌딩
        if (CurrentState == BossState.Walk)
        {
            // 내 컴퓨터에서 보여지는 보스 위치 기준, 타겟이 어디있는지 방향 계산
            Vector3 localTargetPos = transform.InverseTransformPoint(AggroTarget.transform.position);
            localTargetPos.y = 0;
            Vector3 animDir = localTargetPos.normalized;

            // 모든 클라이언트가 각자의 애니메이터에 값을 집어넣어 다리를 움직임!
            _visual?.SetDirection(animDir.x, animDir.z);
            _visual?.SetTurn(0f); // 걷는 중엔 이동 블렌드가 처리하므로 제자리 턴은 끔
        }
        else
        {
            _visual?.SetDirection(0f, 0f);

            // 🔥 [요구사항4] 제자리(Idle)에서 타겟과의 각도 오차를 Turn 파라미터(-1 좌 ~ +1 우)로 전달.
            //    각 클라이언트가 자기 화면의 네트워크 동기화된 transform/타겟 기준으로 계산하므로 별도 네트워크 변수 불필요.
            //    Animator의 Turn 블렌드 트리(좌턴/Idle/우턴 스텝)가 이 값으로 발을 움직여 미끄러짐을 없앤다.
            float turnSign = 0f;
            if (CurrentState == BossState.Idle)
            {
                Vector3 toTarget = AggroTarget.transform.position - transform.position;
                toTarget.y = 0f;
                if (toTarget.sqrMagnitude > 0.0001f)
                {
                    Vector3 flatForward = transform.forward; flatForward.y = 0f;
                    float angle = Vector3.SignedAngle(flatForward.normalized, toTarget.normalized, Vector3.up);
                    if (Mathf.Abs(angle) > facingDeadzoneAngle)
                        turnSign = Mathf.Clamp(angle / 90f, -1f, 1f);
                }
            }
            _visual?.SetTurn(turnSign);
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

        // ★ 루트모션 모드: 비주얼이 모아둔 실제 이동량/회전량을 가져와 적용 (커브 무시)
        if (action.useRootMotion)
        {
            if (_visual != null)
            {
                // 🔥 [버그 픽스] 회전 먼저 적용 — 90도 루트모션 턴(Turn90L/R_RM)이 실제로 몸을 돌리게 한다.
                //    캡처가 OnAnimatorMove 로 자동적용을 가로채므로, 여기서 안 돌려주면 보스가 제자리에서 안 돈다.
                Quaternion rmRot = _visual.ConsumeRootMotionRotation();
                if (rmRot != Quaternion.identity)
                    transform.rotation = rmRot * transform.rotation;

                Vector3 rmDelta = _visual.ConsumeRootMotionDelta();
                rmDelta.y = 0f; // 수직은 기존 지형/중력 로직에 맡김
                if (rmDelta.sqrMagnitude > 0.0000001f)
                    _movement.MoveWithWallSlide(transform, rmDelta, Runner.DeltaTime);
            }
            return;
        }

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

            // 벽 미끄러짐 연산을 헬퍼에 위임하여 안전하게 이동
            _movement.MoveWithWallSlide(transform, frameDisplacement, Runner.DeltaTime);
        }
    }

    // [수정됨] 오직 방장(서버)만 실행하는 보스 이동 물리 처리
    private void ProcessLocomotionPhysics()
    {
        if (AggroTarget == null || CurrentState == BossState.Sleep || CurrentState == BossState.Die)
        {
            return;
        }

        Vector3 toTarget = AggroTarget.transform.position - transform.position;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude < 0.0001f) return;
        Vector3 toTargetDir = toTarget.normalized;

        if (CurrentState == BossState.Walk)
        {
            // 걷는 중엔 다리가 실제로 앞으로 움직이므로, 몸통을 부드럽게 슬러프해도 미끄러짐이 보이지 않는다.
            Quaternion targetRot = Quaternion.LookRotation(toTargetDir);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, (rotationSpeed * 0.4f) * Runner.DeltaTime);

            // 이동 연산 (이동속도 버프 반영: 비행 시 Boss_Flying power 2 → 2배)
            Vector3 localTargetPos = transform.InverseTransformPoint(AggroTarget.transform.position);
            localTargetPos.y = 0;
            Vector3 animDir = localTargetPos.normalized;

            float speed = moveSpeed * GetMoveSpeedMultiplier();
            Vector3 worldMoveOffset = transform.TransformDirection(new Vector3(animDir.x, 0, animDir.z)) * speed * Runner.DeltaTime;
            _movement.MoveWithWallSlide(transform, worldMoveOffset, Runner.DeltaTime);
        }
        else if (CurrentState == BossState.Idle)
        {
            // ==========================================
            // 🔥 [요구사항4] 제자리 회전 풋슬라이딩(동상 회전) 제거
            // ==========================================
            Vector3 flatForward = transform.forward; flatForward.y = 0f;
            float angle = Vector3.SignedAngle(flatForward.normalized, toTargetDir, Vector3.up);

            if (IsAirborne)
            {
                // 공중 호버: 발이 땅에 없으므로 '미끄러짐' 개념이 없다 → 부드러운 슬러프로 타겟을 향함.
                Quaternion targetRot = Quaternion.LookRotation(toTargetDir);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, (rotationSpeed * 0.3f) * Runner.DeltaTime);
            }
            else if (Mathf.Abs(angle) > facingDeadzoneAngle)
            {
                // 지상 제자리 회전: 몸통을 '턴 애니메이션 발놀림과 동일한 속도(turnInPlaceSpeed)'로만 돌린다.
                // 동시에 UpdateLocomotionVisuals가 Turn 파라미터로 발 스텝 모션을 블렌딩하므로,
                // 발이 회전을 끌고 가는 것처럼 보여 '팽이 회전(Foot Sliding)'이 사라진다.
                float maxStep = turnInPlaceSpeed * Runner.DeltaTime;
                float step = Mathf.Clamp(angle, -maxStep, maxStep);
                transform.Rotate(0f, step, 0f, Space.World);
            }
        }
    }

    private void ProcessPatternTracking()
    {
        if (AggroTarget == null || CurrentPatternIndex < 0) return;

        BossPatternModule pattern = CurrentAvailablePatterns[CurrentPatternIndex];
        BossActionModule action = pattern.GetAction(CurrentStepIndex);

        // 🔥 [회전 이중 적용 버그 픽스] 루트모션 액션(Turn90_RM 등)은 '루트모션 회전'이 방향을 전담한다.
        //    여기서 트래킹 Slerp까지 같이 돌리면 (루트모션 + 트래킹) 회전이 합산되어 타겟을 지나쳐 과회전 →
        //    패턴 종료 후 Idle 턴-인-플레이스가 되돌리면서 '휙 한 번 더 도는' 튕김이 발생했다.
        //    → 루트모션 액션이면 트래킹을 완전히 배제한다. (둘 다 원하면 해당 액션을 비-루트모션으로 설계할 것)
        if (action.useRootMotion) return;

        // 기획자가 이 패턴엔 트래킹(호밍)을 안 켰다면 패스
        if (!action.enableTracking) return;

        // 현재 액션의 진행률(0~1) 계산
        float remaining = StateTimer.RemainingTime(Runner) ?? 0f;
        float progress = Mathf.Clamp01(1f - (remaining / action.duration));

        // 기획자가 설정한 퍼센트(예: 0.5 = 50%)까지만 타겟을 바라보며 회전
        // 그 이후에는 회전을 멈춰서 플레이어가 구르기로 피할 틈을 줌! (엘든링 꿀팁 1)
        if (progress <= action.trackingStopPercent)
        {
            Vector3 direction = (AggroTarget.transform.position - transform.position).normalized;
            direction.y = 0;
            if (direction != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, action.trackingSpeed * Runner.DeltaTime);
            }
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
        // Vector3 dir = (AggroTarget.transform.position - transform.position).normalized;
        // dir.y = 0;
        // if (dir != Vector3.zero)
        //     transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), rotationSpeed * Runner.DeltaTime);

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
        // 룰렛 로직은 BossPatternSelector로 위임 (자식은 이 메서드를 오버라이드해 가로채기 가능)
        return _patternSelector.SelectByRange(CurrentAvailablePatterns, currentDistance);
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

        // ★ 이 액션이 루트모션이면 캡처 ON(누적 리셋), 아니면 OFF
        _visual?.SetRootMotionCapture(action.useRootMotion);
    }

    // ==========================================
    // [상태 관리 및 비주얼 동기화]
    // ==========================================
    protected void ChangeState(BossState newState)
    {
        // ★ 패턴 실행 상태를 벗어나면 루트모션 캡처를 끈다
        if (newState != BossState.ExecutingPattern)
            _visual?.SetRootMotionCapture(false);

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
                int targetHash = Animator.StringToHash(action.animationStateName);
                Debug.Log($"[보스] 재생 시도 상태명: '{action.animationStateName}' (hash={targetHash})");
                _visual.PlayAction(targetHash);

                // (원본 클립 길이 / 기획자가 설정한 시간)으로 정확한 배속 도출
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

            // 상태가 '방금 딱 바뀌었을 때만' 1회 호출
            if (_lastState != CurrentState)
            {
                _visual.SetAnimSpeed(1.0f); // 패턴이 끝났으니 배속을 무조건 1.0(정상)으로 복구

                // 🔥 [수정됨] 복잡한 사운드/애니 로직을 모두 Visual에게 위임합니다!
                if (CurrentState == BossState.Groggy)
                {
                    float speedMult = groggyAnimLength / groggyDuration;
                    // groggyDuration도 함께 넘겨, 비주얼이 자기 클립 길이에 맞춰 배속을 재계산할 수 있게 함
                    _visual.PlayGroggy(speedMult, groggyDuration);
                }
                else if (CurrentState == BossState.Idle || CurrentState == BossState.Walk)
                {
                    _visual.DoLocomotion();
                }
                else if (CurrentState == BossState.Die)
                {
                    _visual.PlayDie();
                }
                else if (CurrentState == BossState.WakeUp)
                {
                    _visual.PlayWakeUp(_wakeUpAnimHash);
                }
                else if (CurrentState == BossState.PhaseTransition)
                {
                    _visual.PlayPhaseTransition(_wakeUpAnimHash);
                }

                _lastState = CurrentState;
            }
        }

        UpdateLocomotionVisuals();

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
        // 최다 딜러 산출은 BossAggroTracker에 위임
        NetworkObject topDPSPlayer = _aggroTracker.GetTopAttacker();

        if (topDPSPlayer != null)
        {
            AggroTarget = topDPSPlayer;
            Debug.Log($"[Aggro] 어그로 갱신! 대상: {topDPSPlayer.Id}");
        }
        else
        {
            // 누적 딜이 없으면 가장 가까운 대상으로 갱신
            FindClosestTarget();
            Debug.Log("[Aggro] 누적 딜량 없음. 가장 가까운 대상으로 갱신.");
        }

        _aggroTracker.Clear();
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
            StateTimer = TickTimer.CreateFromSeconds(Runner, phaseTransitionDuration);
            Debug.Log("[보스] 체력 50% 이하! 2페이즈 돌입!");

            //ApplyStatus(1); // 광폭화 SO 만들어둔 거 부여

            return;
        }

        // 6. 어그로 딜미터기 기록 (장부 관리는 헬퍼에 위임)
        _aggroTracker.Record(attacker, damage);
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

    // ------------------------------------------
    // 상태이상 부여 (SO 데이터를 기반으로 자동 설정) 함수
    // ------------------------------------------
    public void ApplyStatus(int statusId)
    {
        if (!HasStateAuthority || CurrentState == BossState.Die) return;
        if (statusId == 0) return; // 0은 빈 칸을 의미하므로 무시

        // 도감 조회는 Resolver에 위임
        StatusEffectData data = _statusResolver.GetData(statusId);
        if (data == null)
        {
            Debug.LogWarning($"[Status] ID {statusId}에 해당하는 상태이상 데이터를 찾을 수 없습니다.");
            return;
        }

        // 무한 지속일 경우 약 11일(999999초) 동안 유지되도록 설정하여 시간 제한을 사실상 없앰
        float duration = data.isInfinite ? 999999f : data.defaultDuration;
        float targetEndTime = Runner.SimulationTime + duration;

        // 이미 같은 상태이상이 있는지 찾아서 덮어씌우기 (시간 연장)
        for (int i = 0; i < ActiveStatuses.Length; i++)
        {
            if (ActiveStatuses[i].StatusId == statusId)
            {
                BossStatusData existing = ActiveStatuses[i];
                existing.EndTime = targetEndTime;
                existing.Power = data.Power;
                ActiveStatuses.Set(i, existing);
                return;
            }
        }

        // 없다면 빈자리(0)를 찾아서 새로 넣기
        for (int i = 0; i < ActiveStatuses.Length; i++)
        {
            if (ActiveStatuses[i].StatusId == 0)
            {
                ActiveStatuses.Set(i, new BossStatusData
                {
                    StatusId = statusId,
                    EndTime = targetEndTime,
                    Power = data.Power
                });
                return;
            }
        }
    }

    // ------------------------------------------
    // 특정 트리거로 상태이상을 강제로 끌 때 사용하는 함수
    // ------------------------------------------
    public void RemoveStatus(int statusId)
    {
        if (!HasStateAuthority) return;

        for (int i = 0; i < ActiveStatuses.Length; i++)
        {
            if (ActiveStatuses[i].StatusId == statusId)
            {
                // 빈 깡통으로 덮어씌워서 즉시 삭제!
                ActiveStatuses.Set(i, new BossStatusData());
                Debug.Log($"[Status] 상태이상(ID: {statusId})이 강제 해제되었습니다.");
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
    // 배율/UI 계산은 모두 Resolver에 위임 (공개 시그니처 유지)
    public float GetIncomingDamageMultiplier()
        => _statusResolver.GetIncomingMultiplier(ActiveStatuses, IsGimmickActive, gimmickDamageReduction);

    public float GetOutgoingDamageMultiplier()
        => _statusResolver.GetOutgoingMultiplier(ActiveStatuses, DamageMultiplier);

    // 이동속도 배율 (MoveSpeed 상태이상). 비행 버프(Boss_Flying, power 2) 등이 여기에 반영됨.
    public float GetMoveSpeedMultiplier()
        => _statusResolver.GetMoveSpeedMultiplier(ActiveStatuses);

    // ==========================================
    // UI 쪽 스크립트에 전달할 함수
    // ==========================================
    public List<ActiveStatusUIInfo> GetActiveStatusesForUI()
        => _statusResolver.BuildUIList(ActiveStatuses, Runner.SimulationTime);

    // ==========================================
    // UI 및 비주얼에서 특정 상태이상이 현재 켜져있는지 쉽게 확인하는 헬퍼 함수
    // ==========================================
    public bool HasStatus(int statusId)
    {
        for (int i = 0; i < ActiveStatuses.Length; i++)
        {
            if (ActiveStatuses[i].StatusId == statusId)
            {
                return true;
            }
        }
        return false;
    }
}
