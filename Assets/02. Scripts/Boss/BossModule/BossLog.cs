// 전투 핫패스(피격/어그로/패턴 갱신 등) 로그 전용 래퍼.
// [Conditional] 덕분에 에디터가 아닌 빌드에서는 '호출 자체'가 컴파일에서 제거되어,
// 문자열 보간(GC 할당)과 로그 비용이 릴리즈 빌드에 전혀 남지 않는다.
public static class BossLog
{
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    public static void Info(string message) => UnityEngine.Debug.Log(message);

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    public static void Warn(string message) => UnityEngine.Debug.LogWarning(message);
}
