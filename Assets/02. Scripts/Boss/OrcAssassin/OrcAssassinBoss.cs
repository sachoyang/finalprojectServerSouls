using Fusion;
using UnityEngine;

public class OrcAssassinBoss : NetworkBossCore
{
    [Header("오크 어쌔신 전용 기믹")]
    [Tooltip("플레이어와의 거리가 이 값 이상 떨어지면 단검 투척 패턴을 우선 발동")]
    public float daggerThrowDistance = 8.0f;
    public int daggerThrowPatternIndex = 3; // 인스펙터 패턴 리스트의 3번이 단검 투척이라 가정

    public override void Spawned()
    {
        base.Spawned();
        Debug.Log("[OrcAssassinBoss] 오크 어쌔신 스폰 완료!");
    }

    // ==========================================
    // [특수 AI] 패턴 룰렛 가로채기
    // ==========================================
    protected override int SelectPatternBasedOnRange(float currentDistance)
    {
        // 1. 어쌔신 특수 기믹: 거리가 너무 멀면 얄밉게 단검 던지기
        if (currentDistance >= daggerThrowDistance)
        {
            // (선택사항) 쿨타임 등을 체크해서 매번 던지지 않게 조절 가능
            return daggerThrowPatternIndex; 
        }

        // 2. 특수 조건이 아니면 기존 부모의 거리+가중치 룰렛 시스템 사용
        return base.SelectPatternBasedOnRange(currentDistance);
    }

    // ==========================================
    // 2페이즈(은신/독 부여) 진입 처리
    // ==========================================
    public override void FixedUpdateNetwork()
    {
        base.FixedUpdateNetwork();
        if (!HasStateAuthority) return;

        // 부모(Core)가 체력 50%를 감지하고 PhaseTransition 상태로 넘겼을 때
        if (CurrentState == BossState.PhaseTransition && !IsGimmickActive)
        {
            ActivatePhase2Gimmick();
        }
    }

    private void ActivatePhase2Gimmick()
    {
        IsGimmickActive = true;
        // 예: 이동 속도 1.5배 증가, 독 인챈트 등 상태이상(버프) SO 적용
        ApplyStatus(2); // 2번이 '가벼운 발걸음' 버프라고 가정
    }

    private bool _localStealthActive = false; // 내 화면에 은신이 켜져있는지 확인용

    // ==========================================
    // [비주얼 클라이언트 동기화]
    // ==========================================
    public override void Render()
    {
        base.Render(); // 부모(NetworkBossCore)가 하는 기본 애니메이션 처리는 그대로 실행!

        if (_visual == null) return;

        // 1. 서버에서 은신 기믹을 켰는데, 내 화면은 아직 쌩얼이라면? -> 투명화 켜기!
        if (IsGimmickActive && !_localStealthActive)
        {
            _localStealthActive = true;

            // _visual을 OrcAssassinVisual로 형변환해서 전용 함수 호출
            if (_visual is OrcAssassinVisual orcVisual)
            {
                orcVisual.EnableStealth();
            }
        }
        // 2. 서버에서 은신이 꺼졌는데(사망 등), 내 화면엔 아직 켜져있다면? -> 원래대로 복구!
        else if (!IsGimmickActive && _localStealthActive)
        {
            _localStealthActive = false;

            // (선택사항) OrcAssassinVisual.cs에 DisableStealth() 함수를 만드셨다면 호출
            // if (_visual is OrcAssassinVisual orcVisual) orcVisual.DisableStealth();
        }
    }
}