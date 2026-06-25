using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class PlayerAbilityPoolSetupTool
{
    // PlayerAbilityModule 에셋과 Player.prefab의 reward pool을 동기화하는 에디터 전용 도구.
    // Animator 동기화와 분리해, 보상 후보 등록만 필요할 때 prefab의 애니메이터 설정을 건드리지 않도록 한다.
    private const string PlayerPrefabPath = "Assets/06. Prefabs/Player.prefab";
    // 런타임 AbilityManager가 읽는 Resources/SkillModule과 동일한 에셋 폴더를 사용한다.
    private const string SkillModuleFolder = "Assets/02. Scripts/Player/Abilities/Resources/SkillModule";
    private const string AbilityPoolPropertyName = "abilityPool";

    [MenuItem("Tools/ServerSouls/Sync Ability Modules To Player Reward Pool")]
    public static void SyncAbilityModulesToPlayerRewardPool()
    {
        // PrefabUtility.LoadPrefabContents를 사용하면 씬에 배치된 오브젝트가 아니라 원본 Player.prefab을 직접 수정할 수 있다.
        // 작업이 끝나면 finally에서 반드시 UnloadPrefabContents를 호출해 에디터 메모리에 열린 prefab을 정리한다.
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        if (prefabRoot == null)
        {
            Debug.LogError($"Player prefab not found: {PlayerPrefabPath}");
            return;
        }

        try
        {
            PlayerAbilityInventory inventory = prefabRoot.GetComponentInChildren<PlayerAbilityInventory>(true);
            if (inventory == null)
            {
                Debug.LogError($"PlayerAbilityInventory not found in prefab: {PlayerPrefabPath}");
                return;
            }

            List<PlayerAbilityModule> eligibleModules = LoadEligibleModules();
            SerializedObject serializedInventory = new SerializedObject(inventory);
            SerializedProperty abilityPool = serializedInventory.FindProperty(AbilityPoolPropertyName);
            if (abilityPool == null || !abilityPool.isArray)
            {
                Debug.LogError($"Serialized property not found: {AbilityPoolPropertyName}");
                return;
            }

            int removedCount = RemoveIneligibleSkillModules(abilityPool, eligibleModules);
            int addedCount = AddMissingModules(abilityPool, eligibleModules);

            // SerializedProperty로 수정한 값을 실제 컴포넌트에 반영한 뒤 prefab asset으로 저장한다.
            serializedInventory.ApplyModifiedProperties();
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, PlayerPrefabPath);
            Debug.Log($"Player reward pool synced. Added: {addedCount}, Removed: {removedCount}, Total Eligible: {eligibleModules.Count}");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    private static List<PlayerAbilityModule> LoadEligibleModules()
    {
        // SkillModule 폴더에 있는 모듈 중 Include In Reward Pool이 켜진 것만 prefab pool 동기화 대상으로 삼는다.
        // 테스트용/미완성 모듈은 체크를 끄면 보상 후보에 섞이지 않는다.
        List<PlayerAbilityModule> modules = new List<PlayerAbilityModule>();

        // 폴더가 이동 또는 삭제된 상태에서 실행되어도 AssetDatabase 오류를 발생시키지 않는다.
        if (!AssetDatabase.IsValidFolder(SkillModuleFolder))
        {
            Debug.LogWarning($"SkillModule folder not found: {SkillModuleFolder}");
            return modules;
        }

        string[] moduleGuids = AssetDatabase.FindAssets("t:PlayerAbilityModule", new[] { SkillModuleFolder });

        foreach (string moduleGuid in moduleGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(moduleGuid);
            PlayerAbilityModule module = AssetDatabase.LoadAssetAtPath<PlayerAbilityModule>(path);
            if (module == null || !module.IncludeInRewardPool)
            {
                continue;
            }

            modules.Add(module);
        }

        modules.Sort((left, right) => string.CompareOrdinal(left.AbilityId, right.AbilityId));
        return modules;
    }

    private static int RemoveIneligibleSkillModules(SerializedProperty abilityPool, IReadOnlyList<PlayerAbilityModule> eligibleModules)
    {
        // 이 도구가 관리하는 SkillModule 폴더의 항목만 제거한다.
        // 다른 경로에서 수동으로 넣은 특수 모듈이 있을 수 있으므로, 폴더 밖 참조는 보존한다.
        int removedCount = 0;
        for (int i = abilityPool.arraySize - 1; i >= 0; i--)
        {
            SerializedProperty element = abilityPool.GetArrayElementAtIndex(i);
            PlayerAbilityModule module = element.objectReferenceValue as PlayerAbilityModule;
            if (module == null)
            {
                abilityPool.DeleteArrayElementAtIndex(i);
                removedCount++;
                continue;
            }

            string path = AssetDatabase.GetAssetPath(module);
            if (!path.StartsWith(SkillModuleFolder))
            {
                continue;
            }

            if (ContainsModule(eligibleModules, module))
            {
                continue;
            }

            abilityPool.DeleteArrayElementAtIndex(i);
            removedCount++;
        }

        return removedCount;
    }

    private static int AddMissingModules(SerializedProperty abilityPool, IReadOnlyList<PlayerAbilityModule> eligibleModules)
    {
        // 이미 pool에 들어간 모듈은 건너뛰어 여러 번 실행해도 중복 등록되지 않게 한다.
        int addedCount = 0;
        foreach (PlayerAbilityModule module in eligibleModules)
        {
            if (ContainsModule(abilityPool, module))
            {
                continue;
            }

            int index = abilityPool.arraySize;
            abilityPool.InsertArrayElementAtIndex(index);
            abilityPool.GetArrayElementAtIndex(index).objectReferenceValue = module;
            addedCount++;
        }

        return addedCount;
    }

    private static bool ContainsModule(SerializedProperty abilityPool, PlayerAbilityModule module)
    {
        for (int i = 0; i < abilityPool.arraySize; i++)
        {
            if (abilityPool.GetArrayElementAtIndex(i).objectReferenceValue == module)
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsModule(IReadOnlyList<PlayerAbilityModule> modules, PlayerAbilityModule module)
    {
        foreach (PlayerAbilityModule candidate in modules)
        {
            if (candidate == module)
            {
                return true;
            }
        }

        return false;
    }
}
