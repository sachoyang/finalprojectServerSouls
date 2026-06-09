using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerStats))]
// 플레이어가 획득한 능력 목록과 액티브 키 매핑을 관리하는 컴포넌트.
// 플레이어 프리팹에 붙이고, Inspector의 abilityPool에 보상으로 나올 ScriptableObject 모듈들을 넣으면 된다.
public class PlayerAbilityInventory : MonoBehaviour
{
    // PlayerPrefs에 액티브 슬롯별 키 설정을 저장할 때 사용하는 접두어.
    private const string KeyPrefsPrefix = "PlayerAbilityKey.";

    [Header("Reward Pool")]
    // 보스 처치 보상 후보 전체 목록.
    // 이 리스트 안에서 현재 보스 단계에 맞는 모듈 3개를 랜덤으로 뽑는다.
    [SerializeField] private List<PlayerAbilityModule> abilityPool = new List<PlayerAbilityModule>();

    // true면 이미 획득한 능력은 보상 후보에서 제외한다.
    [SerializeField] private bool preventDuplicateModules = true;

    [Header("Default Active Keys")]
    // 액티브 능력을 처음 획득했을 때 자동으로 배정되는 기본 키 목록.
    // 첫 번째 액티브는 Alpha1, 두 번째 액티브는 Alpha2처럼 획득 순서대로 배정된다.
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
    // 현재 플레이어가 획득한 모든 능력.
    // 패시브와 액티브가 모두 들어간다.
    [SerializeField] private List<PlayerAbilityModule> equippedModules = new List<PlayerAbilityModule>();

    // 현재 플레이어가 획득한 액티브 능력 슬롯 목록.
    // 패시브 능력은 이 목록에 들어가지 않는다.
    [SerializeField] private List<PlayerAbilitySlot> activeSlots = new List<PlayerAbilitySlot>();

    private PlayerStats _stats;
    private PlayerAbilityExecutor _executor;
    private NetworkPlayerData _playerData;

    public IReadOnlyList<PlayerAbilityModule> EquippedModules
    {
        get
        {
            EnsureRuntimeLists();
            return equippedModules;
        }
    }

    public IReadOnlyList<PlayerAbilitySlot> ActiveSlots
    {
        get
        {
            EnsureRuntimeLists();
            return activeSlots;
        }
    }

    // UI가 보상 선택창을 띄우고 싶을 때 구독할 수 있는 이벤트.
    public event Action<IReadOnlyList<PlayerAbilityModule>> RewardOptionsGenerated;

    // 능력 장착이 끝난 뒤 UI 갱신이나 효과음 재생 등에 사용할 수 있는 이벤트.
    public event Action<PlayerAbilityModule> AbilityEquipped;

    // 키 변경 UI가 표시를 갱신할 수 있도록 알려주는 이벤트.
    public event Action<int, KeyCode> ActiveKeyChanged;

    private void Awake()
    {
        EnsureRuntimeLists();
        _stats = GetComponent<PlayerStats>();
        _executor = GetComponent<PlayerAbilityExecutor>();
        _playerData = GetComponent<NetworkPlayerData>();
        if (_executor == null)
        {
            _executor = gameObject.AddComponent<PlayerAbilityExecutor>();
        }
        LoadSavedActiveKeys();
    }

    public List<PlayerAbilityModule> GenerateRewardOptions(int bossStage, int optionCount = 3)
    {
        EnsureRuntimeLists();
        List<PlayerAbilityModule> candidates = new List<PlayerAbilityModule>();
        foreach (PlayerAbilityModule module in abilityPool)
        {
            // 빈 항목이거나 현재 보스 단계에 등장할 수 없는 능력은 제외한다.
            if (module == null || !module.CanAppearAtStage(bossStage))
            {
                continue;
            }

            // 중복 획득을 막는 설정이면 이미 가진 능력도 제외한다.
            if (preventDuplicateModules && HasModule(module))
            {
                continue;
            }

            candidates.Add(module);
        }

        Shuffle(candidates);

        // 섞은 후보 중 앞에서부터 optionCount개만 보상 선택지로 사용한다.
        int count = Mathf.Min(Mathf.Max(0, optionCount), candidates.Count);
        List<PlayerAbilityModule> options = candidates.GetRange(0, count);
        RewardOptionsGenerated?.Invoke(options);
        return options;
    }

    // 보상 선택창에서 플레이어가 능력 1개를 고르면 호출한다.
    public bool SelectRewardOption(PlayerAbilityModule module)
    {
        if (!EquipModule(module, false))
        {
            return false;
        }

        _playerData ??= GetComponent<NetworkPlayerData>();
        _playerData?.RecordAbility(module);
        return true;
    }

    public void RestoreFromPlayerData()
    {
        _playerData ??= GetComponent<NetworkPlayerData>();
        if (_playerData == null)
        {
            return;
        }

        EnsureRuntimeLists();
        for (int i = 0; i < _playerData.SavedAbilityCount; i++)
        {
            PlayerAbilityModule module = FindModuleById(_playerData.GetAbilityId(i));
            EquipModule(module, true);
        }
    }

