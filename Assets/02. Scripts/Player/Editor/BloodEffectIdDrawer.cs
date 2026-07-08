using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 기본 공격 타수와 프로젝트에 존재하는 모든 Active 스킬을 효과 ID 드롭다운으로 표시한다.
/// 선택 결과에는 화면용 이름이 아니라 실제 PlayerAbilityModule.AbilityId가 저장된다.
/// </summary>
[CustomPropertyDrawer(typeof(BloodEffectIdAttribute))]
public sealed class BloodEffectIdDrawer : PropertyDrawer
{
    private static readonly List<string> DisplayNames = new List<string>();
    private static readonly List<string> EffectIds = new List<string>();
    private static string[] _displayNameOptions = Array.Empty<string>();
    private static string[] _missingValueOptions = Array.Empty<string>();
    private static string _cachedMissingValue;
    private static bool _cacheDirty = true;

    static BloodEffectIdDrawer()
    {
        EditorApplication.projectChanged += MarkCacheDirty;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EnsureCache();

        int selectedIndex = EffectIds.IndexOf(property.stringValue);
        bool missingValue = !string.IsNullOrWhiteSpace(property.stringValue) && selectedIndex < 0;

        string[] shownNames = _displayNameOptions;
        if (missingValue)
        {
            if (!string.Equals(_cachedMissingValue, property.stringValue, StringComparison.Ordinal))
            {
                _cachedMissingValue = property.stringValue;
                _missingValueOptions = new string[_displayNameOptions.Length + 1];
                Array.Copy(_displayNameOptions, _missingValueOptions, _displayNameOptions.Length);
                _missingValueOptions[^1] = $"현재 값(에셋 없음) / {property.stringValue}";
            }

            shownNames = _missingValueOptions;
            selectedIndex = shownNames.Length - 1;
        }
        else if (selectedIndex < 0)
        {
            selectedIndex = 0;
        }

        EditorGUI.BeginProperty(position, label, property);
        EditorGUI.BeginChangeCheck();
        int newIndex = EditorGUI.Popup(position, label.text, selectedIndex, shownNames);
        if (EditorGUI.EndChangeCheck() &&
            newIndex >= 0 &&
            newIndex < EffectIds.Count)
        {
            // SerializedProperty로 값을 바꾸면 기본 Inspector가 저장, Undo/Redo,
            // Scene Dirty 및 Prefab Override를 한 흐름으로 처리한다.
            property.stringValue = EffectIds[newIndex];
        }
        EditorGUI.EndProperty();
    }

    private static void EnsureCache()
    {
        if (!_cacheDirty)
        {
            return;
        }

        _cacheDirty = false;
        DisplayNames.Clear();
        EffectIds.Clear();

        AddOption("선택 안 함", string.Empty);
        AddOption("기본 공격 / 1타", BloodEffectSpawner.BasicAttack1Id);
        AddOption("기본 공격 / 2타", BloodEffectSpawner.BasicAttack2Id);
        AddOption("기본 공격 / 3타", BloodEffectSpawner.BasicAttack3Id);

        string[] guids = PlayerAbilityAssetSearch.FindAbilityAssetGuids();
        List<PlayerAbilityModule> activeModules = new List<PlayerAbilityModule>();
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            PlayerAbilityModule module = AssetDatabase.LoadAssetAtPath<PlayerAbilityModule>(path);
            // 별도 시스템 효과를 적용하는 모듈은 직접 타격하는 액티브 스킬이 아니므로 제외한다.
            if (module != null &&
                module.AbilityType == AbilityType.Active &&
                module.SpecialEffect == PlayerAbilitySpecialEffect.None)
            {
                activeModules.Add(module);
            }
        }

        activeModules.Sort((left, right) =>
            string.Compare(left.DisplayName, right.DisplayName, StringComparison.OrdinalIgnoreCase));

        HashSet<string> addedIds = new HashSet<string>(EffectIds);
        for (int i = 0; i < activeModules.Count; i++)
        {
            PlayerAbilityModule module = activeModules[i];
            if (string.IsNullOrWhiteSpace(module.AbilityId) || !addedIds.Add(module.AbilityId))
            {
                continue;
            }

            AddOption($"액티브 스킬 / {module.DisplayName}  [{module.AbilityId}]", module.AbilityId);
        }

        _displayNameOptions = DisplayNames.ToArray();
        _cachedMissingValue = null;
        _missingValueOptions = Array.Empty<string>();
    }

    private static void AddOption(string displayName, string effectId)
    {
        DisplayNames.Add(displayName);
        EffectIds.Add(effectId);
    }

    private static void MarkCacheDirty()
    {
        _cacheDirty = true;
    }
}
