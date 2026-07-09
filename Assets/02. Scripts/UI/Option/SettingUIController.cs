using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class SettingUIController : MonoBehaviour
{
    [Header("Data Settings")]
    public SettingData currentTabData;

    [Header("UI References")]
    public Transform contentParent;
    [SerializeField] private GameObject closeTarget;

    [Header("Prefabs")]
    public GameObject categoryPrefab;
    public GameObject itemPrefab;
    public GameObject sliderItemPrefab;

    private void Start()
    {
        if (currentTabData != null)
        {
            RefreshUI();
        }
    }

    public void ChangeTab(SettingData newData)
    {
        if (newData == null)
            return;

        currentTabData = newData;
        RefreshUI();
    }

    public void CloseOption()
    {
        GameObject target = closeTarget != null ? closeTarget : gameObject;
        target.SetActive(false);
    }

    public void RefreshUI()
    {
        if (contentParent == null || currentTabData == null)
            return;

        foreach (Transform child in contentParent)
        {
            Destroy(child.gameObject);
        }

        foreach (SettingCategory category in currentTabData.categories)
        {
            GameObject categoryObject = Instantiate(categoryPrefab, contentParent);
            Text categoryText = categoryObject.GetComponentInChildren<Text>();

            if (categoryText != null)
                categoryText.text = category.categoryName;

            foreach (SettingItem item in category.items)
            {
                GameObject prefabToUse =
                    item.itemType == SettingType.Slider
                        ? sliderItemPrefab
                        : itemPrefab;

                if (prefabToUse == null)
                    continue;

                GameObject itemObject = Instantiate(prefabToUse, contentParent);

                if (item.itemType == SettingType.Slider)
                {
                    SettingSliderUI sliderUI = itemObject.GetComponent<SettingSliderUI>();
                    if (sliderUI != null)
                        sliderUI.Setup(item);
                }
                else
                {
                    SettingItemUI itemUI = itemObject.GetComponent<SettingItemUI>();
                    if (itemUI != null)
                        itemUI.Setup(item);
                }
            }
        }
    }
}