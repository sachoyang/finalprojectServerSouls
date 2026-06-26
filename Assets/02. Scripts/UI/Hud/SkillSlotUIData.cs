using UnityEngine;

// PlayerAbilityInventory의 액티브 슬롯을 HUD 표시용으로 가공한 읽기 전용 데이터다.
// 쿨타임 종료 시간 계산은 인벤토리 쪽에서 끝내고, UI는 남은 시간과 아이콘만 표시한다.
public readonly struct SkillSlotUIData
{
    public readonly bool IsEmpty;
    public readonly string AbilityId;
    public readonly string DisplayName;
    public readonly Sprite Icon;
    public readonly KeyCode KeyCode;
    public readonly float CooldownRemaining;
    public readonly float CooldownDuration;
    public readonly bool IsReady;

    public SkillSlotUIData(
        bool isEmpty,
        string abilityId,
        string displayName,
        Sprite icon,
        KeyCode keyCode,
        float cooldownRemaining,
        float cooldownDuration)
    {
        IsEmpty = isEmpty;
        AbilityId = abilityId;
        DisplayName = displayName;
        Icon = icon;
        KeyCode = keyCode;
        CooldownRemaining = cooldownRemaining;
        CooldownDuration = cooldownDuration;
        IsReady = !isEmpty && cooldownRemaining <= 0f;
    }

    // 빈 슬롯도 null 대신 명시적인 데이터로 넘겨 UI 분기를 단순하게 유지한다.
    public static SkillSlotUIData Empty => new SkillSlotUIData(
        true,
        string.Empty,
        string.Empty,
        null,
        KeyCode.None,
        0f,
        0f);
}
