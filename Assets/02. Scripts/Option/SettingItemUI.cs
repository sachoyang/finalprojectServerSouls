using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class SettingItemUI : MonoBehaviour
{
    public Text itemNameText;
    public Text optionText;
    public Button btnPrev;
    public Button btnNext;

    private SettingItem _data;
    private List<string> _options;
    private int _currentIndex;

    public void Setup(SettingItem data)
    {
        _data = data;

        if (_data == null)
            return;

        if (itemNameText != null)
            itemNameText.text = _data.itemName;

        _options = _data.options;

        _currentIndex = Mathf.Clamp(_data.defaultIndex, 0, GetMaxOptionIndex());

        if (GameSettingsManager.Instance != null && _data.settingKey != SettingKey.None)
        {
            int savedIndex = GameSettingsManager.Instance.GetInt(_data.settingKey, _currentIndex);
            _currentIndex = Mathf.Clamp(savedIndex, 0, GetMaxOptionIndex());
        }

        if (btnPrev != null)
        {
            btnPrev.onClick.RemoveListener(PrevOption);
            btnPrev.onClick.AddListener(PrevOption);
        }

        if (btnNext != null)
        {
            btnNext.onClick.RemoveListener(NextOption);
            btnNext.onClick.AddListener(NextOption);
        }

        UpdateUI();
        ApplySetting();
    }

    private void PrevOption()
    {
        if (_options == null || _options.Count == 0)
            return;

        _currentIndex = (_currentIndex - 1 + _options.Count) % _options.Count;
        UpdateUI();
        SaveAndApplySetting();
    }

    private void NextOption()
    {
        if (_options == null || _options.Count == 0)
            return;

        _currentIndex = (_currentIndex + 1) % _options.Count;
        UpdateUI();
        SaveAndApplySetting();
    }

    private void UpdateUI()
    {
        if (optionText == null)
            return;

        if (_options == null || _options.Count == 0)
        {
            optionText.text = string.Empty;
            return;
        }

        optionText.text = _options[_currentIndex];
    }

    private void SaveAndApplySetting()
    {
        if (GameSettingsManager.Instance == null || _data == null || _data.settingKey == SettingKey.None)
            return;

        GameSettingsManager.Instance.SetInt(_data.settingKey, _currentIndex);
    }

    private void ApplySetting()
    {
        if (GameSettingsManager.Instance == null || _data == null || _data.settingKey == SettingKey.None)
            return;

        GameSettingsManager.Instance.SetInt(_data.settingKey, _currentIndex);
    }

    private int GetMaxOptionIndex()
    {
        if (_options == null || _options.Count == 0)
            return 0;

        return _options.Count - 1;
    }
}