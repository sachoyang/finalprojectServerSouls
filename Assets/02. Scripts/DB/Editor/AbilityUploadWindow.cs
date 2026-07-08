using UnityEngine;
using UnityEditor;
using UnityEngine.Networking;
using System.Threading.Tasks;

public class AbilityUploadWindow : EditorWindow
{
    private SoulRushApiSettings settings;
    private bool settingsDirty;
    private bool isUploading;

    [MenuItem("Soul Rush/🚀 스킬 DB로 업로드 (Upload)")]
    public static void ShowWindow()
    {
        GetWindow<AbilityUploadWindow>("스킬 업로드 툴");
    }

    private void OnEnable()
    {
        settings = SoulRushApiSettings.GetOrCreateSettings();
    }

    private void OnDisable()
    {
        SaveSettingsIfRequired();
    }

    private void OnGUI()
    {
        GUILayout.Label("유니티 ➔ DB 스킬 기획 데이터 업로드", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        EditorGUI.BeginChangeCheck();
        settings.uploadUrl = EditorGUILayout.TextField("업로드 API 주소", settings.uploadUrl);
        if (EditorGUI.EndChangeCheck())
        {
            EditorUtility.SetDirty(settings);
            settingsDirty = true;
        }

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "선택한 스킬 모듈(SO)의 기획 데이터(쿨타임, 데미지 등)를 DB로 업로드합니다.\n" +
            "업로드 전, 'Bit Index'와 'Ability Id'가 정확히 입력되었는지 확인하세요!", MessageType.Warning);
        EditorGUILayout.Space();

        using (new EditorGUI.DisabledScope(isUploading))
        {
            if (GUILayout.Button(
                    isUploading ? "업로드 중..." : "🔥 선택한 스킬 데이터 DB로 업로드",
                    GUILayout.Height(40)))
            {
                UploadSelectedAbilities();
            }
        }
    }

    private async void UploadSelectedAbilities()
    {
        if (isUploading)
        {
            return;
        }

        Object[] selectedObjects = Selection.GetFiltered(typeof(PlayerAbilityModule), SelectionMode.Assets);
        
        if (selectedObjects.Length == 0)
        {
            Debug.LogWarning("[업로드 알림] 업로드할 스킬 모듈(SO) 파일을 선택해주세요!");
            return;
        }

        isUploading = true;
        Repaint();
        int successCount = 0;
        try
        {
            foreach (Object obj in selectedObjects)
            {
                PlayerAbilityModule module = (PlayerAbilityModule)obj;

                WWWForm form = new WWWForm();
                // 공통 (abilities)
                form.AddField("bit_index", module.BitIndex);
                form.AddField("ability_id", module.AbilityId ?? "");
                form.AddField("ability_type", module.AbilityType.ToString());
                form.AddField("display_name", module.DisplayName ?? "");
                form.AddField("description", module.Description ?? "");
                form.AddField("appear_stage", module.AppearStage);
                form.AddField("basic_skill", module.BasicSkill ? 1 : 0);
                form.AddField("unlocked_skill", module.UnlockedSkill ? 1 : 0);
                form.AddField("max_level", module.MaxLevel);

                // 타입별 레벨무관 기본값 (active/utility). Passive는 0/빈값으로 전송된다.
                form.AddField("cooldown_seconds", module.CooldownSeconds.ToString());
                form.AddField("stamina_cost", module.StaminaCost.ToString());
                form.AddField("special_effect", module.SpecialEffect.ToString());

                // 레벨별 값은 배열이므로 JSON 문자열로 묶어서 보낸다. (서버는 파싱해 _levels 테이블에 저장)
                form.AddField("levels_json", BuildLevelsJson(module));

                using (UnityWebRequest www = UnityWebRequest.Post(settings.uploadUrl, form))
                {
                    var operation = www.SendWebRequest();
                    while (!operation.isDone) await Task.Delay(10);

                    if (www.result == UnityWebRequest.Result.Success)
                    {
                        Debug.Log($"<color=green>[업로드 성공]</color> {module.name} ➔ DB 전송 완료!");
                        successCount++;
                    }
                    else
                    {
                        Debug.LogError($"<color=red>[업로드 실패]</color> {module.name} ➔ {www.error}");
                    }
                }
            }

            Debug.Log($"✨ <b>총 {successCount}개의 스킬 데이터가 서버 DB에 업데이트 되었습니다.</b>");
        }
        finally
        {
            isUploading = false;
            Repaint();
        }
    }

    // 모듈의 레벨별 값을 타입에 맞게 뽑아 levels_json({"levels":[...]}) 문자열로 만든다.
    //  값은 공개 접근자(Get*)로 읽는다. Passive의 방어/공격 증가율은 내부적으로 rate(÷100)로
    //  저장되므로 업로드 시 다시 퍼센트(×100)로 되돌려 보낸다.
    private static string BuildLevelsJson(PlayerAbilityModule module)
    {
        AbilityLevelDBList wrap = new AbilityLevelDBList();
        int max = module.MaxLevel;

        for (int level = 1; level <= max; level++)
        {
            AbilityLevelDBData row = new AbilityLevelDBData { level = level };

            switch (module.AbilityType)
            {
                case AbilityType.Active:
                    row.skill_multiplier = module.GetDamageMultiplier(level);
                    break;
                case AbilityType.Passive:
                    row.max_health_bonus = module.GetMaxHealthBonus(level);
                    row.max_stamina_bonus = module.GetMaxStaminaBonus(level);
                    row.defense_bonus_percent = module.GetDefenseRateBonus(level) * 100f;
                    row.attack_damage_bonus_percent = module.GetAttackDamageBonusRate(level) * 100f;
                    break;
                case AbilityType.Utility:
                    row.health_restore_amount = module.GetHealthRestoreAmount(level);
                    row.stamina_restore_amount = module.GetStaminaRestoreAmount(level);
                    break;
            }

            wrap.levels.Add(row);
        }

        return JsonUtility.ToJson(wrap);
    }

    private void SaveSettingsIfRequired()
    {
        if (!settingsDirty)
        {
            return;
        }

        settingsDirty = false;
        AssetDatabase.SaveAssets();
    }
}
