using UnityEngine;

// 백엔드 도메인 이벤트(OnSessionExpired) ↔ 네트워크 종료를 잇는 유일한 브리지
// 교차 의존(NetworkManager → BackendManager)을 이 컴포넌트 한 곳으로 격리한다.
public class SessionGuard : MonoBehaviour
{
    private ISessionGuard _networkGuard; // 네트워크 레이어는 인터페이스로만 참조

    // 네트워크 레이어 주입 (구체 타입을 모르게)
    public void Initialize(ISessionGuard networkGuard)
    {
        _networkGuard = networkGuard;
    }

    private void Start()
    {
        // 백엔드가 발행하는 도메인 이벤트만 구독
        if (BackendManager.Instance != null)
            BackendManager.Instance.OnSessionExpired += OnSessionExpired;
    }

    private void OnDestroy()
    {
        if (BackendManager.Instance != null)
            BackendManager.Instance.OnSessionExpired -= OnSessionExpired;
    }

    // 도메인 이벤트 수신 → 네트워크 종료 처리 위임
    private void OnSessionExpired()
    {
        _networkGuard?.HandleSessionExpired();
    }
}
