using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

[CustomPropertyDrawer(typeof(AbilityAnimationEntry))]
public class AbilityAnimationEntryDrawer : PropertyDrawer
{
    private const float LineSpacing = 2f;

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUIUtility.singleLineHeight * 2f + LineSpacing;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        SerializedProperty clip = property.FindPropertyRelative("clip");
        SerializedProperty trigger = property.FindPropertyRelative("trigger");

        Rect clipRect = new Rect(
            position.x,
            position.y,
            position.width,
            EditorGUIUtility.singleLineHeight);

        Rect triggerRect = new Rect(
            position.x,
            position.y + EditorGUIUtility.singleLineHeight + LineSpacing,
            position.width,
            EditorGUIUtility.singleLineHeight);

        EditorGUI.PropertyField(clipRect, clip);
        DrawTriggerPopup(triggerRect, property, trigger);

        EditorGUI.EndProperty();
    }

    private static void DrawTriggerPopup(Rect rect, SerializedProperty property, SerializedProperty trigger)
    {
        AbilityAssetDatabase database = property.serializedObject.targetObject as AbilityAssetDatabase;
        AnimatorController controller = database != null
            ? database.triggerSourceController as AnimatorController
            : null;

        if (controller == null)
        {
            EditorGUI.PropertyField(rect, trigger);
            return;
        }

        string[] options = GetTriggerOptions(controller, trigger.stringValue);
        int selectedIndex = 0;
        for (int i = 0; i < options.Length; i++)
        {
            if (options[i] == trigger.stringValue)
            {
                selectedIndex = i;
                break;
            }
        }

        selectedIndex = EditorGUI.Popup(rect, "Trigger", selectedIndex, options);
        trigger.stringValue = selectedIndex <= 0 ? string.Empty : options[selectedIndex];
    }

    private static string[] GetTriggerOptions(AnimatorController controller, string current)
    {
        List<string> options = new List<string> { "(None)" };

        foreach (AnimatorControllerParameter parameter in controller.parameters)
        {
            if (parameter.type == AnimatorControllerParameterType.Trigger)
            {
                options.Add(parameter.name);
            }
        }

        if (!string.IsNullOrWhiteSpace(current) && !options.Contains(current))
        {
            options.Add(current);
        }

        return options.ToArray();
    }
}
