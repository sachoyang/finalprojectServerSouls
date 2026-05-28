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
        itemNameText.text = data.itemName;
        _options = data.options;

        _currentIndex = data.defaultIndex;

        if (GameSettingsManager.Instance != null && data.settingKey != SettingKey.None)
            _currentIndex = GameSettingsManager.Instance.GetInt(data.settingKey, data.defaultIndex);

        btnPrev.onClick.RemoveAllListeners();
        btnPrev.onClick.AddListener(PrevOption);

        btnNext.onClick.RemoveAllListeners();
        btnNext.onClick.AddListener(NextOption);

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
        if (_options == null || _options.Count == 0)
            return;

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
}