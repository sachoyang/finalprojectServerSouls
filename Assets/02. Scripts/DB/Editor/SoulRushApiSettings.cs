using UnityEngine;
using UnityEditor;
using System.IO;

// API 주소들을 물리적 파일(.asset)로 저장해서 Git으로 공유하기 위한 데이터 클래스
public class SoulRushApiSettings : ScriptableObject
{
    public string bakeUrl = "http://192.168.0.5:8080/soulrush_api/get_abilities.php";
    public string uploadUrl = "http://192.168.0.5:8080/soulrush_api/upload_ability.php";

    // 저장될 경로 (Git에 올라갈 위치)
    private const string SettingPath = "Assets/02. Scripts/DB/SoulRushApiSettings.asset";

    public static SoulRushApiSettings GetOrCreateSettings()
    {
        var settings = AssetDatabase.LoadAssetAtPath<SoulRushApiSettings>(SettingPath);
        
        // 파일이 없으면 자동으로 생성해 줍니다.
        if (settings == null)
        {
            string settingsDirectory = Path.GetDirectoryName(SettingPath);
            if (!string.IsNullOrEmpty(settingsDirectory) &&
                !Directory.Exists(settingsDirectory))
            {
                Directory.CreateDirectory(settingsDirectory);
            }
            settings = ScriptableObject.CreateInstance<SoulRushApiSettings>();
            AssetDatabase.CreateAsset(settings, SettingPath);
            AssetDatabase.SaveAssets();
        }
        return settings;
    }
}
