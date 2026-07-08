using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class PlayerAbilityBitIndexUtility
{
    private const string SkillModuleFolder = "Assets/02. Scripts/Player/Abilities/Resources/SkillModule";

    [MenuItem("Soul Rush/Abilities/Assign Missing Bit Indices")]
    public static void AssignMissingBitIndices()
    {
        int changed = 0;
        foreach (PlayerAbilityModule module in LoadSkillModules())
        {
            if (module == null || module.BitIndex > 0)
            {
                continue;
            }

            if (TryAssignNextBitIndex(module))
            {
                changed++;
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[Ability BitIndex] 비어 있는 BitIndex {changed}개를 자동 할당했습니다.");
    }

    [MenuItem("Soul Rush/Abilities/Normalize Bit Indices By Type")]
    public static void NormalizeBitIndicesByType()
    {
        int changed = 0;
        changed += NormalizeType(AbilityType.Active);
        changed += NormalizeType(AbilityType.Passive);
        changed += NormalizeType(AbilityType.Utility);

        AssetDatabase.SaveAssets();
        Debug.Log($"[Ability BitIndex] 타입별 범위 기준으로 BitIndex {changed}개를 재정렬했습니다.");
    }

    internal static bool TryAssignNextBitIndex(PlayerAbilityModule module)
    {
        if (module == null)
        {
            return false;
        }

        if (!TryGetRange(module.AbilityType, out int min, out int max))
        {
            return false;
        }

        HashSet<int> used = new HashSet<int>(
            LoadSkillModules()
                .Where(candidate => candidate != null && candidate != module && candidate.AbilityType == module.AbilityType)
                .Select(candidate => candidate.BitIndex));

        for (int bitIndex = min; bitIndex <= max; bitIndex++)
        {
            if (used.Contains(bitIndex))
            {
                continue;
            }

            SetBitIndex(module, bitIndex);
            return true;
        }

        Debug.LogWarning($"[Ability BitIndex] {module.AbilityType} 범위 {min}~{max}에 빈 BitIndex가 없습니다: {module.name}");
        return false;
    }

    internal static bool IsInTypeRange(PlayerAbilityModule module)
    {
        if (module == null || !TryGetRange(module.AbilityType, out int min, out int max))
        {
            return false;
        }

        return module.BitIndex >= min && module.BitIndex <= max;
    }

    internal static string GetRangeLabel(AbilityType abilityType)
    {
        return TryGetRange(abilityType, out int min, out int max)
            ? $"{min}~{max}"
            : "-";
    }

    private static int NormalizeType(AbilityType abilityType)
    {
        if (!TryGetRange(abilityType, out int min, out int max))
        {
            return 0;
        }

        List<PlayerAbilityModule> modules = LoadSkillModules()
            .Where(module => module != null && module.AbilityType == abilityType)
            .OrderBy(module => module.BitIndex)
            .ThenBy(module => module.AbilityId)
            .ToList();

        int capacity = max - min + 1;
        if (modules.Count > capacity)
        {
            Debug.LogWarning($"[Ability BitIndex] {abilityType} 스킬 수가 범위 용량({capacity})보다 많습니다.");
        }

        int changed = 0;
        for (int i = 0; i < modules.Count && i < capacity; i++)
        {
            int bitIndex = min + i;
            if (modules[i].BitIndex == bitIndex)
            {
                continue;
            }

            SetBitIndex(modules[i], bitIndex);
            changed++;
        }

        return changed;
    }

    private static bool TryGetRange(AbilityType abilityType, out int min, out int max)
    {
        switch (abilityType)
        {
            case AbilityType.Active:
                min = 1;
                max = 19;
                return true;
            case AbilityType.Passive:
                min = 20;
                max = 39;
                return true;
            case AbilityType.Utility:
                min = 40;
                max = 60;
                return true;
            default:
                min = 0;
                max = 0;
                return false;
        }
    }

    private static List<PlayerAbilityModule> LoadSkillModules()
    {
        string[] guids = PlayerAbilityAssetSearch.FindAbilityAssetGuids(new[] { SkillModuleFolder });
        List<PlayerAbilityModule> modules = new List<PlayerAbilityModule>(guids.Length);
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            PlayerAbilityModule module = AssetDatabase.LoadAssetAtPath<PlayerAbilityModule>(path);
            if (module != null)
            {
                modules.Add(module);
            }
        }

        return modules;
    }

    private static void SetBitIndex(PlayerAbilityModule module, int bitIndex)
    {
        SerializedObject serializedObject = new SerializedObject(module);
        serializedObject.FindProperty("bitIndex").intValue = bitIndex;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(module);
    }
}
