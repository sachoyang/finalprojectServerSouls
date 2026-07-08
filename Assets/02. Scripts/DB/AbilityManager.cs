using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

// ==========================================
// 1. 서버 JSON 규격에 맞춘 데이터 클래스 (타입 분리 + 레벨별 데이터)
//  스킬 모듈이 Active/Passive/Utility로 분리되면서 DB도 타입별 테이블로 나뉘었다.
//  단, Unity로 내려올 때는 한 스킬 = 한 오브젝트로 묶여서 온다.
//  (연출값(애니/VFX/사운드/히트박스)은 DB에 없다 → 로컬 에셋 유지)
//  자세한 규격: Assets/02. Scripts/DB/DBability_README.md,  루트 SKILL_DB_SERVER_SPEC.md
// ==========================================

// 스킬 한 개의 '레벨 한 줄'. 타입에 따라 쓰이는 필드만 채워지고 나머지는 0/빈값(JsonUtility 안전).
[Serializable]
public class AbilityLevelDBData
{
    public int level;

    // Active: Hit Event damageRate에 곱할 레벨별 배율
    public float skill_multiplier;

    // Passive: 레벨별 '최종' 증가값 (차이값 아님). defense/attack은 퍼센트 입력값(10 = 10%)
    public float max_health_bonus;
    public float max_stamina_bonus;
    public float defense_bonus_percent;
    public float attack_damage_bonus_percent;

    // Utility: 레벨별 회복량
    public float health_restore_amount;
    public float stamina_restore_amount;
}

// 레벨 배열을 JSON 문자열로 주고받을 때 쓰는 래퍼 (업로드 시 levels_json 필드로 전송)
[Serializable]
public class AbilityLevelDBList
{
    public List<AbilityLevelDBData> levels = new List<AbilityLevelDBData>();
}

// 스킬 한 개 전체 (공통 + 타입별 레벨무관 기본값 + 레벨 배열)
[Serializable]
public class AbilityDBData
{
    // 공통 (abilities 테이블)
    public string ability_id;
    public string ability_type;   // "Active" / "Passive" / "Utility"
    public int bit_index;
    public string display_name;
    public string description;     // 토큰 포함 설명문
    public int appear_stage;       // 몇 스테이지부터 등장하는지
    public int basic_skill;        // 기본 스킬 여부 (0/1)
    public int unlocked_skill;     // 기본 해금 여부 (0/1)
    public int max_level;

    // 타입별 레벨무관 기본값 (active_abilities / utility_abilities)
    //  - Passive는 이 값들을 안 쓴다(0/빈값으로 옴).
    public float cooldown_seconds;
    public float stamina_cost;
    public string special_effect;  // Utility 전용 (enum 이름)

    // 레벨별 값 (active/passive/utility _levels 테이블)
    public List<AbilityLevelDBData> levels;
}

[Serializable]
public class AbilityDBResponse
{
    public string status;
    public List<AbilityDBData> data;
}

// 2. 능력 모듈 생성 및 비트마스크 연동 매니저
public class AbilityManager : MonoSingleton<AbilityManager>
{
    [Header("스킬 수치 동기화")]
    [Tooltip("끄면 서버 Bake 수치를 받지 않고 Resources/SkillModule의 로컬 값만 사용합니다.")]
    [SerializeField] private bool useServerAbilityBalance;

    // bit_index를 Key로 사용하는 전체 스킬 딕셔너리
    public Dictionary<int, PlayerAbilityModule> AllAbilitiesDict { get; private set; } = new Dictionary<int, PlayerAbilityModule>();
    public Dictionary<string, PlayerAbilityModule> AllAbilitiesById { get; private set; } = new Dictionary<string, PlayerAbilityModule>();
    public bool IsLoaded { get; private set; }

    [Header("디버그용: 현재 로드된 스킬 목록")]
    [SerializeField] private List<PlayerAbilityModule> _debugLoadedAbilities = new List<PlayerAbilityModule>();

