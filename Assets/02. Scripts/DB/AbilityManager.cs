using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

// 1. 서버 JSON 규격에 맞춘 데이터 클래스
[Serializable]
public class AbilityDBData
{
    public int bit_index;
    public string ability_id;
    public string display_name;
    public string description;
    public string ability_type;
    public float stamina_cost;
    public float cooldown_seconds;
    public float damage_multiplier;
    public float duration;
    public string special_effect;
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
    // bit_index를 Key로 사용하는 전체 스킬 딕셔너리
    public Dictionary<int, PlayerAbilityModule> AllAbilitiesDict { get; private set; } = new Dictionary<int, PlayerAbilityModule>();
    public Dictionary<string, PlayerAbilityModule> AllAbilitiesById { get; private set; } = new Dictionary<string, PlayerAbilityModule>();
    public bool IsLoaded { get; private set; }

    [Header("디버그용: 현재 로드된 스킬 목록")]
    [SerializeField] private List<PlayerAbilityModule> _debugLoadedAbilities = new List<PlayerAbilityModule>();

    protected override void Awake()
    {
        base.Awake();
    }

    // ==========================================
    // 🌐 서버에서 스킬 목록 가져와서 조립하기
    // ==========================================
    public void FetchAbilities(Action<bool> onComplete = null)
    {
        IsLoaded = false;
        StartCoroutine(FetchRoutine(onComplete));
    }

    private IEnumerator FetchRoutine(Action<bool> onComplete)
    {
        string url = BackendManager.Instance.BASE_URL + "get_abilities.php";

        using (UnityWebRequest www = UnityWebRequest.Get(url))
        {
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                AbilityDBResponse res = JsonUtility.FromJson<AbilityDBResponse>(www.downloadHandler.text);

                if (res.status == "success")
                {
                    AllAbilitiesDict.Clear();
                    AllAbilitiesById.Clear();
                    _debugLoadedAbilities.Clear();
                    IsLoaded = false;

                    foreach (AbilityDBData dbData in res.data)
                    {
                        // 🔥 지정된 경로(SkillModule)에서 미리 만들어둔 SO를 로드합니다.
                        PlayerAbilityModule bakedModule = Resources.Load<PlayerAbilityModule>($"SkillModule/{dbData.ability_id}");

                        if (bakedModule != null)
                        {
                            // 라이브 업데이트: 서버에서 바뀐 최신 데미지/쿨타임 수치만 덮어씌움! (클라이언트 패치 불필요)
                            bakedModule.InitializeFromDB(dbData);

                            AllAbilitiesDict[dbData.bit_index] = bakedModule;
                            if (!string.IsNullOrWhiteSpace(dbData.ability_id))
                            {
                                AllAbilitiesById[dbData.ability_id] = bakedModule;
                            }
                            _debugLoadedAbilities.Add(bakedModule);
                        }
                        else
                        {
                            Debug.LogWarning($"[AbilityManager] '{dbData.ability_id}' 파일을 찾을 수 없습니다. 에디터에서 Bake 툴을 먼저 돌려주세요!");
                        }
                    }

                    Debug.Log($"<color=green>[AbilityManager] 서버 데이터 동기화 완료! {AllAbilitiesDict.Count}개 스킬 준비됨.</color>");
                    IsLoaded = true;

                    // 🌟 조립이 끝나면 로그인 시 받아둔 유저 비트마스크를 해석해 각 SO의 보상 풀 포함 여부를 갱신
                    ApplyRewardPoolFromBitmask();

                    onComplete?.Invoke(true);
                }
                else
                {
                    onComplete?.Invoke(false);
                }
            }
            else
            {
                Debug.LogError("스킬 로드 실패: " + www.error);
                onComplete?.Invoke(false);
            }
        }
    }

    // ==========================================
    // ⚔️ 유저의 비트마스크를 해독하여 보유한 스킬 리스트 반환
    // ==========================================
    public List<PlayerAbilityModule> GetUnlockedAbilitiesList(long userBitmask)
    {
        List<PlayerAbilityModule> unlockedList = new List<PlayerAbilityModule>();

        // 0번 비트부터 63번 비트까지 검사
        foreach (var kvp in AllAbilitiesDict)
        {
            int bitIndex = kvp.Key;

            // 비트 연산 (AND): 해당 자리가 1인지 확인
            if ((userBitmask & (1L << bitIndex)) != 0)
            {
                unlockedList.Add(kvp.Value);
            }
        }

        return unlockedList;
    }

    // ==========================================
    // 🌟 [핵심] 유저 비트마스크 해석 → SO의 includeInRewardPool 런타임 갱신
    // 서버는 "기본 해금 + 유저 추가 해금"이 합산된 단일 비트마스크 하나만 던져준다.
    // ==========================================
    public void ApplyRewardPoolFromBitmask()
    {
        long userBitmask = BackendManager.HasInstance ? BackendManager.Instance.CurrentSkillsBitmask : 0L;
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

            // 해당 비트가 켜져 있으면 true, 꺼져 있으면 false로 덮어쓴다.
            bool unlocked = (userBitmask & (1L << module.BitIndex)) != 0L;
            module.SetIncludeInRewardPool(unlocked);
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

        // 2. 해당 SO의 보상 풀 포함 여부 즉시 갱신
        module.SetIncludeInRewardPool(true);

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
