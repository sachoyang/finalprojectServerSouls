using Fusion;
using UnityEngine;
using System.Collections.Generic;

// 보스의 현재 상태를 아주 심플하게 통제하기 위한 열거형
public enum BossState
{
    Sleep,
    WakeUp,           // [여기에 새로 추가!] 깨어날 때 포효하는 상태
    Idle,
    Walk,
    ExecutingPattern, // SO 패턴을 실행 중일 때의 상태
    Die
}

public class NetworkBossCore : NetworkBehaviour
{
    [Header("기본 설정")]
    public float moveSpeed = 2.5f;
    public float rotationSpeed = 5.0f;
    public float wakeUpRange = 10.0f;
    public float aggroRefreshTime = 10.0f;
    public float patternCooldown = 2.0f;

    [Header("기상(Wake Up) 설정")]
    [Tooltip("잠에서 깰 때 재생할 애니메이션 이름 (예: Scream)")]
    public string wakeUpAnimName = "Scream";
    [Tooltip("포효 애니메이션의 지속 시간 (초)")]
    public float wakeUpDuration = 2.8f;
    private int _wakeUpAnimHash; // 최적화용 해시 변수

    [Header("체력 설정")]
    public float maxHP = 100000f;
    [Networked] public float CurrentHP { get; set; }

    [Header("벽 충돌 설정 (미끄러짐)")]
    public LayerMask wallLayerMask;
    public float bodyRadius = 2.0f;
    public float castHeightOffset = 2.0f;

