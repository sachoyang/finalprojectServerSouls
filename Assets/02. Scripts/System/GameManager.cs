using UnityEngine;

[DisallowMultipleComponent]
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Managed Managers")]
    [SerializeField] private BackendManager backendManager;
    [SerializeField] private AbilityManager abilityManager;
    [SerializeField] private NetworkManager networkManager;
    [SerializeField] private GameObject[] combatOnlyManagers;

    public BackendManager Backend => backendManager;
    public AbilityManager Ability => abilityManager;
    public NetworkManager Network => networkManager;

    public static GameManager GetOrCreate()
    {
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
        ResolveManagers();
        AdoptManagersUnderRoot();
    }

    private void OnValidate()
    {
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
        SetCombatManagersActive(false);
    }

    public void SetLobbyMode()
    {
        SetCombatManagersActive(false);
    }

    public void SetCombatMode()
    {
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
        if (current != null)
        {
            return current;
        }

        T found = FindObjectOfType<T>(true);
        if (found != null)
        {
            return found;
        }

        GameObject managerObject = new GameObject(typeof(T).Name);
        managerObject.transform.SetParent(transform, false);
        return managerObject.AddComponent<T>();
    }

    private void AdoptManagersUnderRoot()
    {
        AdoptManager(backendManager);
        AdoptManager(abilityManager);
        AdoptManager(networkManager);
    }

    private void AdoptManager(MonoBehaviour manager)
    {
        if (manager == null || manager.transform == transform || manager.transform.IsChildOf(transform))
        {
            return;
        }

        manager.transform.SetParent(transform, true);
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
