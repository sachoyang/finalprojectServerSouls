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
                form.AddField("bit_index", module.BitIndex);
                form.AddField("ability_id", module.AbilityId ?? "");
                form.AddField("display_name", module.DisplayName ?? "");
                form.AddField("description", module.Description ?? "");
                form.AddField("ability_type", module.AbilityType.ToString());
                form.AddField("basic_skill", module.BasicSkill ? 1 : 0);
                form.AddField("stamina_cost", module.StaminaCost.ToString());
                form.AddField("cooldown_seconds", module.CooldownSeconds.ToString());
                form.AddField("damage_multiplier", module.HitboxDamage.ToString());
                form.AddField("duration", module.HitboxLifetime.ToString());
                form.AddField("special_effect", module.SpecialEffect.ToString());

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
