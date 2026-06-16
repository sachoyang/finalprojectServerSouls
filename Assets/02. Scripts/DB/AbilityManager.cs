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
    public string icon_key;
    public string animation_key;
    public string vfx_key;
    public string hitbox_key;

    public string sound_key;
    public float sound_volume;
    public float sound_delay;
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
    [Header("에셋 데이터베이스 연결")]
    [Tooltip("아까 만든 Ability Asset Database SO 파일을 끌어다 넣으세요.")]
    public AbilityAssetDatabase assetDatabase;

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
                        // 🔥 [핵심 변경] 새로 만들지 말고, Bake 툴이 만들어둔 실제 파일을 가져옵니다!
                        PlayerAbilityModule bakedModule = Resources.Load<PlayerAbilityModule>($"GeneratedAbilities/{dbData.ability_id}");
                        
                        if (bakedModule != null)
                        {
                            // 라이브 업데이트: 서버에서 바뀐 최신 데미지/쿨타임 수치만 덮어씌움! (클라이언트 패치 불필요)
                            bakedModule.InitializeFromDB(dbData, assetDatabase);
                            
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
