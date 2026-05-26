using UnityEngine;
using System.Collections.Generic;

public enum SettingType
{
    LeftRightButton,
    Slider
}

public enum SettingKey
{
    None,
    GraphicsQuality,
    MasterVolume,
    BgmVolume,
    SfxVolume
}

[CreateAssetMenu(fileName = "SettingData", menuName = "UI/SettingData")]
public class SettingData : ScriptableObject
{
    public string tabName;
    public List<SettingCategory> categories;
}

[System.Serializable]
public class SettingCategory
{
    public string categoryName;
    public List<SettingItem> items;
}

[System.Serializable]
public class SettingItem
{
    public string itemName;
    public SettingType itemType;
    public SettingKey settingKey;

    [Header("Button Options")]
    public List<string> options;
    public int defaultIndex;

    [Header("Slider Options")]
    public float minValue = 0f;
    public float maxValue = 1f;
    public float defaultValue = 0.5f;
}