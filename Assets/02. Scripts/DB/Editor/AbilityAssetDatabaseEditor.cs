using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

[CustomEditor(typeof(AbilityAssetDatabase))]
public class AbilityAssetDatabaseEditor : Editor
{
    private SerializedProperty _icons;
    private SerializedProperty _animations;
    private SerializedProperty _animationEntries;
    private SerializedProperty _triggerSourceController;
    private SerializedProperty _prefabs;
    private SerializedProperty _sounds;
    private SerializedProperty _dropFbxHere;

    private void OnEnable()
    {
        _icons = serializedObject.FindProperty("icons");
        _animations = serializedObject.FindProperty("animations");
        _animationEntries = serializedObject.FindProperty("animationEntries");
        _triggerSourceController = serializedObject.FindProperty("triggerSourceController");
        _prefabs = serializedObject.FindProperty("prefabs");
        _sounds = serializedObject.FindProperty("sounds");
        _dropFbxHere = serializedObject.FindProperty("dropFbxHere");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.LabelField("에셋 등록소", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_icons, true);
        EditorGUILayout.PropertyField(_animations, true);
        EditorGUILayout.PropertyField(_triggerSourceController);
        DrawAnimationEntries();
        EditorGUILayout.PropertyField(_prefabs, true);
        EditorGUILayout.PropertyField(_sounds, true);

        EditorGUILayout.Space(16f);
        EditorGUILayout.LabelField("FBX 애니메이션 자동 추출기", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_dropFbxHere, true);

        if (serializedObject.ApplyModifiedProperties())
        {
            EditorUtility.SetDirty(target);
        }
    }

    private void DrawAnimationEntries()
    {
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Animation Trigger Entries", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Sync From Animations"))
            {
                if (SyncEntriesFromAnimations())
                {
                    SaveDatabaseAsset();
                }
            }

            if (GUILayout.Button("Add Entry"))
            {
                _animationEntries.arraySize++;
                SaveDatabaseAsset();
            }
        }

        AnimatorController controller = _triggerSourceController.objectReferenceValue as AnimatorController;
        string[] triggerOptions = GetTriggerOptions(controller);

        for (int i = 0; i < _animationEntries.arraySize; i++)
        {
            SerializedProperty entry = _animationEntries.GetArrayElementAtIndex(i);
            SerializedProperty clip = entry.FindPropertyRelative("clip");
            SerializedProperty trigger = entry.FindPropertyRelative("trigger");

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField($"Element {i}", EditorStyles.boldLabel);
                    if (GUILayout.Button("Remove", GUILayout.Width(70f)))
                    {
                        _animationEntries.DeleteArrayElementAtIndex(i);
                        SaveDatabaseAsset();
                        break;
                    }
                }

                EditorGUILayout.PropertyField(clip);
                DrawTriggerPopup(trigger, triggerOptions, controller != null);
            }
        }
    }

    private void DrawTriggerPopup(SerializedProperty trigger, string[] triggerOptions, bool hasController)
    {
        if (!hasController)
        {
            EditorGUILayout.PropertyField(trigger);
            EditorGUILayout.HelpBox("Trigger Source Controller를 지정하면 Trigger 목록에서 선택할 수 있습니다.", MessageType.Info);
            return;
        }

        string current = trigger.stringValue;
        int selectedIndex = 0;
        for (int i = 0; i < triggerOptions.Length; i++)
        {
            if (triggerOptions[i] == current)
            {
                selectedIndex = i;
                break;
            }
        }

        if (!string.IsNullOrWhiteSpace(current) && selectedIndex == 0)
        {
            List<string> options = new List<string>(triggerOptions) { current };
            triggerOptions = options.ToArray();
            selectedIndex = triggerOptions.Length - 1;
        }

        selectedIndex = EditorGUILayout.Popup("Trigger", selectedIndex, triggerOptions);
        trigger.stringValue = selectedIndex <= 0 ? string.Empty : triggerOptions[selectedIndex];
    }

    private string[] GetTriggerOptions(AnimatorController controller)
    {
        List<string> options = new List<string> { "(None)" };
        if (controller == null)
        {
            return options.ToArray();
        }

        foreach (AnimatorControllerParameter parameter in controller.parameters)
        {
            if (parameter.type == AnimatorControllerParameterType.Trigger)
            {
                options.Add(parameter.name);
            }
        }

        return options.ToArray();
    }

    private bool SyncEntriesFromAnimations()
    {
        bool changed = false;
        for (int i = 0; i < _animations.arraySize; i++)
        {
            Object clip = _animations.GetArrayElementAtIndex(i).objectReferenceValue;
            if (clip == null || HasEntryForClip(clip))
            {
                continue;
            }

            int newIndex = _animationEntries.arraySize;
            _animationEntries.arraySize++;
            SerializedProperty entry = _animationEntries.GetArrayElementAtIndex(newIndex);
            entry.FindPropertyRelative("clip").objectReferenceValue = clip;
            entry.FindPropertyRelative("trigger").stringValue = string.Empty;
            changed = true;
        }

        return changed;
    }

    private bool HasEntryForClip(Object clip)
    {
        for (int i = 0; i < _animationEntries.arraySize; i++)
        {
            SerializedProperty entryClip = _animationEntries
                .GetArrayElementAtIndex(i)
                .FindPropertyRelative("clip");

            if (entryClip.objectReferenceValue == clip)
            {
                return true;
            }
        }

        return false;
    }

    private void SaveDatabaseAsset()
    {
        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(target);
        AssetDatabase.SaveAssets();
    }
}
