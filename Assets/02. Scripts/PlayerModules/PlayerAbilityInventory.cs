using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerStats))]
public class PlayerAbilityInventory : MonoBehaviour
{
    private const string KeyPrefsPrefix = "PlayerAbilityKey.";

    [Header("Reward Pool")]
    [SerializeField] private List<PlayerAbilityModule> abilityPool = new List<PlayerAbilityModule>();
    [SerializeField] private bool preventDuplicateModules = true;

    [Header("Default Active Keys")]
    [SerializeField] private KeyCode[] defaultActiveKeys =
    {
        KeyCode.Alpha1,
        KeyCode.Alpha2,
        KeyCode.Alpha3,
        KeyCode.Alpha4,
        KeyCode.Alpha5,
        KeyCode.Alpha6,
        KeyCode.Alpha7,
        KeyCode.Alpha8
    };

    [Header("Runtime")]
    [SerializeField] private List<PlayerAbilityModule> equippedModules = new List<PlayerAbilityModule>();
    [SerializeField] private List<PlayerAbilitySlot> activeSlots = new List<PlayerAbilitySlot>();

    private PlayerStats _stats;

    public IReadOnlyList<PlayerAbilityModule> EquippedModules => equippedModules;
    public IReadOnlyList<PlayerAbilitySlot> ActiveSlots => activeSlots;

    public event Action<IReadOnlyList<PlayerAbilityModule>> RewardOptionsGenerated;
    public event Action<PlayerAbilityModule> AbilityEquipped;
    public event Action<int, KeyCode> ActiveKeyChanged;

    private void Awake()
    {
        _stats = GetComponent<PlayerStats>();
        LoadSavedActiveKeys();
    }

    public List<PlayerAbilityModule> GenerateRewardOptions(int bossStage, int optionCount = 3)
    {
        List<PlayerAbilityModule> candidates = new List<PlayerAbilityModule>();
        foreach (PlayerAbilityModule module in abilityPool)
        {
            if (module == null || !module.CanAppearAtStage(bossStage))
            {
                continue;
            }

            if (preventDuplicateModules && HasModule(module))
            {
                continue;
            }

            candidates.Add(module);
        }

        Shuffle(candidates);

        int count = Mathf.Min(Mathf.Max(0, optionCount), candidates.Count);
        List<PlayerAbilityModule> options = candidates.GetRange(0, count);
        RewardOptionsGenerated?.Invoke(options);
        return options;
    }

    public bool SelectRewardOption(PlayerAbilityModule module)
    {
        if (module == null || (preventDuplicateModules && HasModule(module)))
        {
            return false;
        }

        PlayerAbilityContext context = CreateContext();
        if (!module.CanEquip(context))
        {
            return false;
        }

        equippedModules.Add(module);
        module.OnEquipped(context);

        if (module.IsActive)
        {
            int slotIndex = activeSlots.Count;
            activeSlots.Add(new PlayerAbilitySlot(module, GetSavedOrDefaultKey(slotIndex)));
        }

        AbilityEquipped?.Invoke(module);
        return true;
    }

    public bool TryChangeActiveKey(int activeSlotIndex, KeyCode newKey)
    {
        if (activeSlotIndex < 0 || activeSlotIndex >= activeSlots.Count || newKey == KeyCode.None)
        {
            return false;
        }

        activeSlots[activeSlotIndex].SetKey(newKey);
        PlayerPrefs.SetString(GetPrefsKey(activeSlotIndex), newKey.ToString());
        PlayerPrefs.Save();
        ActiveKeyChanged?.Invoke(activeSlotIndex, newKey);
        return true;
    }

    public PlayerAbilitySlot GetActiveSlot(int activeSlotIndex)
    {
        return activeSlotIndex >= 0 && activeSlotIndex < activeSlots.Count ? activeSlots[activeSlotIndex] : null;
    }

    public PlayerAbilityContext CreateContext()
    {
        NetworkPlayerController controller = GetComponent<NetworkPlayerController>();
        return new PlayerAbilityContext(gameObject, _stats, controller != null ? controller.Runner : null);
    }

    private bool HasModule(PlayerAbilityModule module)
    {
        string abilityId = module.AbilityId;
        foreach (PlayerAbilityModule equipped in equippedModules)
        {
            if (equipped != null && equipped.AbilityId == abilityId)
            {
                return true;
            }
        }

        return false;
    }

    private KeyCode GetSavedOrDefaultKey(int activeSlotIndex)
    {
        string saved = PlayerPrefs.GetString(GetPrefsKey(activeSlotIndex), string.Empty);
        if (!string.IsNullOrEmpty(saved) && Enum.TryParse(saved, out KeyCode savedKey))
        {
            return savedKey;
        }

        return activeSlotIndex >= 0 && activeSlotIndex < defaultActiveKeys.Length
            ? defaultActiveKeys[activeSlotIndex]
            : KeyCode.None;
    }

    private void LoadSavedActiveKeys()
    {
        for (int i = 0; i < activeSlots.Count; i++)
        {
            activeSlots[i].SetKey(GetSavedOrDefaultKey(i));
        }
    }

    private static string GetPrefsKey(int activeSlotIndex)
    {
        return $"{KeyPrefsPrefix}{activeSlotIndex}";
    }

    private static void Shuffle<T>(IList<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            T temp = list[i];
            list[i] = list[j];
            list[j] = temp;
        }
    }
}
