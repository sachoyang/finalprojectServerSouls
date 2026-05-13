using Fusion;
using UnityEngine;

public enum DragonState
{
    Idle, Walk, BiteAttack, ClawAttack, HornAttack, Jump, Scream, Sleep
}

public enum BossPattern
{
    None,
    Pattern1_Bite,
    Pattern2_Claw,
    Pattern3_Jump // [추가됨] 점프 패턴 명시
}

public class DragonBoss : NetworkBehaviour
{
    [Header("설정")]
    public float moveSpeed = 2.5f;
    public float rotationSpeed = 5.0f;

    [Header("사거리 설정")]
    public float attackRange = 6.0f;
    public float runDistance = 10.0f;

    // ==========================================
    // 보스 체력 시스템
    // ==========================================
    [Header("체력 설정")]
    public float maxHP = 100000f;
    [Networked] public float CurrentHP { get; set; }

    // ==========================================
    [Networked] public DragonState CurrentState { get; set; }
    [Networked] private TickTimer StateTimer { get; set; }
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
            ChangeState(DragonState.Idle, 3.0f);
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (HasStateAuthority)
        {
            if (CurrentHP <= 0) return;

            UpdateBossAI();

            if (CurrentState == DragonState.Walk && AggroTarget != null)
            {
                MoveTowardsTarget();
            }
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

    private void UpdateBossAI()
    {
        if (StateTimer.Expired(Runner))
        {
            FindAggroTarget();

            if (CurrentPattern != BossPattern.None)
            {
                ExecutePatternStep();
                return;
            }

            if (CurrentState == DragonState.Idle)
            {
                if (AggroTarget != null)
                {
                    float dist = Vector3.Distance(transform.position, AggroTarget.transform.position);
                    if (dist <= attackRange)
                    {
                        ChooseAttackPattern();
                    }
                    else
                    {
                        ChangeState(DragonState.Walk, 3.0f);
                    }
                }
            }
            else
            {
                ChangeState(DragonState.Idle, 2.5f);
            }
        }
        else
        {
            if (CurrentState == DragonState.Walk && AggroTarget != null)
            {
                MoveTowardsTarget();
            }
        }
    }

    private void MoveTowardsTarget()
    {
        Vector3 direction = (AggroTarget.transform.position - transform.position).normalized;
        direction.y = 0;

        if (direction != Vector3.zero)
        {
            float dist = Vector3.Distance(transform.position, AggroTarget.transform.position);

            if (dist <= attackRange)
            {
                ChangeState(DragonState.Idle, 0.1f);
                return;
            }

            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Runner.DeltaTime);
            transform.position += transform.forward * moveSpeed * Runner.DeltaTime;
        }
    }

    private void ChangeState(DragonState newState, float duration)
    {
        CurrentState = newState;
        CurrentActionDuration = duration; 
        ActionCounter++;                  
        StateTimer = TickTimer.CreateFromSeconds(Runner, duration);
    }

    // ==========================================
    // [핵심 추가] 플레이어가 보스 뒤편에 있는지 판별하는 함수
    // ==========================================
    private bool IsAnyPlayerBehind()
    {
        foreach (var player in FindObjectsOfType<NetworkPlayerController>())
        {
            if (player == null) continue;

            // 보스와 플레이어 간의 수평 거리 및 방향 계산
            Vector3 toPlayer = (player.transform.position - transform.position);
            toPlayer.y = 0;

            // 사거리(인식 범위) 내에 있는 플레이어만 판정 (공격 사거리보다 약간 넓게 감지)
            if (toPlayer.magnitude <= attackRange * 1.5f)
            {
                Vector3 bossForward = transform.forward;
                bossForward.y = 0;

                // 내적(Dot)이 음수면 플레이어가 보스의 기준면 뒤쪽에 위치함을 의미함
                // -0.1f 이하로 설정하여 경계선이 아닌 확실한 뒤편일 때만 트리거되도록 안전장치 적용
                if (Vector3.Dot(bossForward.normalized, toPlayer.normalized) < -0.1f)
                {
                    return true;
                }
            }
        }
        return false;
    }

    // ==========================================
    // [수정됨] 조건에 따른 패턴 동적 확률 추첨
    // ==========================================
    private void ChooseAttackPattern()
    {
        float rand = Random.Range(0f, 100f);
        bool isPlayerBehind = IsAnyPlayerBehind();

        if (isPlayerBehind)
        {
            // [조건 만족] 플레이어가 뒤에 있을 때: 점프 패턴 60%, 물기 20%, 할퀴기 20%
            if (rand < 60f)
            {
                CurrentPattern = BossPattern.Pattern3_Jump;
            }
            else if (rand < 80f)
            {
                CurrentPattern = BossPattern.Pattern1_Bite;
            }
            else
            {
                CurrentPattern = BossPattern.Pattern2_Claw;
            }
            Debug.Log("[AI] 배후 플레이어 감지! 점프 패턴 확률 대폭 상승.");
        }
        else
        {
            // [평소 상태] 정면에 있을 때: 물기 40%, 할퀴기 40%, 점프 20%
            if (rand < 40f)
            {
                CurrentPattern = BossPattern.Pattern1_Bite;
            }
            else if (rand < 80f)
            {
                CurrentPattern = BossPattern.Pattern2_Claw;
            }
            else
            {
                CurrentPattern = BossPattern.Pattern3_Jump;
            }
        }

        PatternStep = 0;
        ExecutePatternStep();
    }

    private void ExecutePatternStep()
    {
        if (CurrentPattern == BossPattern.Pattern1_Bite)
        {
            if (PatternStep == 0) { ChangeState(DragonState.Idle, 0.5f); PatternStep++; }
            else if (PatternStep == 1) { ChangeState(DragonState.BiteAttack, 1.2f); PatternStep++; }
            else if (PatternStep == 2) { ChangeState(DragonState.BiteAttack, 1.2f); PatternStep++; }
            else { EndPattern(); }
        }
        else if (CurrentPattern == BossPattern.Pattern2_Claw)
        {
            if (PatternStep == 0) { ChangeState(DragonState.ClawAttack, 1.5f); PatternStep++; }
            else if (PatternStep == 1) { ChangeState(DragonState.ClawAttack, 1.5f); PatternStep++; }
            else if (PatternStep == 2) { ChangeState(DragonState.Idle, 0.8f); PatternStep++; }
            else if (PatternStep == 3) { ChangeState(DragonState.ClawAttack, 1.5f); PatternStep++; }
            else { EndPattern(); }
        }
        // ==========================================
        // [추가됨] Pattern3_Jump 실행 단계
        // ==========================================
        else if (CurrentPattern == BossPattern.Pattern3_Jump)
        {
            if (PatternStep == 0) 
            { 
                // 원본 길이 2.0초 동안 점프 모션 및 광역기 이펙트 대기
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
        ChangeState(DragonState.Idle, 2.5f);
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
                $"HP : {CurrentHP} / {maxHP}";

            GUI.Label(new Rect(30, 30, 500, 300), debugInfo, style);
        }
    }
}