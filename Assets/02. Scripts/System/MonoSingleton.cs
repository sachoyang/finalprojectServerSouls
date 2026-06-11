using UnityEngine;

// 모든 매니저의 부모가 될 제네릭 싱글톤 클래스
public abstract class MonoSingleton<T> : MonoBehaviour where T : MonoBehaviour
{
    private static T _instance;

    // 좀비 싱글톤 생성 방지 플래그 (종료 중 표시)
    private static bool _applicationIsQuitting = false;

    // 안전한 인스턴스 체크: 종료 중엔 false → 파괴 콜백에서 사용
    public static bool HasInstance => _instance != null && !_applicationIsQuitting;

    public static T Instance
    {
        get
        {
            // 종료 중이면 새 오브젝트를 만들지 않고 null 반환 (좀비 방지)
            if (_applicationIsQuitting)
            {
                Debug.LogWarning($"[MonoSingleton] 종료 중 '{typeof(T).Name}' 인스턴스 요청 → null 반환");
                return null;
            }

            if (_instance == null)
            {
                // 1. 씬에 이미 매니저가 있는지 찾는다.
                _instance = FindObjectOfType<T>();

                // 2. 없다면 빈 오브젝트를 만들고 컴포넌트를 붙여서 스스로 생성한다.
                if (_instance == null)
                {
                    GameObject singletonObject = new GameObject(typeof(T).Name);
                    _instance = singletonObject.AddComponent<T>();
                    DontDestroyOnLoad(singletonObject);
                }
            }
            return _instance;
        }
    }

    protected virtual void Awake()
    {
        // 씬에 이미 진짜(_instance)가 있는데, 새로운 씬이 로드되면서 내가 또 생성되었다면?
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject); // 즉시 자살
            return;
        }

        // 내가 최초라면
        _instance = this as T;
        DontDestroyOnLoad(gameObject);
    }

    protected virtual void OnApplicationQuit()
    {
        _applicationIsQuitting = true; // 종료 플래그 set (이후 Instance 접근은 null)
    }
}