    protected override void Awake()
    {
        base.Awake();

        // [로컬 스킬 풀 초기화]
        // 서버 로그인 여부와 관계없이 게임 시작 시 Resources/SkillModule 안의 에셋을 먼저 읽는다.
        // 애니메이션, VFX, 히트박스, 사운드 같은 Unity 에셋 참조는 이 로컬 모듈이 원본으로 보관한다.
        LoadLocalAbilityCatalog();
    }

    // ==========================================
    // 📦 로컬 SkillModule을 기본 스킬 풀로 등록하기
    // ==========================================
    private void LoadLocalAbilityCatalog()
    {
        PlayerAbilityModule[] localModules = LoadLocalSkillModules();

        AllAbilitiesDict.Clear();
        AllAbilitiesById.Clear();
        _debugLoadedAbilities.Clear();

        foreach (PlayerAbilityModule module in localModules)
        {
            RegisterAbilityModule(module);
        }

        // 서버에 접속하지 않아도 로컬 에셋이 하나 이상 있으면 스킬 시스템을 사용할 수 있다.
        IsLoaded = AllAbilitiesById.Count > 0;

        if (IsLoaded)
        {
            Debug.Log(
                $"<color=cyan>[AbilityManager] 로컬 SkillModule {AllAbilitiesById.Count}개를 기본 스킬 풀로 등록했습니다.</color>");
        }
        else
        {
            Debug.LogWarning(
                "[AbilityManager] Resources/SkillModule에서 PlayerAbilityModule을 찾지 못했습니다.");
        }
    }

    private static PlayerAbilityModule[] LoadLocalSkillModules()
    {
        List<PlayerAbilityModule> modules = new List<PlayerAbilityModule>();
        LoadLocalSkillModules("SkillModule/ActiveSkill", modules);
        LoadLocalSkillModules("SkillModule/PassiveSkill", modules);
        LoadLocalSkillModules("SkillModule/UtilitySkill", modules);
        return modules.ToArray();
    }

    private static void LoadLocalSkillModules(string resourcesPath, List<PlayerAbilityModule> modules)
    {
        PlayerAbilityModule[] loadedModules = Resources.LoadAll<PlayerAbilityModule>(resourcesPath);
        for (int i = 0; i < loadedModules.Length; i++)
        {
            if (loadedModules[i] != null && !modules.Contains(loadedModules[i]))
            {
                modules.Add(loadedModules[i]);
            }
        }
    }

    // 로컬 로드와 서버 최신화 후 인덱스 재구성에서 공통으로 사용하는 등록 함수다.
    private void RegisterAbilityModule(PlayerAbilityModule module)
    {
        if (module == null || string.IsNullOrWhiteSpace(module.AbilityId))
        {
            return;
        }

        if (AllAbilitiesById.ContainsKey(module.AbilityId))
        {
            Debug.LogWarning(
                $"[AbilityManager] 중복된 AbilityId '{module.AbilityId}'가 있어 뒤에 읽은 모듈로 교체합니다.");
        }

        if (AllAbilitiesDict.TryGetValue(module.BitIndex, out PlayerAbilityModule existingModule) &&
            existingModule != null &&
            existingModule.AbilityId != module.AbilityId)
        {
            Debug.LogWarning(
                $"[AbilityManager] BitIndex {module.BitIndex}가 '{existingModule.AbilityId}'와 '{module.AbilityId}'에서 중복됩니다.");
        }

        AllAbilitiesDict[module.BitIndex] = module;
        AllAbilitiesById[module.AbilityId] = module;

        if (!_debugLoadedAbilities.Contains(module))
        {
            _debugLoadedAbilities.Add(module);
        }
    }

    // 서버 데이터가 BitIndex나 AbilityId를 최신 값으로 바꿀 수 있으므로 딕셔너리 키도 다시 맞춘다.
    private void RebuildCatalogIndexes()
    {
        PlayerAbilityModule[] modules = _debugLoadedAbilities.ToArray();

        AllAbilitiesDict.Clear();
        AllAbilitiesById.Clear();
        _debugLoadedAbilities.Clear();

        foreach (PlayerAbilityModule module in modules)
        {
            RegisterAbilityModule(module);
        }

        IsLoaded = AllAbilitiesById.Count > 0;
    }

