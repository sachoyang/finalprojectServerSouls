using System;
using Fusion;
using UnityEngine;
using System.Collections.Generic;
using Random = UnityEngine.Random;

public enum DragonState
{
    Idle, Walk, BiteAttack, ClawAttack, HornAttack, Jump, Scream, Sleep, Die
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

    [Header("사거리 및 어그로 설정")]
    public float attackRange = 6.0f;
    public float wakeUpRange = 10.0f;
    [Tooltip("어그로 대상을 재탐색하는 주기 (초)")]
    public float aggroRefreshTime = 10.0f; 
    
    [Header("공격 쿨타임 설정")]
    public float patternCooldown = 2.0f;
    
    [Header("애니메이션 원본 클립 길이")]
    public float animClipIdle = 1.333f;
    public float animClipBite = 1.2f;
    public float animClipClaw = 3.333f;
    public float animClipHorn = 2.167f;
    public float animClipJump = 2.0f;
    public float animClipDie = 1.9f;

    [Header("패턴 실제 시전 시간")]
    public float durationBite = 2f;
    public float durationClaw = 2f;
    public float durationJump = 3f;

    [Header("벽 충돌 설정")]
    public LayerMask wallLayerMask;
    public float bodyRadius = 2.0f;
    public float castHeightOffset = 2.0f;

    [Header("체력 설정")]
    public float maxHP = 100000f;
    [Networked] public float CurrentHP { get; set; }

    [Networked] public DragonState CurrentState { get; set; }
    [Networked] private TickTimer StateTimer { get; set; }
    [Networked] private TickTimer AttackCooldown { get; set; }
    [Networked] public NetworkObject AggroTarget { get; set; }
    
