using UnityEngine;
using UnityEngine.UI; // 레거시 UI를 위해 반드시 필요합니다
using System.Collections.Generic;

public class SettingItemUI : MonoBehaviour
{
    // TextMeshProUGUI 대신 Text를 사용합니다
    public Text itemNameText;
    public Text optionText;
    public Button btnPrev;
    public Button btnNext;

    private List<string> _options;
    private int _currentIndex;

    public void Setup(SettingItem data)
    {
        itemNameText.text = data.itemName;
        _options = data.options;
        _currentIndex = data.defaultIndex;

        // 버튼 리스너 초기화 및 연결
        btnPrev.onClick.RemoveAllListeners();
        btnPrev.onClick.AddListener(PrevOption);

        btnNext.onClick.RemoveAllListeners();
        btnNext.onClick.AddListener(NextOption);

        UpdateUI();
    }

    void PrevOption()
    {
        if (_options == null || _options.Count == 0) return;
        _currentIndex = (_currentIndex - 1 + _options.Count) % _options.Count;
        UpdateUI();
    }

    void NextOption()
    {
        if (_options == null || _options.Count == 0) return;
        _currentIndex = (_currentIndex + 1) % _options.Count;
        UpdateUI();
    }

    void UpdateUI()
    {
        optionText.text = _options[_currentIndex];
    }
}