    public void RestoreFromSessionData(PlayerRef owner)
    {
        EnsureRuntimeLists();
        IReadOnlyList<string> abilityIds = PlayerSessionStore.GetAbilityIds(owner);
        for (int i = 0; i < abilityIds.Count; i++)
        {
            PlayerAbilityModule module = FindModuleById(abilityIds[i]);
            EquipModule(module, true);
        }
    }

    private bool EquipModule(PlayerAbilityModule module, bool allowAlreadyEquipped)
    {
        EnsureRuntimeLists();
        if (module == null)
        {
            return false;
        }

        if (preventDuplicateModules && HasModule(module))
        {
            return allowAlreadyEquipped;
        }

        PlayerAbilityContext context = CreateContext();
        // 실제 장착 가능 여부는 Executor가 검사한다.
        if (_executor != null && !_executor.CanEquip(module, context))
        {
            return false;
        }

        equippedModules.Add(module);

        // 패시브는 장착 즉시 스탯 보너스와 즉시 효과를 적용한다.
        // 액티브는 슬롯에 등록하고 실제 실행은 PlayerAbilityController가 담당한다.
        _executor?.EquipModule(module, context);

        if (module.IsActive && module.SpecialEffect == PlayerAbilitySpecialEffect.None)
        {
            int slotIndex = activeSlots.Count;
            activeSlots.Add(new PlayerAbilitySlot(module, GetSavedOrDefaultKey(slotIndex)));
        }

        AbilityEquipped?.Invoke(module);
        return true;
    }

    // 특정 액티브 슬롯의 키를 바꿀 때 사용한다.
    // 변경된 키는 PlayerPrefs에 저장되므로 다음 실행 때도 유지된다.
    public bool TryChangeActiveKey(int activeSlotIndex, KeyCode newKey)
    {
        EnsureRuntimeLists();
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

    // PlayerAbilityController가 슬롯 번호로 실제 슬롯을 찾을 때 사용한다.
    public PlayerAbilitySlot GetActiveSlot(int activeSlotIndex)
    {
        EnsureRuntimeLists();
        return activeSlotIndex >= 0 && activeSlotIndex < activeSlots.Count ? activeSlots[activeSlotIndex] : null;
    }

    public List<SkillSlotUIData> GetSkillSlotUIData(float currentTime)
    {
        // HUD는 슬롯 내부 구조를 직접 조립하지 않고, 여기서 만든 표시용 데이터만 읽는다.
        EnsureRuntimeLists();
        List<SkillSlotUIData> slots = new List<SkillSlotUIData>(activeSlots.Count);

        for (int i = 0; i < activeSlots.Count; i++)
        {
            PlayerAbilitySlot slot = activeSlots[i];
            PlayerAbilityModule module = slot?.Module;
            if (slot == null || module == null)
            {
                slots.Add(SkillSlotUIData.Empty);
                continue;
            }

            slots.Add(new SkillSlotUIData(
                false,
                module.AbilityId,
                module.DisplayName,
                module.Icon,
                slot.KeyCode,
                Mathf.Max(0f, slot.NextReadyTime - currentTime),
                module.CooldownSeconds));
        }

        return slots;
    }

    // 모듈 함수에 넘겨줄 플레이어 실행 정보를 만든다.
    public PlayerAbilityContext CreateContext()
    {
        NetworkPlayerController controller = GetComponent<NetworkPlayerController>();
        return new PlayerAbilityContext(gameObject, _stats, controller != null ? controller.Runner : null);
    }

    // 같은 abilityId를 가진 모듈을 이미 획득했는지 확인한다.
    public PlayerAbilityModule FindModuleById(string abilityId)
    {
        if (string.IsNullOrWhiteSpace(abilityId))
        {
            return null;
        }

        EnsureRuntimeLists();
        foreach (PlayerAbilityModule module in abilityPool)
        {
            if (module != null && module.AbilityId == abilityId)
            {
                return module;
            }
        }

        foreach (PlayerAbilityModule module in equippedModules)
        {
            if (module != null && module.AbilityId == abilityId)
            {
                return module;
            }
        }

        return null;
    }

    private bool HasModule(PlayerAbilityModule module)
    {
        EnsureRuntimeLists();
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

    // 저장된 키가 있으면 저장값을 사용하고, 없으면 기본 키 배열에서 가져온다.
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

    // Inspector에서 미리 들어있는 activeSlots가 있을 경우 저장된 키 설정을 반영한다.
    private void LoadSavedActiveKeys()
    {
        EnsureRuntimeLists();
        for (int i = 0; i < activeSlots.Count; i++)
        {
            activeSlots[i].SetKey(GetSavedOrDefaultKey(i));
        }
    }

    private static string GetPrefsKey(int activeSlotIndex)
    {
        return $"{KeyPrefsPrefix}{activeSlotIndex}";
    }

    private void EnsureRuntimeLists()
    {
        abilityPool ??= new List<PlayerAbilityModule>();
        equippedModules ??= new List<PlayerAbilityModule>();
        activeSlots ??= new List<PlayerAbilitySlot>();
        defaultActiveKeys ??= Array.Empty<KeyCode>();
    }

    // 보상 후보를 랜덤 순서로 섞기 위한 Fisher-Yates 셔플.
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
