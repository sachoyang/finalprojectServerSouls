using Fusion;
using UnityEngine;

public enum DragonState
{
    Idle, Walk, BiteAttack, ClawAttack, HornAttack, Jump, Scream, Sleep
}

public class DragonBoss : NetworkBehaviour
{
    [Header("설정")]
    public float moveSpeed = 3.0f;      // 걷기 속도
    public float rotationSpeed = 5.0f;  // 회전 속도

    [Header("사거리 설정")]
    public float attackRange = 5.0f;    // 이 거리 안에 들어오면 공격 시작
    public float runDistance = 15.0f;   // 이 거리보다 멀면 뛰어감 (선택 사항)

    [Networked] public DragonState CurrentState { get; set; }
    [Networked] private TickTimer StateTimer { get; set; }
    [Networked] public NetworkObject AggroTarget { get; set; }

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
            
            // [물리 이동] Walk 상태일 때 타겟을 향해 이동 및 회전
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

            // 타이머가 끝나고 다음 행동을 고를 때, 현재 상태가 Idle이면 판단 시작
            if (CurrentState == DragonState.Idle)
            {
                if (AggroTarget != null)
                {
                    float dist = Vector3.Distance(transform.position, AggroTarget.transform.position);

                    // 1. 타겟이 공격 사거리 안에 있다면 공격 패턴 추첨
                    if (dist <= attackRange)
                    {
                        ChooseAttackPattern(); 
                    }
                    else
                    {
                        // 2. 사거리 밖이라면 다가감
                        ChangeState(DragonState.Walk, 3.0f); // 3초간 걷기 시도
                    }
                }
                else
                {
                    // 타겟이 없으면 멍때리기
                    ChangeState(DragonState.Idle, 1.0f); 
                }
            }
            else 
            {
                // 어떤 액션(Walk, Attack)이 끝났다면 무조건 Idle(딜타임)로 전환
                // 기획: 패턴 사이사이 평타 3~4대 가능한 시간 (약 2~3초)
                ChangeState(DragonState.Idle, 2.5f);
            }
        }
        else
        {
            // Walk 상태일 때 계속 쫓아감
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

            // [추가된 핵심 로직] 걷는 도중이라도 사거리 안에 들어오면 즉시 걷기 취소하고 대기
            if (dist <= attackRange)
            {
                ChangeState(DragonState.Idle, 0.1f); // 0.1초 뒤 바로 공격 패턴 뽑도록 유도
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
        StateTimer = TickTimer.CreateFromSeconds(Runner, duration);
    }

    private void ChooseAttackPattern()
    {
        float rand = Random.Range(0f, 100f);

        // 공격 사거리 안에서만 뽑히는 패턴들 (기획 확률 기반)
        if (rand < 50f) ChangeState(DragonState.BiteAttack, 2.0f);
        else if (rand < 80f) ChangeState(DragonState.ClawAttack, 2.5f);
        else ChangeState(DragonState.Jump, 3.0f);
    }

    private void FindAggroTarget()
    {
        float closestDistance = float.MaxValue;
        NetworkObject bestTarget = null;

        // 씬의 모든 Player 스크립트를 찾아 가장 가까운 타겟 설정
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
            if (change == nameof(CurrentState))
            {
                UpdateAnimation(CurrentState);
            }
        }
    }

    // 상태 변화에 따라 DragonVisual의 함수들을 호출
    private void UpdateAnimation(DragonState state)
    {
        // 이동 애니메이션 제어 (Walk일 때만 speed 1.0)
        visual.SetSpeed(state == DragonState.Walk ? 1.0f : 0.0f);

        // 상태별 트리거 실행
        switch (state)
        {
            case DragonState.BiteAttack: visual.DoBiteAttack(); break;
            case DragonState.ClawAttack: visual.DoClawAttack(); break;
            case DragonState.Jump: visual.DoJump(); break;
            case DragonState.HornAttack: visual.DoHornAttack(); break;
            case DragonState.Scream: visual.DoScream(); break;
        }
    }
}