using UnityEngine;
using UnityEngine.UI;

public class SettingSliderUI : MonoBehaviour
{
    public Text itemNameText;
    public Text valueText;
    public Slider slider;

    public void Setup(SettingItem data)
    {
        itemNameText.text = data.itemName;

        // 1. 데이터 설정
        slider.minValue = data.minValue;
        slider.maxValue = data.maxValue;
        slider.value = data.defaultValue;

        // 2. 이벤트 연결 전 기존 리스너 제거
        slider.onValueChanged.RemoveAllListeners();
        slider.onValueChanged.AddListener(OnSliderChanged);

        // 3. 초기 실행
        OnSliderChanged(slider.value);
    }

    void OnSliderChanged(float value)
    {
        if (valueText != null)
        {
            // 현재 값이 거꾸로 나온다면 (max - 현재값 + min) 형태로 계산하거나
            // 슬라이더 인스펙터의 'Reverse' 설정을 체크해야 합니다.
            // 가장 확실한 방법은 아래처럼 퍼센트를 계산하는 것입니다.

            float range = slider.maxValue - slider.minValue;
            float percentage = (value - slider.minValue) / range;

            // 만약 UI상에서 오른쪽이 0, 왼쪽이 100으로 되어있다면 
            // 100 - (percentage * 100) 식으로 가야 하지만,
            // 보통은 아래 표준 방식이 맞습니다.
            int displayValue = Mathf.RoundToInt(percentage * 100);

            valueText.text = displayValue.ToString() + "%";
        }
    }
}