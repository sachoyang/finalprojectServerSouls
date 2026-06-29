using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 06. Prefabs/BloodEffect 폴더의 피 분출 프리팹만 선택 목록으로 보여준다.
/// 공통 부착 혈흔으로 따로 사용하는 AttachedBloodDecal은 목록에서 제외한다.
/// </summary>
[CustomPropertyDrawer(typeof(BloodEffectPrefabAttribute))]
public sealed class BloodEffectPrefabDrawer : PropertyDrawer
{
    private const string BloodEffectFolder = "Assets/06. Prefabs/BloodEffect";
    private const string AttachedBloodDecalName = "AttachedBloodDecal";

    private static readonly List<string> DisplayNames = new List<string>();
    private static readonly List<GameObject> Prefabs = new List<GameObject>();
    private static string[] _displayNameOptions = Array.Empty<string>();
    private static string[] _missingPrefabOptions = Array.Empty<string>();
    private static GameObject _cachedMissingPrefab;
    private static bool _cacheDirty = true;

    static BloodEffectPrefabDrawer()
    {
        EditorApplication.projectChanged += MarkCacheDirty;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EnsureCache();

        GameObject currentPrefab = property.objectReferenceValue as GameObject;
        int selectedIndex = Prefabs.IndexOf(currentPrefab);
        bool missingPrefab = currentPrefab != null && selectedIndex < 0;

        string[] shownNames = _displayNameOptions;
        if (missingPrefab)
        {
            if (_cachedMissingPrefab != currentPrefab)
            {
                _cachedMissingPrefab = currentPrefab;
                _missingPrefabOptions = new string[_displayNameOptions.Length + 1];
                Array.Copy(_displayNameOptions, _missingPrefabOptions, _displayNameOptions.Length);
                _missingPrefabOptions[^1] = $"현재 값(목록 외부) / {currentPrefab.name}";
            }

            shownNames = _missingPrefabOptions;
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
            newIndex < Prefabs.Count)
        {
            // SerializedProperty를 사용하므로 저장, Undo/Redo, Prefab Override가 함께 처리된다.
            property.objectReferenceValue = Prefabs[newIndex];
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
        Prefabs.Clear();

        DisplayNames.Add("선택 안 함");
        Prefabs.Add(null);

        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { BloodEffectFolder });
        List<GameObject> foundPrefabs = new List<GameObject>();
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null ||
                string.Equals(prefab.name, AttachedBloodDecalName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foundPrefabs.Add(prefab);
        }

        foundPrefabs.Sort((left, right) =>
            EditorUtility.NaturalCompare(left.name, right.name));

        for (int i = 0; i < foundPrefabs.Count; i++)
        {
            DisplayNames.Add(foundPrefabs[i].name);
            Prefabs.Add(foundPrefabs[i]);
        }

        _displayNameOptions = DisplayNames.ToArray();
        _cachedMissingPrefab = null;
        _missingPrefabOptions = Array.Empty<string>();
    }

    private static void MarkCacheDirty()
    {
        _cacheDirty = true;
    }
}
