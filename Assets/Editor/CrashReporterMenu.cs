using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// CrashReporter의 에디터 전용 스위치.
///
/// 설정은 EditorPrefs(이 PC 전용)에 저장되므로 커밋되지 않는다.
/// 내가 켜도 팀원 에디터는 꺼진 상태 그대로다.
/// </summary>
public static class CrashReporterMenu
{
    private const string UploadMenuPath = "Tools/Crash Reporter/에디터에서도 서버로 전송";
    private const string OpenFolderMenuPath = "Tools/Crash Reporter/대기 중인 리포트 폴더 열기";
    private const string ClearMenuPath = "Tools/Crash Reporter/대기 중인 리포트 모두 삭제";

    [MenuItem(UploadMenuPath)]
    private static void ToggleUploadInEditor()
    {
        bool enabled = !CrashReporter.UploadInEditorEnabled;
        CrashReporter.UploadInEditorEnabled = enabled;

        Debug.Log(enabled
            ? "[CrashReporter] 에디터 전송 ON — 이제 플레이 모드 예외가 우리 서버로 올라갑니다."
            : "[CrashReporter] 에디터 전송 OFF — 리포트는 디스크에만 쌓입니다.");
    }

    // 메뉴에 체크 표시를 그려준다.
    [MenuItem(UploadMenuPath, isValidateFunction: true)]
    private static bool ToggleUploadInEditorValidate()
    {
        Menu.SetChecked(UploadMenuPath, CrashReporter.UploadInEditorEnabled);
        return true;
    }

    [MenuItem(OpenFolderMenuPath)]
    private static void OpenPendingFolder()
    {
        string dir = PendingDirectory;
        Directory.CreateDirectory(dir);
        EditorUtility.RevealInFinder(dir);
    }

    [MenuItem(ClearMenuPath)]
    private static void ClearPendingReports()
    {
        string dir = PendingDirectory;
        if (!Directory.Exists(dir))
        {
            Debug.Log("[CrashReporter] 대기 중인 리포트가 없습니다.");
            return;
        }

        string[] files = Directory.GetFiles(dir, "*.json");
        if (files.Length == 0)
        {
            Debug.Log("[CrashReporter] 대기 중인 리포트가 없습니다.");
            return;
        }

        bool confirmed = EditorUtility.DisplayDialog(
            "크래시 리포트 삭제",
            $"아직 서버로 전송되지 않은 리포트 {files.Length}건을 삭제합니다.\n되돌릴 수 없습니다.",
            "삭제", "취소");
        if (!confirmed) return;

        foreach (string file in files) File.Delete(file);
        Debug.Log($"[CrashReporter] 리포트 {files.Length}건을 삭제했습니다.");
    }

    // CrashReporter가 리포트를 쌓아두는 곳과 같은 경로여야 한다.
    private static string PendingDirectory =>
        Path.Combine(Application.persistentDataPath, "CrashReports");
}
