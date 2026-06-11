// 세션 만료 시 '네트워크 종료' 책임을 추상화 (네트워크 레이어를 백엔드 없이 테스트 가능하게)
public interface ISessionGuard
{
    // 세션이 만료되었을 때 호출: 런너 셧다운 등 네트워크 정리 수행
    void HandleSessionExpired();
}