    // [핵심 추가] 10초 타이머와 누적 딜량 기록 장부
    [Networked] private TickTimer AggroTimer { get; set; }
    private Dictionary<NetworkObject, float> _damageTracker = new Dictionary<NetworkObject, float>();

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
            ChangeState(DragonState.Sleep, 0f);
        }
        UpdateAnimation(CurrentState);
    }

    public override void FixedUpdateNetwork()
    {
        if (HasStateAuthority)
        {
            if (CurrentHP <= 0) return;

            // [핵심] 자고 있지 않을 때 10초마다 어그로 대상을 재정산합니다.
            if (CurrentState != DragonState.Sleep)
            {
                if (AggroTimer.Expired(Runner))
                {
                    UpdateAggroByDamage();
                }
            }

            UpdateBossAI();

            if (CurrentState == DragonState.Walk && AggroTarget != null)
            {
                MoveTowardsTarget();
            }
        }
    }

    private void UpdateBossAI()
    {
        if (CurrentState == DragonState.Sleep && CurrentHP > 0)
        {
            FindClosestTarget(); 
            if (AggroTarget != null)
            {
                float dist = Vector3.Distance(transform.position, AggroTarget.transform.position);
                if (dist <= wakeUpRange)
                {
                    Debug.Log("플레이어 감지! 보스가 잠에서 깨어납니다.");
                    ChangeState(DragonState.Scream, 2.5f); 
                    
                    // 깨어나는 순간 첫 10초 타이머 시작
                    AggroTimer = TickTimer.CreateFromSeconds(Runner, aggroRefreshTime);
                }
            }
            return; 
        }

        // 도중에 타겟이 죽거나 나가서 사라지면 가장 가까운 타겟으로 땜빵
        if (AggroTarget == null && CurrentState != DragonState.Sleep)
        {
            FindClosestTarget();
        }

        if (StateTimer.Expired(Runner))
        {
            if (CurrentPattern != BossPattern.None)
            {
                ExecutePatternStep();
                return;
            }

            if (CurrentState == DragonState.Idle || CurrentState == DragonState.Scream)
            {
                if (AggroTarget != null)
                {
                    float dist = Vector3.Distance(transform.position, AggroTarget.transform.position);
                    if (dist <= attackRange) ChooseAttackPattern();
                    else UpdateContinuousAI();
                }
            }
            else
            {
                UpdateContinuousAI();
            }
        }
        else
        {
            if (CurrentPattern == BossPattern.None)
            {
                UpdateContinuousAI();
            }
        }
    }

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

        // 장부에 딜량 누적
        if (attacker != null)
        {
            if (_damageTracker.ContainsKey(attacker))
                _damageTracker[attacker] += damage;
            else
                _damageTracker[attacker] = damage;
        }

        if (CurrentHP <= 0)
        {
            CurrentHP = 0;
            Debug.Log("보스 처치 완료!");
            ChangeState(DragonState.Die, 2); 
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

    private void UpdateContinuousAI()
    {
        if (AggroTarget == null || CurrentState == DragonState.Sleep) return;

        // 1. 공격 쿨타임이 아직 안 끝났다면? (대기 턴)
        if (!AttackCooldown.ExpiredOrNotRunning(Runner))
        {
            // 걷기를 멈추고 제자리에 대기 (비비적거림 100% 차단)
            if (CurrentState != DragonState.Idle) ChangeState(DragonState.Idle, 0.1f);
            
            // 플레이어가 도망가도 쫓아가지 않고 고개만 돌리며 다음 턴을 노려봄
            Vector3 dir = (AggroTarget.transform.position - transform.position).normalized;
            dir.y = 0;
            if (dir != Vector3.zero)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), rotationSpeed * Runner.DeltaTime);
                
            return; // 아래의 이동 로직을 아예 스킵!
        }

        // -----------------------------------------------------
        // 2. 여기서부터는 '쿨타임이 끝났을 때(내 공격 턴)'만 실행됩니다.
        // -----------------------------------------------------
        float dist = Vector3.Distance(transform.position, AggroTarget.transform.position);

        if (dist <= attackRange)
        {
            // 사거리 내에 있다면 망설임이나 걷기 없이 [즉시] 패턴 발동
            ChooseAttackPattern();
        }
        else
        {
            // 사거리 밖이라면, 때리기 위해 확실하게 걸어서 추적
            if (CurrentState != DragonState.Walk) ChangeState(DragonState.Walk, 1.0f);
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

        PatternStep = 0;
        ExecutePatternStep();
    }

    private void ExecutePatternStep()
    {
        if (CurrentPattern == BossPattern.Pattern1_Bite)
        {
            if (PatternStep == 0) { ChangeState(DragonState.Idle, 0.3f); PatternStep++; }
            else if (PatternStep == 1) { ChangeState(DragonState.BiteAttack, durationBite); PatternStep++; }
            else if (PatternStep == 2) { ChangeState(DragonState.BiteAttack, durationBite); PatternStep++; }
            else { EndPattern(); }
        }
        else if (CurrentPattern == BossPattern.Pattern2_Claw)
        {
            if (PatternStep == 0) { ChangeState(DragonState.ClawAttack, durationClaw); PatternStep++; }
            else if (PatternStep == 1) { ChangeState(DragonState.ClawAttack, durationClaw); PatternStep++; }
            else if (PatternStep == 2) { ChangeState(DragonState.Idle, 0.5f); PatternStep++; }
            else if (PatternStep == 3) { ChangeState(DragonState.ClawAttack, durationClaw); PatternStep++; }
            else { EndPattern(); }
        }
        else if (CurrentPattern == BossPattern.Pattern3_Jump)
        {
            if (PatternStep == 0) 
            { 
                ChangeState(DragonState.Jump, durationJump); 
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
        
        AttackCooldown = TickTimer.CreateFromSeconds(Runner, patternCooldown);
        ChangeState(DragonState.Idle, 1f);
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
        visual.SetSleep(state == DragonState.Sleep);

        float originalLength = 1.0f;
        switch (state)
        {
            case DragonState.BiteAttack: originalLength = animClipBite; break;
            case DragonState.ClawAttack: originalLength = animClipClaw; break;
            case DragonState.HornAttack: originalLength = animClipHorn; break;
            case DragonState.Jump: originalLength = animClipJump; break;
            case DragonState.Idle: originalLength = animClipIdle; break;
            case DragonState.Die: originalLength = animClipDie; break;
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
            case DragonState.Die: visual.DoDie(); break;
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
                $"Aggro Refresh in : {(AggroTimer.IsRunning ? Math.Round(AggroTimer.RemainingTime(Runner).Value, 1) : 0)}s\n" +
                $"HP : {CurrentHP} / {maxHP}";

            GUI.Label(new Rect(30, 30, 500, 350), debugInfo, style);
        }
    }
}