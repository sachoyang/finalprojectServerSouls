using UnityEngine;
using UnityEngine.UI;

public class SettingSliderUI : MonoBehaviour
{
    public Text itemNameText;
    public Text valueText;
    public Slider slider;

    private SettingItem _data;
    private bool _isInitialized;

    public void Setup(SettingItem data)
    {
        _data = data;

        if (_data == null)
            return;

        if (itemNameText != null)
            itemNameText.text = _data.itemName;

        if (slider == null)
            return;

        slider.minValue = _data.minValue;
        slider.maxValue = _data.maxValue;

        float value = _data.defaultValue;

        if (GameSettingsManager.Instance != null && _data.settingKey != SettingKey.None)
            value = GameSettingsManager.Instance.GetFloat(_data.settingKey, _data.defaultValue);

        value = Mathf.Clamp(value, slider.minValue, slider.maxValue);

        slider.onValueChanged.RemoveListener(OnSliderChanged);
        slider.value = value;
        slider.onValueChanged.AddListener(OnSliderChanged);

        _isInitialized = true;

        UpdateValueText(value);
        SaveAndApplySetting(value);
    }

    private void OnSliderChanged(float value)
    {
        if (!_isInitialized)
            return;

        UpdateValueText(value);
        SaveAndApplySetting(value);
    }

    private void UpdateValueText(float value)
    {
        if (valueText == null || slider == null)
            return;

        float range = slider.maxValue - slider.minValue;
        float percentage = range > 0f ? (value - slider.minValue) / range : 0f;
        int displayValue = Mathf.RoundToInt(percentage * 100f);

        valueText.text = displayValue + "%";
    }

    private void SaveAndApplySetting(float value)
    {
        if (GameSettingsManager.Instance == null || _data == null || _data.settingKey == SettingKey.None)
            return;

        GameSettingsManager.Instance.SetFloat(_data.settingKey, value);
    }
}