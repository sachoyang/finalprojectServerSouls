using System.Collections.Generic;
using UnityEditor;

public static class PlayerAbilityAssetSearch
{
    private static readonly string[] AbilityTypeQueries =
    {
        "t:ActiveAbilityModule",
        "t:PassiveAbilityModule",
        "t:UtilityAbilityModule"
    };

    public static string[] FindAbilityAssetGuids(string[] searchInFolders = null)
    {
        HashSet<string> guids = new HashSet<string>();
        foreach (string query in AbilityTypeQueries)
        {
            string[] found = searchInFolders == null
                ? AssetDatabase.FindAssets(query)
                : AssetDatabase.FindAssets(query, searchInFolders);
            for (int i = 0; i < found.Length; i++)
            {
                guids.Add(found[i]);
            }
        }

        string[] result = new string[guids.Count];
        guids.CopyTo(result);
        return result;
    }
}