    // ==========================================
    // 🌐 서버 데이터로 로컬 SkillModule의 수치만 최신화하기
    // ==========================================
    public void FetchAbilities(Action<bool> onComplete = null)
    {
        // 서버 요청 중에도 이미 등록된 로컬 스킬 풀은 계속 사용할 수 있게 IsLoaded를 끄지 않는다.
        if (!IsLoaded)
        {
            LoadLocalAbilityCatalog();
        }

        if (!useServerAbilityBalance)
        {
            Debug.Log(
                "<color=yellow>[AbilityManager] 서버 스킬 Bake 동기화가 꺼져 있어 로컬 SkillModule 수치만 사용합니다.</color>");
            ApplyRewardPoolFromBitmask();
            onComplete?.Invoke(IsLoaded);
            return;
        }

        StartCoroutine(FetchRoutine(onComplete));
    }

    private IEnumerator FetchRoutine(Action<bool> onComplete)
    {
        BackendManager backendManager =
            BackendManager.HasInstance ? BackendManager.Instance : null;
        if (backendManager == null)
        {
            Debug.LogWarning("[AbilityManager] BackendManager가 없어 로컬 SkillModule만 사용합니다.");
            onComplete?.Invoke(IsLoaded);
            yield break;
        }

        string url = backendManager.BASE_URL + "get_abilities.php";

        using (UnityWebRequest www = UnityWebRequest.Get(url))
        {
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                AbilityDBResponse res = JsonUtility.FromJson<AbilityDBResponse>(www.downloadHandler.text);

                if (res != null && res.status == "success" && res.data != null)
                {
                    foreach (AbilityDBData dbData in res.data)
                    {
                        if (dbData == null || string.IsNullOrWhiteSpace(dbData.ability_id))
                        {
                            Debug.LogWarning("[AbilityManager] AbilityId가 없는 서버 스킬 데이터는 건너뜁니다.");
                            continue;
                        }

                        // 서버는 Unity 에셋 참조를 전달하지 않는다.
                        // 시작 시 등록한 로컬 모듈을 AbilityId로 찾아 밸런스 수치만 덮어쓴다.
                        PlayerAbilityModule bakedModule =
                            FindByAbilityId(dbData.ability_id);

                        if (bakedModule != null)
                        {
                            // InitializeFromDB는 데미지, 쿨타임 같은 서버 관리 값만 변경한다.
                            // 로컬에 연결된 애니메이션, VFX, 히트박스, 사운드 참조는 그대로 유지된다.
                            bakedModule.InitializeFromDB(dbData);
                        }
                        else
                        {
                            Debug.LogWarning(
                                $"[AbilityManager] 서버 스킬 '{dbData.ability_id}'에 대응하는 로컬 SkillModule이 없습니다. Bake 후 에셋 참조를 설정해주세요.");
                        }
                    }

                    // 서버가 bit_index를 변경했을 가능성이 있으므로 최신 모듈 값으로 인덱스를 다시 만든다.
                    RebuildCatalogIndexes();

                    Debug.Log(
                        $"<color=green>[AbilityManager] 서버 수치 동기화 완료! 로컬 SkillModule {AllAbilitiesDict.Count}개를 사용합니다.</color>");

                    // DB 기본 스킬과 로그인 유저의 개인 해금 비트를 합쳐 최종 보상 풀을 갱신한다.
                    ApplyRewardPoolFromBitmask();

                    onComplete?.Invoke(true);
                }
                else
                {
                    // 서버 응답 형식이 잘못되어도 로컬 에셋이 준비되어 있으면 해당 데이터로 계속 진행한다.
                    Debug.LogWarning(
                        "[AbilityManager] 서버 스킬 응답이 올바르지 않아 로컬 SkillModule을 사용합니다.");
                    onComplete?.Invoke(IsLoaded);
                }
            }
            else
            {
                // 서버 최신화에 실패해도 로컬 SkillModule은 이미 준비되어 있으므로 게임 진행은 가능하다.
                Debug.LogWarning(
                    $"[AbilityManager] 서버 스킬 최신화 실패. 로컬 SkillModule을 사용합니다: {www.error}");
                onComplete?.Invoke(IsLoaded);
            }
        }
    }

    // ==========================================
    // ⚔️ 유저의 비트마스크를 해독하여 보유한 스킬 리스트 반환
    // ==========================================
    public List<PlayerAbilityModule> GetUnlockedAbilitiesList(long userBitmask)
    {
        List<PlayerAbilityModule> unlockedList = new List<PlayerAbilityModule>();

        // 로그인하지 않은 로컬 실행에서는 서버 비트마스크가 없다.
        // 이때는 SkillModule 에셋에서 Unlocked Skill이 켜진 항목을 로컬 스킬 풀로 사용한다.
        bool usesLocalPool =
            !BackendManager.HasInstance ||
            string.IsNullOrWhiteSpace(BackendManager.Instance.CurrentLoginID);

        // 0번 비트부터 63번 비트까지 검사
        foreach (var kvp in AllAbilitiesDict)
        {
            int bitIndex = kvp.Key;
            PlayerAbilityModule module = kvp.Value;

            if (module == null)
            {
                continue;
            }

            // 로컬 실행은 에셋의 테스트 풀 설정을 사용한다.
            // 로그인 상태에서는 DB의 전체 유저 기본 스킬 또는 개인 해금 비트를 사용한다.
            if ((usesLocalPool && module.UnlockedSkill) ||
                (!usesLocalPool &&
                 (module.BasicSkill || (userBitmask & (1L << bitIndex)) != 0)))
            {
                unlockedList.Add(module);
            }
        }

        return unlockedList;
    }

    // 모든 유저 공통 Basic Skill과 유저별 해금 비트를 합쳐
    // 런타임 UnlockedSkill 값을 갱신한다.
    public void ApplyRewardPoolFromBitmask()
    {
        long userBitmask = BackendManager.HasInstance
            ? BackendManager.Instance.CurrentSkillsBitmask
            : 0L;
        ApplyRewardPoolFromBitmask(userBitmask);
    }

    public void ApplyRewardPoolFromBitmask(long userBitmask)
    {
        foreach (var kvp in AllAbilitiesDict)
        {
            PlayerAbilityModule module = kvp.Value;
            if (module == null)
            {
                continue;
            }

            bool personallyUnlocked =
                (userBitmask & (1L << module.BitIndex)) != 0L;
            module.SetUnlockedSkill(
                module.BasicSkill || personallyUnlocked);
        }
    }

    // ==========================================
    // 🌟 [핵심] 런타임 신규 해금 + 서버 영구 저장 통합 함수
    // 1) 비트마스크에 OR 연산으로 추가  2) SO 즉시 갱신  3) 서버로 영구 저장
    // ==========================================
    public void UnlockAbilityAndSync(PlayerAbilityModule module)
    {
        if (module == null)
        {
            Debug.LogWarning("[AbilityManager] UnlockAbilityAndSync: module이 null입니다.");
            return;
        }

        if (!BackendManager.HasInstance)
        {
            Debug.LogWarning("[AbilityManager] UnlockAbilityAndSync: BackendManager가 없어 서버 저장을 건너뜁니다.");
            return;
        }

        int bitIndex = module.BitIndex;
        if (bitIndex < 0 || bitIndex >= 63)
        {
            Debug.LogWarning($"[AbilityManager] {module.AbilityId}의 BitIndex({bitIndex})가 범위를 벗어나 동기화할 수 없습니다.");
            return;
        }

        // 1. 로컬 비트마스크에 OR 연산으로 추가 (해당 비트 1로 켬)
        long newBitmask = BackendManager.Instance.CurrentSkillsBitmask | (1L << bitIndex);
        BackendManager.Instance.SetCurrentSkillsBitmask(newBitmask);

        // 2. 개인 해금 결과를 런타임 보상 풀에 즉시 반영
        module.SetUnlockedSkill(true);

        // 3. 변경된 비트마스크를 서버로 즉시 영구 저장
        BackendManager.Instance.UpdateSkills(newBitmask);
    }

    public PlayerAbilityModule FindByAbilityId(string abilityId)
    {
        if (string.IsNullOrWhiteSpace(abilityId))
        {
            return null;
        }

        return AllAbilitiesById.TryGetValue(abilityId, out PlayerAbilityModule module)
            ? module
            : null;
    }
}
