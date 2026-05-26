using UnityEngine;
using UnityEngine.UI;

public class SettingSliderUI : MonoBehaviour
{
    public Text itemNameText;
    public Text valueText;
    public Slider slider;

    private SettingItem _data;

    public void Setup(SettingItem data)
    {
        _data = data;

        itemNameText.text = data.itemName;

        slider.minValue = data.minValue;
        slider.maxValue = data.maxValue;

        float savedValue = data.defaultValue;

        if (GameSettingsManager.Instance != null && data.settingKey != SettingKey.None)
            savedValue = GameSettingsManager.Instance.GetFloat(data.settingKey, data.defaultValue);

        slider.value = savedValue;

        slider.onValueChanged.RemoveAllListeners();
        slider.onValueChanged.AddListener(OnSliderChanged);

        OnSliderChanged(slider.value);
    }

    private void OnSliderChanged(float value)
    {
        if (valueText != null)
        {
            float range = slider.maxValue - slider.minValue;
            float percentage = range <= 0f ? 0f : (value - slider.minValue) / range;
            int displayValue = Mathf.RoundToInt(percentage * 100);

            valueText.text = displayValue.ToString() + "%";
        }

        if (GameSettingsManager.Instance != null && _data != null && _data.settingKey != SettingKey.None)
            GameSettingsManager.Instance.SetFloat(_data.settingKey, value);
    }
}