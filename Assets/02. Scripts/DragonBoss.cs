using System;
using Fusion;
using UnityEngine;
using Random = UnityEngine.Random;

public enum DragonState
{
    Idle, Walk, BiteAttack, ClawAttack, HornAttack, Jump, Scream, Sleep
}

public enum BossPattern
{
    None,
    Pattern1_Bite,
    Pattern2_Claw,
    Pattern3_Jump
}

public class DragonBoss : NetworkBehaviour
{
    [Header("이동 및 회전 설정")]
    public float moveSpeed = 2.5f;
    public float rotationSpeed = 5.0f;

    [Header("사거리 설정")]
    public float attackRange = 6.0f;
    
    [Header("공격 쿨타임 설정")]
    [Tooltip("패턴이 끝난 후 다음 공격까지 대기하는 시간")]
    public float patternCooldown = 2.0f;

    [Header("벽 충돌 설정 (가벽 박스 사용)")]
    public LayerMask wallLayerMask;
    public float bodyRadius = 2.0f;
    public float castHeightOffset = 2.0f;

    [Header("체력 설정")]
    public float maxHP = 100000f;
    [Networked] public float CurrentHP { get; set; }

    [Networked] public DragonState CurrentState { get; set; }
    [Networked] private TickTimer StateTimer { get; set; }
    
    // [핵심 추가] 패턴 공격 사이의 쿨타임을 독립적으로 관리하는 타이머
    [Networked] private TickTimer AttackCooldown { get; set; }
    
    [Networked] public NetworkObject AggroTarget { get; set; }

    [Networked] public BossPattern CurrentPattern { get; set; }
    [Networked] public int PatternStep { get; set; }

    [Networked] public byte ActionCounter { get; set; }
    [Networked] public float CurrentActionDuration { get; set; }

    public DragonVisual visual;
    private ChangeDetector _changeDetector;

    public override void Spawned()
    {
        _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);
        visual = GetComponentInChildren<DragonVisual>();

        if (HasStateAuthority)
        {
            CurrentHP = maxHP; 
            ChangeState(DragonState.Idle, 1.0f);
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (HasStateAuthority)
        {
            if (CurrentHP <= 0) return;

            // 타겟 갱신은 상시 실행
            FindAggroTarget();

            // 1. 현재 실행 중인 패턴이 있다면 패턴 스텝만 처리하고 이동/AI는 스킵
            if (CurrentPattern != BossPattern.None)
            {
                if (StateTimer.Expired(Runner))
                {
                    ExecutePatternStep();
                }
                return;
            }

            // 2. 패턴을 안 쓰고 있는 평소 상태의 상시 AI 제어
            UpdateContinuousAI();
        }
    }

