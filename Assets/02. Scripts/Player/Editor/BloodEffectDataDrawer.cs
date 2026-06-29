using UnityEditor;
using UnityEngine;

/// <summary>
/// BloodEffectData 리스트의 모든 요소를 동일한 한글 UI로 그린다.
/// Unity 2022에서 새 리스트 요소가 필드별 PropertyDrawer를 놓치는 표시 문제를 방지한다.
/// </summary>
[CustomPropertyDrawer(typeof(BloodEffectSpawner.BloodEffectData))]
public sealed class BloodEffectDataDrawer : PropertyDrawer
{
    private const int BaseVisibleFieldCount = 8;

    private static readonly GUIContent EffectIdLabel =
        new GUIContent("효과 ID", "기본 공격 타수 또는 Active 스킬의 AbilityId를 선택합니다.");
    private static readonly GUIContent PrefabLabel =
        new GUIContent("피 이펙트 프리팹", "06. Prefabs/BloodEffect 폴더의 프리팹을 선택합니다.");
    private static readonly GUIContent HitSoundLabel =
        new GUIContent("타격 사운드", "피 이펙트와 같은 위치에서 재생할 3D 사운드입니다.");
    private static readonly GUIContent SoundVolumeLabel =
        new GUIContent("사운드 볼륨", "SoundManager의 CombatHit 볼륨에 곱합니다. 기본값은 1입니다.");
    private static readonly GUIContent ScaleLabel =
        new GUIContent("기본 크기", "프리팹 원본 크기에 곱할 배율입니다.");
    private static readonly GUIContent RandomScaleMinLabel =
        new GUIContent("무작위 크기 최소", "기본 크기에 곱할 최소 무작위 배율입니다.");
    private static readonly GUIContent RandomScaleMaxLabel =
        new GUIContent("무작위 크기 최대", "기본 크기에 곱할 최대 무작위 배율입니다.");
    private static readonly GUIContent OverrideColorLabel =
        new GUIContent("색상 변경", "체크한 경우에만 아래 색상으로 원본 색상을 덮어씁니다.");
    private static readonly GUIContent ColorLabel =
        new GUIContent("피 색상", "색상 변경을 체크했을 때 적용할 색상입니다.");

    public override float GetPropertyHeight(
        SerializedProperty property,
        GUIContent label)
    {
        float lineHeight = EditorGUIUtility.singleLineHeight;
        if (!property.isExpanded)
        {
            return lineHeight;
        }

        SerializedProperty overrideColor = property.FindPropertyRelative("overrideColor");
        bool showColor = overrideColor != null && overrideColor.boolValue;
        int visibleFieldCount = BaseVisibleFieldCount + (showColor ? 1 : 0);

        return lineHeight * (visibleFieldCount + 1) +
               EditorGUIUtility.standardVerticalSpacing * visibleFieldCount;
    }

    public override void OnGUI(
        Rect position,
        SerializedProperty property,
        GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        Rect line = new Rect(
            position.x,
            position.y,
            position.width,
            EditorGUIUtility.singleLineHeight);
        property.isExpanded = EditorGUI.Foldout(
            line,
            property.isExpanded,
            label,
            true);

        if (!property.isExpanded)
        {
            EditorGUI.EndProperty();
            return;
        }

        EditorGUI.indentLevel++;
        DrawProperty(ref line, property.FindPropertyRelative("effectId"), EffectIdLabel);
        DrawProperty(ref line, property.FindPropertyRelative("prefab"), PrefabLabel);
        DrawProperty(ref line, property.FindPropertyRelative("hitSound"), HitSoundLabel);
        DrawProperty(ref line, property.FindPropertyRelative("soundVolume"), SoundVolumeLabel);
        DrawProperty(ref line, property.FindPropertyRelative("scale"), ScaleLabel);
        DrawProperty(ref line, property.FindPropertyRelative("randomScaleMin"), RandomScaleMinLabel);
        DrawProperty(ref line, property.FindPropertyRelative("randomScaleMax"), RandomScaleMaxLabel);

        SerializedProperty overrideColor = property.FindPropertyRelative("overrideColor");
        DrawProperty(ref line, overrideColor, OverrideColorLabel);

        if (overrideColor != null && overrideColor.boolValue)
        {
            MoveToNextLine(ref line);
            SerializedProperty color = property.FindPropertyRelative("color");
            if (color != null)
            {
                EditorGUI.PropertyField(line, color, ColorLabel);
            }
        }

        EditorGUI.indentLevel--;
        EditorGUI.EndProperty();
    }

    private static void DrawProperty(
        ref Rect line,
        SerializedProperty property,
        GUIContent label)
    {
        MoveToNextLine(ref line);
        if (property != null)
        {
            EditorGUI.PropertyField(line, property, label);
        }
    }

    private static void MoveToNextLine(ref Rect line)
    {
        line.y += EditorGUIUtility.singleLineHeight +
                  EditorGUIUtility.standardVerticalSpacing;
    }
}
