using UnityEngine;

[DisallowMultipleComponent]
public class GameManager : MonoBehaviour
{
    // 씬을 넘어 살아남는 최상위 흐름 관리자다.
    // 실제 네트워크/백엔드/스킬 로직은 각 전용 매니저가 담당하고,
    // GameManager는 필요한 매니저를 찾거나 생성하고 현재 씬 모드에 맞춰 on/off만 조정한다.
    public static GameManager Instance { get; private set; }

    [Header("Managed Managers")]
    // 아래 매니저들은 직접 자식으로 묶지 않는다.
    // Fusion NetworkRunner 같은 컴포넌트는 root GameObject에서 DontDestroyOnLoad 되어야 하므로,
    // GameManager는 참조만 들고 각 매니저 오브젝트는 root 상태를 유지한다.
    [SerializeField] private BackendManager backendManager;
    [SerializeField] private AbilityManager abilityManager;
    [SerializeField] private NetworkManager networkManager;

    // 로그인/로비에서는 꺼두고 전투 씬에서만 켤 매니저나 오브젝트를 등록한다.
    // 예: 전투 전용 판정/스폰/연출 관리자. 아직 없으면 비워둬도 된다.
    [SerializeField] private GameObject[] combatOnlyManagers;

    public BackendManager Backend => backendManager;
    public AbilityManager Ability => abilityManager;
    public NetworkManager Network => networkManager;

    public static GameManager GetOrCreate()
    {
        // 씬 로드 순서 때문에 LoginSceneController 같은 곳에서 먼저 호출될 수 있다.
        // 이미 있으면 그 인스턴스를 쓰고, 없으면 씬 안 비활성 오브젝트까지 찾는다.
        if (Instance != null)
        {
            return Instance;
        }

        GameManager existing = FindObjectOfType<GameManager>(true);
        if (existing != null)
        {
            return existing;
        }

        GameObject managerObject = new GameObject(nameof(GameManager));
        return managerObject.AddComponent<GameManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Awake 시점에 핵심 매니저 참조를 확보한다.
        // Inspector에 연결되어 있으면 그 참조를 쓰고, 없으면 씬에서 찾거나 새로 만든다.
        ResolveManagers();
    }

    private void OnValidate()
    {
        // Inspector 배열에 빈 칸이 남아도 런타임 순회가 단순하도록 뒤쪽으로 정리한다.
        compactCombatOnlyManagers();
    }

    public void ResolveManagers()
    {
        backendManager = ResolveManager(backendManager);
        abilityManager = ResolveManager(abilityManager);
        networkManager = ResolveManager(networkManager);
        compactCombatOnlyManagers();
    }

    public void SetLoginMode()
    {
        // 로그인 화면에서는 전투 전용 오브젝트가 입력/네트워크 흐름에 끼어들 필요가 없다.
        SetCombatManagersActive(false);
    }

    public void SetLobbyMode()
    {
        // 로비도 전투 판정이나 전투 연출 관리자는 꺼둔다.
        SetCombatManagersActive(false);
    }

    public void SetCombatMode()
    {
        // 실제 전투 씬에 들어온 뒤 필요한 전투 전용 관리자만 켠다.
        SetCombatManagersActive(true);
    }

    public void SetCombatManagersActive(bool isActive)
    {
        if (combatOnlyManagers == null)
        {
            return;
        }

        for (int i = 0; i < combatOnlyManagers.Length; i++)
        {
            if (combatOnlyManagers[i] != null)
            {
                combatOnlyManagers[i].SetActive(isActive);
            }
        }
    }

    private T ResolveManager<T>(T current) where T : MonoBehaviour
    {
        // 1. Inspector에 이미 연결된 참조가 있으면 그대로 사용한다.
        if (current != null)
        {
            return current;
        }

        // 2. 씬 또는 DontDestroyOnLoad 영역에 이미 존재하는 매니저를 찾는다.
        // true를 넘겨 비활성 오브젝트도 후보에 포함한다.
        T found = FindObjectOfType<T>(true);
        if (found != null)
        {
            return found;
        }

        // 3. 어디에도 없으면 root GameObject로 새로 만든다.
        // parent를 GameManager로 바꾸지 않는 이유는 일부 매니저(NetworkRunner 등)가 root여야 하기 때문이다.
        GameObject managerObject = new GameObject(typeof(T).Name);
        return managerObject.AddComponent<T>();
    }

    private void compactCombatOnlyManagers()
    {
        if (combatOnlyManagers == null || combatOnlyManagers.Length == 0)
        {
            return;
        }

        int writeIndex = 0;
        for (int readIndex = 0; readIndex < combatOnlyManagers.Length; readIndex++)
        {
            if (combatOnlyManagers[readIndex] == null)
            {
                continue;
            }

            combatOnlyManagers[writeIndex] = combatOnlyManagers[readIndex];
            writeIndex++;
        }

        for (int i = writeIndex; i < combatOnlyManagers.Length; i++)
        {
            combatOnlyManagers[i] = null;
        }
    }
}