    // ==========================================
    // [수정된 핵심 AI] 딜레이 없는 즉각 반응 로직
    // ==========================================
    private void UpdateContinuousAI()
    {
        if (AggroTarget == null) return;

        float dist = Vector3.Distance(transform.position, AggroTarget.transform.position);

        // [조건 A] 사거리 내에 들어왔을 때
        if (dist <= attackRange)
        {
            // 공격 쿨타임이 끝났다면 망설임 없이 즉시 공격!
            if (AttackCooldown.ExpiredOrNotRunning(Runner))
            {
                ChooseAttackPattern();
            }
            else
            {
                // 쿨타임 대기 중일 때는 걷기 모션을 끄고 타겟을 노려보며 딜타임 가짐
                if (CurrentState != DragonState.Idle)
                {
                    ChangeState(DragonState.Idle, 0.1f);
                }
                
                // 제자리에서 타겟 방향으로 부드럽게 회전만 유지
                Vector3 dir = (AggroTarget.transform.position - transform.position).normalized;
                dir.y = 0;
                if (dir != Vector3.zero)
                {
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), rotationSpeed * Runner.DeltaTime);
                }
            }
        }
        // [조건 B] 사거리보다 멀 때
        else
        {
            // 즉시 걷기 상태로 전환하여 타겟 추적
            if (CurrentState != DragonState.Walk)
            {
                // 타이머 길이는 애니메이션 재생용으로만 던져둠 (실제 AI 전환은 상시 거리 체크가 우선함)
                ChangeState(DragonState.Walk, 1.0f);
            }

            MoveTowardsTarget();
        }
    }

    private void MoveTowardsTarget()
    {
        Vector3 direction = (AggroTarget.transform.position - transform.position).normalized;
        direction.y = 0;

        if (direction != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Runner.DeltaTime);
            
            PerformWallSlideMovement(transform.forward, moveSpeed);
        }
    }

    // (스피어캐스트 예측형 슬라이딩 로직 그대로 유지)
    private void PerformWallSlideMovement(Vector3 moveDir, float speed)
    {
        Vector3 targetDisplacement = moveDir * speed * Runner.DeltaTime;
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

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_TakeDamage(float damage)
    {
        if (CurrentHP <= 0) return;

        CurrentHP -= damage;
        Debug.Log($"[Server] 보스가 데미지를 입음! 남은 HP: {CurrentHP}");

        if (CurrentHP <= 0)
        {
            CurrentHP = 0;
            Debug.Log("보스 처치 완료!");
            ChangeState(DragonState.Sleep, 100f); 
        }
    }

    private void ChangeState(DragonState newState, float duration)
    {
        CurrentState = newState;
        CurrentActionDuration = duration; 
        ActionCounter++;                  
        StateTimer = TickTimer.CreateFromSeconds(Runner, duration);
    }

    private bool IsAnyPlayerBehind()
    {
        foreach (var player in FindObjectsOfType<NetworkPlayerController>())
        {
            if (player == null) continue;

            Vector3 toPlayer = (player.transform.position - transform.position);
            toPlayer.y = 0;

            if (toPlayer.magnitude <= attackRange * 1.5f)
            {
                Vector3 bossForward = transform.forward;
                bossForward.y = 0;

                if (Vector3.Dot(bossForward.normalized, toPlayer.normalized) < -0.1f)
                {
                    return true;
                }
            }
        }
        return false;
    }

    private void ChooseAttackPattern()
    {
        float rand = Random.Range(0f, 100f);
        bool isPlayerBehind = IsAnyPlayerBehind();

        if (isPlayerBehind)
        {
            if (rand < 60f) CurrentPattern = BossPattern.Pattern3_Jump;
            else if (rand < 80f) CurrentPattern = BossPattern.Pattern1_Bite;
            else CurrentPattern = BossPattern.Pattern2_Claw;
        }
        else
        {
            if (rand < 40f) CurrentPattern = BossPattern.Pattern1_Bite;
            else if (rand < 80f) CurrentPattern = BossPattern.Pattern2_Claw;
            else CurrentPattern = BossPattern.Pattern3_Jump;
        }

        // 패턴 시작
        PatternStep = 0;
        ExecutePatternStep();
    }

    private void ExecutePatternStep()
    {
        if (CurrentPattern == BossPattern.Pattern1_Bite)
        {
            if (PatternStep == 0) { ChangeState(DragonState.Idle, 0.3f); PatternStep++; }
            else if (PatternStep == 1) { ChangeState(DragonState.BiteAttack, 1.2f); PatternStep++; }
            else if (PatternStep == 2) { ChangeState(DragonState.BiteAttack, 1.2f); PatternStep++; }
            else { EndPattern(); }
        }
        else if (CurrentPattern == BossPattern.Pattern2_Claw)
        {
            if (PatternStep == 0) { ChangeState(DragonState.ClawAttack, 1.5f); PatternStep++; }
            else if (PatternStep == 1) { ChangeState(DragonState.ClawAttack, 1.5f); PatternStep++; }
            else if (PatternStep == 2) { ChangeState(DragonState.Idle, 0.5f); PatternStep++; }
            else if (PatternStep == 3) { ChangeState(DragonState.ClawAttack, 1.5f); PatternStep++; }
            else { EndPattern(); }
        }
        else if (CurrentPattern == BossPattern.Pattern3_Jump)
        {
            if (PatternStep == 0) 
            { 
                ChangeState(DragonState.Jump, 2.0f); 
                PatternStep++; 
            }
            else 
            { 
                EndPattern(); 
            }
        }
    }

    private void EndPattern()
    {
        CurrentPattern = BossPattern.None;
        PatternStep = 0;
        
        // [핵심] 패턴이 완전히 끝나면 다음 공격까지의 쿨타임 타이머를 돌림
        AttackCooldown = TickTimer.CreateFromSeconds(Runner, patternCooldown);
        
        // 대기 상태로 전환 (상시 AI 루프가 즉시 이어받아 거리 체크 후 대기할지 추적할지 결정)
        ChangeState(DragonState.Idle, 0.1f);
    }

    private void FindAggroTarget()
    {
        float closestDistance = float.MaxValue;
        NetworkObject bestTarget = null;

        foreach (var player in FindObjectsOfType<NetworkPlayerController>())
        {
            float dist = Vector3.Distance(transform.position, player.transform.position);
            if (dist < closestDistance)
            {
                closestDistance = dist;
                bestTarget = player.Object;
            }
        }
        AggroTarget = bestTarget;
    }

    public override void Render()
    {
        foreach (var change in _changeDetector.DetectChanges(this))
        {
            if (change == nameof(ActionCounter))
            {
                UpdateAnimation(CurrentState);
            }
        }
    }

    private void UpdateAnimation(DragonState state)
    {
        visual.SetSpeed(state == DragonState.Walk ? 1.0f : 0.0f);

        float originalLength = 1.0f;
        switch (state)
        {
            case DragonState.BiteAttack: originalLength = 1.2f; break;
            case DragonState.ClawAttack: originalLength = 3.333f; break;
            case DragonState.HornAttack: originalLength = 2.167f; break;
            case DragonState.Jump: originalLength = 2.0f; break;
            case DragonState.Idle: originalLength = 1.333f; break;
        }

        float animSpeedMultiplier = 1.0f;
        if (CurrentActionDuration > 0)
        {
            animSpeedMultiplier = originalLength / CurrentActionDuration;
        }

        visual.SetAnimSpeed(animSpeedMultiplier);

        switch (state)
        {
            case DragonState.BiteAttack: visual.DoBiteAttack(); break;
            case DragonState.ClawAttack: visual.DoClawAttack(); break;
            case DragonState.Jump: visual.DoJump(); break;
            case DragonState.HornAttack: visual.DoHornAttack(); break;
            case DragonState.Scream: visual.DoScream(); break;
            case DragonState.Sleep: visual.DoSleep(); break;
        }
    }

    private bool _showDebug = false;

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Slash))
        {
            _showDebug = !_showDebug;
        }
    }

    private void OnGUI()
    {
        if (_showDebug)
        {
            GUIStyle style = new GUIStyle();
            style.fontSize = 35;
            style.normal.textColor = Color.yellow;
            style.fontStyle = FontStyle.Bold;

            string debugInfo =
                $"[Dragon Boss Debug]\n" +
                $"Current State : {CurrentState}\n" +
                $"Current Pattern : {CurrentPattern}\n" +
                $"Pattern Step : {PatternStep}\n" +
                $"Cooldown : {(AttackCooldown.IsRunning ? Math.Round(AttackCooldown.RemainingTime(Runner).Value, 1) : 0)}s\n" +
                $"HP : {CurrentHP} / {maxHP}";

            GUI.Label(new Rect(30, 30, 500, 300), debugInfo, style);
        }
    }
}