using UnityEngine;
using UnityEditor;
using UnityEngine.Networking;
using System.Threading.Tasks;
using System.IO;

public class AbilityBakeWindow : EditorWindow
{
    private string serverUrl = "http://127.0.0.1:8080/soulrush_api/get_abilities.php"; // 기본 URL
    private const string SAVE_PATH = "Assets/Resources/GeneratedAbilities";

    [MenuItem("Soul Rush/⚔️ 스킬 DB 동기화 (Bake)")]
    public static void ShowWindow()
    {
        GetWindow<AbilityBakeWindow>("스킬 동기화 툴");
    }

    private void OnEnable()
    {
        // 이전에 입력했던 주소 기억하기
        serverUrl = EditorPrefs.GetString("SoulRush_API_URL", serverUrl);
    }

    private void OnGUI()
    {
        GUILayout.Label("DB 스킬 데이터 가져오기", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        serverUrl = EditorGUILayout.TextField("API 주소 (get_abilities.php)", serverUrl);

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("버튼을 누르면 서버의 스킬 데이터를 가져와 Assets/Resources/GeneratedAbilities 폴더에 실제 파일(.asset)로 구워냅니다.\n\n팀원들은 이 폴더에 생성된 스킬들을 프리팹이나 인벤토리에 드래그해서 사용하면 됩니다.", MessageType.Info);
        EditorGUILayout.Space();

        if (GUILayout.Button("🚀 스킬 동기화 실행 (Bake)", GUILayout.Height(40)))
        {
            EditorPrefs.SetString("SoulRush_API_URL", serverUrl); // 주소 저장
            BakeAbilities();
        }
    }

    private async void BakeAbilities()
    {
        // 1. AbilityAssetDatabase 자동 찾기
        string[] guids = AssetDatabase.FindAssets("t:AbilityAssetDatabase");
        if (guids.Length == 0)
        {
            Debug.LogError("[Bake 에러] 프로젝트 내에 AbilityAssetDatabase 파일이 없습니다!");
            return;
        }
        AbilityAssetDatabase assetDB = AssetDatabase.LoadAssetAtPath<AbilityAssetDatabase>(AssetDatabase.GUIDToAssetPath(guids[0]));

        // 2. 서버 연결 및 JSON 받아오기
        Debug.Log("🌐 서버에서 스킬 데이터를 다운로드 중...");
        UnityWebRequest req = UnityWebRequest.Get(serverUrl);
        var operation = req.SendWebRequest();

        while (!operation.isDone) await Task.Delay(10); // 에디터 프리징 방지

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
            module.InitializeFromDB(dbData, assetDB);

            if (isNew)
            {
                AssetDatabase.CreateAsset(module, assetPath);
            }
            else
            {
                EditorUtility.SetDirty(module); // 기존 파일 변경사항 저장
            }
            count++;
        }

        // 5. 물리적 파일로 확정 (디스크 쓰기)
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"<color=cyan>✨ [Bake 완료] 총 {count}개의 스킬 파일이 업데이트 되었습니다! Resources/GeneratedAbilities 폴더를 확인하세요.</color>");
    }
}