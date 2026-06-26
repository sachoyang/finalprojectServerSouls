using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class SettingUIController : MonoBehaviour
{
    [Header("Data Settings")]
    public SettingData currentTabData;

    [Header("UI References")]
    public Transform contentParent;

    [Header("Prefabs")]
    public GameObject categoryPrefab;  // OptionTop
    public GameObject itemPrefab;      // OptionMid (버튼형)
    public GameObject sliderItemPrefab; // OptionMid_Slider (슬라이더형)

    void Start()
    {
        if (currentTabData != null)
        {
            RefreshUI();
        }
    }

    public void ChangeTab(SettingData newData)
    {
        if (newData == null) return;
        currentTabData = newData;
        RefreshUI();
    }

    public void RefreshUI()
    {
        // 1. 기존 UI 제거
        foreach (Transform child in contentParent)
        {
            Destroy(child.gameObject);
        }

        // 2. 데이터 기반 생성
        foreach (var cat in currentTabData.categories)
        {
            // 대항목 생성
            GameObject catObj = Instantiate(categoryPrefab, contentParent);
            Text categoryText = catObj.GetComponentInChildren<Text>();
            if (categoryText != null) categoryText.text = cat.categoryName;

            // 중항목 생성
            foreach (var item in cat.items)
            {
                // 타입에 따라 프리팹 결정
                GameObject prefabToUse = (item.itemType == SettingType.Slider) ? sliderItemPrefab : itemPrefab;
                GameObject itemObj = Instantiate(prefabToUse, contentParent);

                // 타입에 맞는 셋업 실행
                if (item.itemType == SettingType.Slider)
                {
                    SettingSliderUI sliderScript = itemObj.GetComponent<SettingSliderUI>();
                    if (sliderScript != null) sliderScript.Setup(item);
                }
                else
                {
                    SettingItemUI itemScript = itemObj.GetComponent<SettingItemUI>();
                    if (itemScript != null) itemScript.Setup(item);
                }
            }
        }
    }
}