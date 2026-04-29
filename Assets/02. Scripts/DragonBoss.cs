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
    Pattern2_Claw   
}

public class DragonBoss : NetworkBehaviour
{
    [Header("설정")]
    public float moveSpeed = 3.0f;      
    public float rotationSpeed = 5.0f;  

    [Header("사거리 설정")]
    public float attackRange = 5.0f;    
    public float runDistance = 15.0f;   

    [Networked] public DragonState CurrentState { get; set; }
    [Networked] private TickTimer StateTimer { get; set; }
    [Networked] public NetworkObject AggroTarget { get; set; }

    [Networked] public BossPattern CurrentPattern { get; set; }
    [Networked] public int PatternStep { get; set; }

    // [핵심 추가 1] 연속된 같은 상태(Bite->Bite)도 무조건 감지하기 위한 행동 카운터
    [Networked] public byte ActionCounter { get; set; }
    
    // [핵심 추가 2] 현재 상태의 목표 지속 시간을 클라이언트에게 공유
    [Networked] public float CurrentActionDuration { get; set; }

    public DragonVisual visual;
    private ChangeDetector _changeDetector;

    public override void Spawned()
    {
        _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);

        if (HasStateAuthority)
        {
            ChangeState(DragonState.Idle, 3.0f);
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (HasStateAuthority)
        {
            UpdateBossAI();

            if (CurrentState == DragonState.Walk && AggroTarget != null)
            {
                MoveTowardsTarget();
            }
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
        CurrentActionDuration = duration; // 배속 계산을 위해 시간 저장
        ActionCounter++;                  // 숫자가 무조건 오르므로 Render에서 100% 감지됨
        StateTimer = TickTimer.CreateFromSeconds(Runner, duration);
    }

    private void ChooseAttackPattern()
    {
        float rand = Random.Range(0f, 100f);

        if (rand < 35f)
        {
            CurrentPattern = BossPattern.Pattern1_Bite;
            PatternStep = 0;
            ExecutePatternStep();
        }
        else if (rand < 70f)
        {
            CurrentPattern = BossPattern.Pattern2_Claw;
            PatternStep = 0;
            ExecutePatternStep();
        }
        else
        {
            // 알려주신 Jump 원본 길이 2.0초 적용
            ChangeState(DragonState.Jump, 2.0f);
        }
    }

    private void ExecutePatternStep()
    {
        if (CurrentPattern == BossPattern.Pattern1_Bite)
        {
            if (PatternStep == 0)
            {
                ChangeState(DragonState.Idle, 0.5f); 
                PatternStep++;
            }
            else if (PatternStep == 1)
            {
                ChangeState(DragonState.BiteAttack, 1.2f); // 알려주신 원본 길이 1.2초
                PatternStep++;
            }
            else if (PatternStep == 2)
            {
                ChangeState(DragonState.BiteAttack, 1.2f); 
                PatternStep++;
            }
            else
            {
                EndPattern(); 
            }
        }
        else if (CurrentPattern == BossPattern.Pattern2_Claw)
        {
            if (PatternStep == 0)
            {
                // 알려주신 Claw 원본은 3.333초지만, 기획의 속도감을 위해 1.5초만에 강제 실행 (약 2.2배속 재생됨)
                ChangeState(DragonState.ClawAttack, 1.5f); 
                PatternStep++;
            }
            else if (PatternStep == 1)
            {
                ChangeState(DragonState.ClawAttack, 1.5f); 
                PatternStep++;
            }
            else if (PatternStep == 2)
            {
                ChangeState(DragonState.Idle, 0.8f);       
                PatternStep++;
            }
            else if (PatternStep == 3)
            {
                ChangeState(DragonState.ClawAttack, 1.5f); 
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

        foreach (var player in FindObjectsOfType<Player>())
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
            // [수정됨] CurrentState 대신 ActionCounter가 변할 때마다 무조건 애니메이션 업데이트 실행
            if (change == nameof(ActionCounter))
            {
                UpdateAnimation(CurrentState);
            }
        }
    }

    private void UpdateAnimation(DragonState state)
    {
        visual.SetSpeed(state == DragonState.Walk ? 1.0f : 0.0f);

        // 알려주신 원본 애니메이션 길이 세팅
        float originalLength = 1.0f;
        switch (state)
        {
            case DragonState.BiteAttack: originalLength = 1.2f; break;
            case DragonState.ClawAttack: originalLength = 3.333f; break;
            case DragonState.HornAttack: originalLength = 2.167f; break;
            case DragonState.Jump: originalLength = 2.0f; break;
            case DragonState.Idle: originalLength = 1.333f; break;
        }

        // [핵심 로직] 원본 길이 / 코드에서 지시한 시간 = 애니메이션 재생 배속
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
                $"Pattern Step : {PatternStep}";

            GUI.Label(new Rect(30, 30, 500, 300), debugInfo, style);
        }
    }
}