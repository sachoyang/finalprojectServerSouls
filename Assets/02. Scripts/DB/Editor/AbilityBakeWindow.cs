using UnityEngine;
using UnityEditor;
using UnityEngine.Networking;
using System.Threading.Tasks;
using System.IO;

public class AbilityBakeWindow : EditorWindow
{
    private SoulRushApiSettings settings;
    private const string SAVE_PATH = "Assets/02. Scripts/Player/Abilities/Resources/SkillModule";

    [MenuItem("Soul Rush/⚔️ 스킬 DB 동기화 (Bake)")]
    public static void ShowWindow()
    {
        GetWindow<AbilityBakeWindow>("스킬 동기화 툴");
    }

    private void OnEnable()
    {
        // 파일에서 세팅 값 불러오기 (없으면 자동 생성)
        settings = SoulRushApiSettings.GetOrCreateSettings();
    }

    private void OnGUI()
    {
        GUILayout.Label("DB 스킬 데이터 가져오기", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        // 값이 변경되었는지 감지 시작
        EditorGUI.BeginChangeCheck();
        
        settings.bakeUrl = EditorGUILayout.TextField("API 주소 (get_abilities.php)", settings.bakeUrl);

        // 사용자가 타이핑해서 값이 바뀌었다면 에셋에 변경 사항을 저장 (Git 추적 가능)
        if (EditorGUI.EndChangeCheck())
        {
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
        }

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("버튼을 누르면 서버의 스킬 데이터를 가져와 Assets/Resources/GeneratedAbilities 폴더에 실제 파일(.asset)로 구워냅니다.\n\n팀원들은 이 폴더에 생성된 스킬들을 프리팹이나 인벤토리에 드래그해서 사용하면 됩니다.", MessageType.Info);
        EditorGUILayout.Space();

        if (GUILayout.Button("🚀 스킬 동기화 실행 (Bake)", GUILayout.Height(40)))
        {
            BakeAbilities();
        }
    }

    private async void BakeAbilities()
    {
        // 2. 서버 연결 및 JSON 받아오기 (세팅 파일의 주소 사용)
        Debug.Log("🌐 서버에서 스킬 데이터를 다운로드 중...");
        UnityWebRequest req = UnityWebRequest.Get(settings.bakeUrl);
        var operation = req.SendWebRequest();

        while (!operation.isDone) await Task.Delay(10); 

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"[Bake 에러] 서버 통신 실패: {req.error}");
            return;
        }

        AbilityDBResponse res = JsonUtility.FromJson<AbilityDBResponse>(req.downloadHandler.text);
        if (res == null || res.status != "success")
        {
            Debug.LogError("[Bake 에러] JSON 파싱 실패 또는 데이터가 없습니다.");
            return;
        }

        // 3. 저장 폴더 확인 및 생성
        if (!Directory.Exists(SAVE_PATH))
        {
            Directory.CreateDirectory(SAVE_PATH);
            AssetDatabase.Refresh();
        }

        int count = 0;

        // 4. 스킬 파일(.asset) 생성 및 데이터 덮어쓰기
        foreach (var dbData in res.data)
        {
            string assetPath = $"{SAVE_PATH}/{dbData.ability_id}.asset";
            PlayerAbilityModule module = AssetDatabase.LoadAssetAtPath<PlayerAbilityModule>(assetPath);

            bool isNew = false;
            if (module == null)
            {
                module = ScriptableObject.CreateInstance<PlayerAbilityModule>();
                isNew = true;
            }

            // 에셋에 DB 수치와 프리팹 연결!
            module.InitializeFromDB(dbData);

            if (isNew)
            {
                AssetDatabase.CreateAsset(module, assetPath);
            }
            else
            {
                EditorUtility.SetDirty(module); 
            }
            count++;
        }

        // 5. 물리적 파일로 확정 (디스크 쓰기)
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"<color=cyan>✨ [Bake 완료] 총 {count}개의 스킬 파일이 업데이트 되었습니다! Resources/GeneratedAbilities 폴더를 확인하세요.</color>");
    }
}