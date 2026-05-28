using System.Collections.Generic;
using UnityEngine;

public class ScenePrefabManager : MonoBehaviour
{
    public static ScenePrefabManager Instance { get; private set; }

    [System.Serializable]
    public class ScenePrefabEntry
    {
        public string prefabId;
        public GameObject prefab;
        public bool createOnStart = true;
        public bool activeOnStart = false;

        [HideInInspector] public GameObject instance;
    }

    [Header("Scene Prefabs")]
    [SerializeField] private List<ScenePrefabEntry> scenePrefabs = new List<ScenePrefabEntry>();

    private readonly Dictionary<string, ScenePrefabEntry> prefabMap = new Dictionary<string, ScenePrefabEntry>();

    private void Awake()
    {
        Instance = this;

        BuildPrefabMap();
        CreateStartPrefabs();
    }

    private void BuildPrefabMap()
    {
        prefabMap.Clear();

        for (int i = 0; i < scenePrefabs.Count; i++)
        {
            ScenePrefabEntry entry = scenePrefabs[i];

            if (entry == null)
                continue;

            if (string.IsNullOrEmpty(entry.prefabId))
            {
                Debug.LogWarning("ScenePrefabManager: Prefab ID가 비어 있습니다.");
                continue;
            }

            if (prefabMap.ContainsKey(entry.prefabId))
            {
                Debug.LogWarning("ScenePrefabManager: 중복된 Prefab ID가 있습니다. " + entry.prefabId);
                continue;
            }

            prefabMap.Add(entry.prefabId, entry);
        }
    }

    private void CreateStartPrefabs()
    {
        for (int i = 0; i < scenePrefabs.Count; i++)
        {
            ScenePrefabEntry entry = scenePrefabs[i];

            if (entry == null || !entry.createOnStart)
                continue;

            CreatePrefab(entry.prefabId);
        }
    }

    public GameObject CreatePrefab(string prefabId)
    {
        if (!prefabMap.TryGetValue(prefabId, out ScenePrefabEntry entry))
        {
            Debug.LogWarning("ScenePrefabManager: 등록되지 않은 Prefab ID입니다. " + prefabId);
            return null;
        }

        if (entry.instance != null)
            return entry.instance;

        if (entry.prefab == null)
        {
            Debug.LogWarning("ScenePrefabManager: Prefab이 비어 있습니다. " + prefabId);
            return null;
        }

        entry.instance = Instantiate(entry.prefab);
        entry.instance.name = entry.prefab.name;
        entry.instance.SetActive(entry.activeOnStart);

        return entry.instance;
    }

    public void ShowPrefab(string prefabId)
    {
        GameObject instance = CreatePrefab(prefabId);

        if (instance != null)
            instance.SetActive(true);
    }

    public void HidePrefab(string prefabId)
    {
        if (!prefabMap.TryGetValue(prefabId, out ScenePrefabEntry entry))
            return;

        if (entry.instance != null)
            entry.instance.SetActive(false);
    }

    public void TogglePrefab(string prefabId)
    {
        GameObject instance = CreatePrefab(prefabId);

        if (instance != null)
            instance.SetActive(!instance.activeSelf);
    }

    public GameObject GetPrefabInstance(string prefabId)
    {
        if (!prefabMap.TryGetValue(prefabId, out ScenePrefabEntry entry))
            return null;

        return entry.instance;
    }

    public T GetPrefabComponent<T>(string prefabId) where T : Component
    {
        GameObject instance = GetPrefabInstance(prefabId);

        if (instance == null)
            return null;

        return instance.GetComponentInChildren<T>(true);
    }
}