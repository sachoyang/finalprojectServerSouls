using UnityEngine;
using UnityEditor;
using UnityEngine.Networking;
using System.Threading.Tasks;

public class AbilityUploadWindow : EditorWindow
{
    private SoulRushApiSettings settings;

    [MenuItem("Soul Rush/🚀 스킬 DB로 업로드 (Upload)")]
    public static void ShowWindow()
    {
        GetWindow<AbilityUploadWindow>("스킬 업로드 툴");
    }

    private void OnEnable()
    {
        settings = SoulRushApiSettings.GetOrCreateSettings();
    }

    private void OnGUI()
    {
        GUILayout.Label("유니티 ➔ DB 스킬 업로드", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        // 값이 변경되었는지 감지 시작
        EditorGUI.BeginChangeCheck();

        settings.uploadUrl = EditorGUILayout.TextField("업로드 API 주소", settings.uploadUrl);

        // 변경 사항이 있으면 에셋 저장 (Git 추적)
        if (EditorGUI.EndChangeCheck())
        {
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
        }

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "선택한 PlayerAbilityModule(SO) 데이터를 DB로 덮어씌웁니다.\n" +
            "보상 풀 포함 여부(include_in_reward_pool)는 서버로 전송하지 않습니다. (비트마스크로 별도 관리)\n" +
            "업로드 전, SO 파일의 'Bit Index'가 제대로 설정되어 있는지 꼭 확인하세요!", MessageType.Warning);
        EditorGUILayout.Space();

        if (GUILayout.Button("🔥 선택한 스킬들 DB로 업로드", GUILayout.Height(40)))
        {
            UploadSelectedAbilities();
        }
    }

    private async void UploadSelectedAbilities()
    {
        // 1. 에셋 데이터베이스 찾기
        string[] dbGuids = AssetDatabase.FindAssets("t:AbilityAssetDatabase");
        if (dbGuids.Length == 0)
        {
            Debug.LogError("[업로드 에러] AbilityAssetDatabase를 찾을 수 없습니다.");
            return;
        }
        AbilityAssetDatabase assetDB = AssetDatabase.LoadAssetAtPath<AbilityAssetDatabase>(AssetDatabase.GUIDToAssetPath(dbGuids[0]));

        // 2. 현재 유니티 프로젝트 창에서 선택된 SO들 가져오기
        Object[] selectedObjects = Selection.GetFiltered(typeof(PlayerAbilityModule), SelectionMode.Assets);
        
        if (selectedObjects.Length == 0)
        {
            Debug.LogWarning("[업로드 알림] 업로드할 PlayerAbilityModule(SO) 파일을 선택해주세요!");
            return;
        }

        int successCount = 0;

        // 3. 선택된 SO들을 하나씩 서버로 전송
        foreach (Object obj in selectedObjects)
        {
            PlayerAbilityModule module = (PlayerAbilityModule)obj;

            WWWForm form = new WWWForm();
            form.AddField("bit_index", module.BitIndex);
            form.AddField("ability_id", module.AbilityId ?? "");
            form.AddField("display_name", module.DisplayName ?? "");
            form.AddField("description", module.Description ?? "");
            form.AddField("ability_type", module.AbilityType.ToString());
            
            form.AddField("stamina_cost", module.StaminaCost.ToString());
            form.AddField("cooldown_seconds", module.CooldownSeconds.ToString());
            form.AddField("damage_multiplier", module.HitboxDamage.ToString());
            form.AddField("duration", module.HitboxLifetime.ToString());
            form.AddField("special_effect", module.SpecialEffect.ToString());

            form.AddField("icon_key", GetKey(module.Icon));
            form.AddField("animation_key", GetKey(module.AnimationClip));
            form.AddField("vfx_key", GetKey(module.EffectPrefab));
            form.AddField("hitbox_key", GetKey(module.HitboxPrefab));
            
            form.AddField("sound_key", GetKey(module.SoundClip));
            form.AddField("sound_volume", module.SoundVolume.ToString());
            form.AddField("sound_delay", module.SoundDelay.ToString());

            // 세팅 파일의 업로드 주소 사용
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

        Debug.Log($"✨ <b>총 {successCount}개의 스킬이 서버 DB에 업데이트 되었습니다.</b> 웹 관리자 페이지를 확인하세요!");
    }

    private string GetKey(UnityEngine.Object asset) => asset != null ? asset.name : "";
}