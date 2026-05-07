using UnityEngine;
using System.Collections.Generic;

// UI 항목의 타입을 구분하기 위한 열거형
public enum SettingType
{
    LeftRightButton, // 기존의 좌우 화살표 버튼 방식
    Slider           // 사운드 조절용 슬라이더 방식
}

[CreateAssetMenu(fileName = "SettingData", menuName = "UI/SettingData")]
public class SettingData : ScriptableObject
{
    public string tabName; // 탭 이름 (예: 그래픽, 사운드)
    public List<SettingCategory> categories;
}

[System.Serializable]
public class SettingCategory
{
    public string categoryName; // 대항목 (예: 기본 설정, 볼륨 설정)
    public List<SettingItem> items; // 중항목 리스트
}

[System.Serializable]
public class SettingItem
{
    public string itemName;      // 중항목 이름 (예: 배경음)
    public SettingType itemType; // UI 타입 선택 (인스펙터에서 드롭다운으로 선택 가능)

    [Header("Button Options")]
    public List<string> options; // 버튼형일 때 사용하는 리스트 (예: 낮음, 보통, 높음)
    public int defaultIndex;     // 버튼형 초기 선택 값

    [Header("Slider Options")]
    public float minValue = 0f;       // 슬라이더 최소값
    public float maxValue = 1f;       // 슬라이더 최대값
    public float defaultValue = 0.5f; // 슬라이더 초기값
}