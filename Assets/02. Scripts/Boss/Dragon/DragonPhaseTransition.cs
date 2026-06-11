// 드래곤 2페이즈(변신) 진입 판정 전담 - 순수 로직 (네트워크 상태 변경/싱글톤 의존 없음)
public class DragonPhaseTransition
{
    // 변신 연출 상태에 진입했고 아직 기믹이 안 켜졌다면 true (1회성 발동 지점)
    public bool ShouldActivate(BossState state, bool gimmickActive)
    {
        return state == BossState.PhaseTransition && !gimmickActive;
    }
}
