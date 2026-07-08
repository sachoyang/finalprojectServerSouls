using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerStats))]
// 플레이어가 획득한 능력 목록과 액티브 키 매핑을 관리하는 컴포넌트.
// 스킬 원본 데이터는 로그인 후 AbilityManager가 DB에서 조립한 카탈로그를 사용한다.
public class PlayerAbilityInventory : MonoBehaviour
{
    // PlayerPrefs에 액티브 슬롯별 키 설정을 저장할 때 사용하는 접두어.
    private const string KeyPrefsPrefix = "PlayerAbilityKey.";

    [Header("Default Active Keys")]
    // 액티브 능력을 처음 획득했을 때 자동으로 배정되는 기본 키 목록.
    // 첫 번째 액티브는 Alpha1, 두 번째 액티브는 Alpha2처럼 획득 순서대로 배정된다.
    [SerializeField]
    private KeyCode[] defaultActiveKeys =
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
    [SerializeField] private List<int> equippedLevels = new List<int>();

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

    // 능력 장착이 끝난 뒤 UI 갱신이나 효과음 재생 등에 사용할 수 있는 이벤트.
    public event Action<PlayerAbilityModule> AbilityEquipped;
    public event Action<PlayerAbilityModule, int> AbilityLevelChanged;

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
        AbilityManager abilityManager = AbilityManager.HasInstance ? AbilityManager.Instance : null;
        if (abilityManager == null || !abilityManager.IsLoaded)
        {
            Debug.LogWarning("[PlayerAbilityInventory] AbilityManager가 아직 DB 스킬 데이터를 준비하지 못했습니다.");
            return new List<PlayerAbilityModule>();
        }

        List<PlayerAbilityModule> candidates = new List<PlayerAbilityModule>();
        foreach (PlayerAbilityModule module in abilityManager.GetUnlockedAbilitiesList(GetUnlockedSkillsBitmask()))
        {
            // 빈 항목이거나 현재 보스 단계에 등장할 수 없는 능력은 제외한다.
            if (module == null || !module.UnlockedSkill || !module.CanAppearAtStage(bossStage))
            {
                continue;
            }

            // 최대 레벨 스킬만 후보에서 제외하고, Lv.1~3은 중복 등장해 레벨업할 수 있다.
            if (GetAbilityLevel(module) >= module.MaxLevel)
            {
                continue;
            }

            candidates.Add(module);
        }

        Shuffle(candidates);

