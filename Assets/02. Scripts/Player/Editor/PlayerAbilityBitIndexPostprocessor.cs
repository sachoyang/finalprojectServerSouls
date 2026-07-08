using UnityEditor;

public sealed class PlayerAbilityBitIndexPostprocessor : AssetPostprocessor
{
    private const string SkillModuleFolder = "Assets/02. Scripts/Player/Abilities/Resources/SkillModule/";

    private static void OnPostprocessAllAssets(
        string[] importedAssets,
        string[] deletedAssets,
        string[] movedAssets,
        string[] movedFromAssetPaths)
    {
        bool changed = false;
        foreach (string assetPath in importedAssets)
        {
            if (!assetPath.StartsWith(SkillModuleFolder) || !assetPath.EndsWith(".asset"))
            {
                continue;
            }

            PlayerAbilityModule module = AssetDatabase.LoadAssetAtPath<PlayerAbilityModule>(assetPath);
            if (module == null || module.BitIndex > 0)
            {
                continue;
            }

            changed |= PlayerAbilityBitIndexUtility.TryAssignNextBitIndex(module);
        }

        if (changed)
        {
            AssetDatabase.SaveAssets();
        }
    }
}