    [Header("패턴 데이터 (ScriptableObject 리스트)")]
    [Tooltip("이 보스가 사용할 수 있는 패턴 모듈(SO)들을 드래그해서 넣어주세요.")]
    public List<BossPatternModule> availablePatterns;

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
    // [그래프 기반 강제 이동 연산] (가장 중요한 부분!)
    // ==========================================
    private void ProcessPatternMovement()
    {
        if (CurrentPatternIndex < 0 || CurrentPatternIndex >= availablePatterns.Count) return;

        BossPatternModule pattern = availablePatterns[CurrentPatternIndex];
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
            }
            return;
        }

        // 2. 타겟 유실 시 재탐색
        if (AggroTarget == null) FindClosestTarget();
        if (AggroTarget == null) return;

        // 1-2. [새로 추가됨] 기상(포효) 중일 때의 처리
        if (CurrentState == BossState.WakeUp)
        {
            if (StateTimer.Expired(Runner))
            {
                // 포효 시간이 끝나면, 첫 공격 쿨타임을 장전하고 비로소 Idle로 넘어갑니다.
                AttackCooldown = TickTimer.CreateFromSeconds(Runner, patternCooldown);
                ChangeState(BossState.Idle);
                AggroTimer = TickTimer.CreateFromSeconds(Runner, aggroRefreshTime);
            }
            return; // 포효 중에는 밑으로 못 내려가게 막음!
        }

        // 회전 (패턴 중이 아닐 때 혹은 타겟을 쳐다봐야 할 때)
        if (CurrentState != BossState.ExecutingPattern)
        {
            Vector3 dir = (AggroTarget.transform.position - transform.position).normalized;
            dir.y = 0;
            if (dir != Vector3.zero)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), rotationSpeed * Runner.DeltaTime);
        }

        // 3. 패턴 진행 중일 때 다음 스텝으로 넘어가기
        if (CurrentState == BossState.ExecutingPattern)
        {
            if (StateTimer.Expired(Runner))
            {
                BossPatternModule pattern = availablePatterns[CurrentPatternIndex];
                CurrentStepIndex++;

                if (CurrentStepIndex < pattern.ActionCount)
                {
                    // 다음 동작 실행
                    ExecuteCurrentPatternStep(pattern.GetAction(CurrentStepIndex));
                }
                else
                {
                    // 패턴 완전 종료
                    CurrentPatternIndex = -1;
                    CurrentStepIndex = -1;
                    AttackCooldown = TickTimer.CreateFromSeconds(Runner, patternCooldown);
                    ChangeState(BossState.Idle);
                }
            }
            return;
        }

        // 4. 평상시 상태 (대기/걷기) - 패턴 쿨타임 돌았는지 확인
        if (AttackCooldown.ExpiredOrNotRunning(Runner))
        {
            float dist = Vector3.Distance(transform.position, AggroTarget.transform.position);
            int selectedPatternIdx = SelectPatternBasedOnRange(dist);

            if (selectedPatternIdx >= 0)
            {
                // 범위 내에 맞는 패턴이 있으면 즉시 실행
                StartPattern(selectedPatternIdx);
            }
            else
            {
                // 맞는 패턴이 없으면 걷기로 추적
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
        for (int i = 0; i < availablePatterns.Count; i++)
        {
            var pattern = availablePatterns[i];
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
            accumulatedWeight += availablePatterns[idx].weight;
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

        BossPatternModule pattern = availablePatterns[CurrentPatternIndex];
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
                BossActionModule action = availablePatterns[CurrentPatternIndex].GetAction(CurrentStepIndex);

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
            _visual.SetSleep(CurrentState == BossState.Sleep);

            // 상태가 '방금 딱 바뀌었을 때만' 1회 호출
            if (_lastState != CurrentState)
            {
                _visual.SetAnimSpeed(1.0f); // 패턴이 끝났으니 배속을 무조건 1.0(정상)으로 복구

                if (CurrentState == BossState.WakeUp)
                {
                    // [새로 추가됨] 기상 상태 진입 시 포효(Scream) 재생!
                    _visual.PlayAction(_wakeUpAnimHash);
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

                _lastState = CurrentState;
            }
        }
    }

    // (기존 UpdateAggroByDamage, FindClosestTarget, RPC_TakeDamage 등은 생략 없이 그대로 유지)
    // ==========================================
    // [핵심 추가] 10초마다 호출되는 딜미터기 정산 로직
    // ==========================================
    private void UpdateAggroByDamage()
    {
        NetworkObject topDPSPlayer = null;
        float maxDamage = 0f;

        // 1. 장부를 훑어서 가장 딜을 많이 넣은 사람을 찾음
        foreach (var kvp in _damageTracker)
        {
            if (kvp.Key != null && kvp.Value > maxDamage)
            {
                maxDamage = kvp.Value;
                topDPSPlayer = kvp.Key;
            }
        }

        // 2. 딜을 넣은 사람이 있으면 그 사람으로 타겟 변경
        if (topDPSPlayer != null && maxDamage > 0)
        {
            AggroTarget = topDPSPlayer;
            Debug.Log($"[Aggro] 어그로 변경! 대상: {topDPSPlayer.Id} (10초 누적 딜: {maxDamage})");
        }
        else
        {
            // 지난 10초간 아무도 때리지 않았다면 제일 가까운 사람으로 타겟 갱신
            FindClosestTarget();
            Debug.Log("[Aggro] 누적 딜량 없음. 가장 가까운 대상으로 갱신.");
        }

        // 3. 다음 10초를 위해 장부 초기화 및 타이머 재시작
        _damageTracker.Clear();
        AggroTimer = TickTimer.CreateFromSeconds(Runner, aggroRefreshTime);
    }

    // ==========================================
    // [수정됨] 데미지와 함께 "누가 때렸는지(attacker)" 기록
    // ==========================================
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_TakeDamage(float damage, NetworkObject attacker = null)
    {
        if (CurrentHP <= 0) return;

        CurrentHP -= damage;
        Debug.Log($"[Server] 보스가 데미지를 입음! 남은 HP: {CurrentHP}");

        if (attacker != null)
        {
            if (_damageTracker.ContainsKey(attacker)) _damageTracker[attacker] += damage;
            else _damageTracker[attacker] = damage;
        }

        if (CurrentHP <= 0)
        {
            CurrentHP = 0;
            Debug.Log("보스 처치 완료!");
            // [수정됨] DragonState가 아닌 BossState로 통일!
            ChangeState(BossState.Die);
        }
    }

    // 컴포넌트 탐색 대신 유니티 태그("Player") 기반 탐색
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
    // [누락 복구] 완벽한 벽 미끄러짐 물리 연산
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
    }

    // ... 나머지 기존 기능들
}