        // 섞은 후보 중 앞에서부터 optionCount개만 보상 선택지로 사용한다.
        int count = Mathf.Min(Mathf.Max(0, optionCount), candidates.Count);
        List<PlayerAbilityModule> options = candidates.GetRange(0, count);
        return options;
    }

    // 보상 선택창에서 플레이어가 능력 1개를 고르면 호출한다.
    public bool SelectRewardOption(PlayerAbilityModule module)
    {
        int nextLevel = GetAbilityLevel(module) + 1;
        if (module == null || nextLevel > module.MaxLevel ||
            !SetAbilityLevel(module, nextLevel, true))
        {
            return false;
        }

        _playerData ??= GetComponent<NetworkPlayerData>();
        _playerData?.RecordAbility(module, nextLevel);
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
            SetAbilityLevel(module, _playerData.GetAbilityLevel(i), false);
        }
    }

    public void RestoreFromSessionData(PlayerRef owner)
    {
        EnsureRuntimeLists();
        IReadOnlyList<PlayerSessionStore.AbilityState> abilities = PlayerSessionStore.GetAbilities(owner);
        for (int i = 0; i < abilities.Count; i++)
        {
            PlayerAbilityModule module = FindModuleById(abilities[i].AbilityId);
            SetAbilityLevel(module, abilities[i].Level, false);
        }
    }

    public bool ApplyServerReward(PlayerAbilityModule module)
    {
        int nextLevel = GetAbilityLevel(module) + 1;
        return module != null && nextLevel <= module.MaxLevel &&
               SetAbilityLevel(module, nextLevel, true);
    }

    private bool SetAbilityLevel(PlayerAbilityModule module, int targetLevel, bool applyAcquireEffects)
    {
        EnsureRuntimeLists();
        if (module == null)
        {
            return false;
        }

        targetLevel = Mathf.Clamp(targetLevel, 1, module.MaxLevel);
        int moduleIndex = FindEquippedModuleIndex(module.AbilityId);
        int currentLevel = moduleIndex >= 0 ? equippedLevels[moduleIndex] : 0;
        if (targetLevel <= currentLevel)
        {
            return true;
        }

        PlayerAbilityContext context = CreateContext();
        if (currentLevel == 0 && _executor != null && !_executor.CanEquip(module, context))
        {
            return false;
        }

        if (moduleIndex < 0)
        {
            equippedModules.Add(module);
            equippedLevels.Add(targetLevel);
            moduleIndex = equippedModules.Count - 1;

            if (module.UsesActiveSlot && module.SpecialEffect == PlayerAbilitySpecialEffect.None)
            {
                int slotIndex = activeSlots.Count;
                activeSlots.Add(new PlayerAbilitySlot(module, GetSavedOrDefaultKey(slotIndex), targetLevel));
            }
        }
        else
        {
            equippedLevels[moduleIndex] = targetLevel;
            UpdateActiveSlotLevel(module.AbilityId, targetLevel);
        }

        // 패시브는 이전 레벨 총 보너스와 새 레벨 총 보너스의 차이만 적용한다.
        // 씬 이동 복구에서는 PlayerStats 스냅샷에 이미 누적값이 있으므로 효과를 재적용하지 않는다.
        if (applyAcquireEffects)
        {
            _executor?.ApplyLevelChange(module, context, currentLevel, targetLevel);
        }
        else
        {
            _executor?.RestoreModule(module, context);
        }

        if (currentLevel == 0)
        {
            AbilityEquipped?.Invoke(module);
        }

        AbilityLevelChanged?.Invoke(module, targetLevel);
        return true;
    }

    public int GetAbilityLevel(PlayerAbilityModule module)
    {
        return module != null ? GetAbilityLevel(module.AbilityId) : 0;
    }

    public int GetAbilityLevel(string abilityId)
    {
        EnsureRuntimeLists();
        int index = FindEquippedModuleIndex(abilityId);
        return index >= 0 ? equippedLevels[index] : 0;
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
                slot.CooldownSeconds));
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

        AbilityManager abilityManager = AbilityManager.HasInstance ? AbilityManager.Instance : null;
        PlayerAbilityModule dbModule = abilityManager != null ? abilityManager.FindByAbilityId(abilityId) : null;
        if (dbModule != null)
        {
            return dbModule;
        }

        EnsureRuntimeLists();
        foreach (PlayerAbilityModule module in equippedModules)
        {
            if (module != null && module.AbilityId == abilityId)
            {
                return module;
            }
        }

        return null;
    }

    private long GetUnlockedSkillsBitmask()
    {
        // 1. 네트워크로 동기화된 플레이어 데이터가 있으면 그 값을 최우선으로 사용
        _playerData ??= GetComponent<NetworkPlayerData>();
        if (_playerData != null && _playerData.UnlockedSkillsBitmask != 0)
        {
            return _playerData.UnlockedSkillsBitmask;
        }

        // 2. 내 로컬 플레이어(InputAuthority)라면, 서버 로그인 시 받아둔 값을 바로 사용!
        if (_playerData == null || (_playerData.Object != null && _playerData.Object.HasInputAuthority))
        {
            return BackendManager.HasInstance ? BackendManager.Instance.CurrentSkillsBitmask : 0L;
        }

        return 0L;
    }

    private bool HasModule(PlayerAbilityModule module)
    {
        return module != null && FindEquippedModuleIndex(module.AbilityId) >= 0;
    }

    private int FindEquippedModuleIndex(string abilityId)
    {
        if (string.IsNullOrWhiteSpace(abilityId))
        {
            return -1;
        }

        for (int i = 0; i < equippedModules.Count; i++)
        {
            PlayerAbilityModule equipped = equippedModules[i];
            if (equipped != null && equipped.AbilityId == abilityId)
            {
                return i;
            }
        }

        return -1;
    }

    private void UpdateActiveSlotLevel(string abilityId, int level)
    {
        for (int i = 0; i < activeSlots.Count; i++)
        {
            PlayerAbilitySlot slot = activeSlots[i];
            if (slot?.Module != null && slot.Module.AbilityId == abilityId)
            {
                slot.SetLevel(level);
                return;
            }
        }
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
        equippedModules ??= new List<PlayerAbilityModule>();
        equippedLevels ??= new List<int>();
        activeSlots ??= new List<PlayerAbilitySlot>();
        defaultActiveKeys ??= Array.Empty<KeyCode>();

        while (equippedLevels.Count < equippedModules.Count)
        {
            equippedLevels.Add(1);
        }

        if (equippedLevels.Count > equippedModules.Count)
        {
            equippedLevels.RemoveRange(equippedModules.Count, equippedLevels.Count - equippedModules.Count);
        }

        for (int i = 0; i < equippedLevels.Count; i++)
        {
            PlayerAbilityModule module = equippedModules[i];
            int maxLevel = module != null ? module.MaxLevel : 1;
            equippedLevels[i] = Mathf.Clamp(equippedLevels[i], 1, maxLevel);
        }
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
