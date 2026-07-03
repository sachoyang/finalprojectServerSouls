using System;
using UnityEngine;
using UnityEngine.UI;

public class RewardCardView : MonoBehaviour
{
    [Header("Skill Info")]
    [SerializeField] private Image iconImage;
    [SerializeField] private Text skillNameText;
    [SerializeField] private Text skillDescriptionText;
    [SerializeField] private Text skillLevelText;
    [SerializeField] private Text skillDivisionText;

    [Header("Select")]
    [SerializeField] private Button selectButton;
    [SerializeField] private GameObject selectedEffectObject;

    private PlayerAbilityModule module;
    private Action<RewardCardView, PlayerAbilityModule> onSelected;

    public void Setup(PlayerAbilityModule rewardModule, int currentLevel, Action<RewardCardView, PlayerAbilityModule> selectCallback)
    {
        module = rewardModule;
        onSelected = selectCallback;

        SetSelected(false);

        if (iconImage != null)
        {
            iconImage.sprite = module != null ? module.Icon : null;
            iconImage.enabled = module != null && module.Icon != null;
        }

        if (skillNameText != null)
            skillNameText.text = module != null ? module.DisplayName : "";

        if (skillDescriptionText != null)
            skillDescriptionText.text = module != null ? module.Description : "";

        if (skillLevelText != null)
        {
            int displayLevel = Mathf.Max(1, currentLevel);
            bool isMaxLevel = module != null && displayLevel >= module.MaxLevel;
            skillLevelText.text = isMaxLevel
                ? $"Lv.{displayLevel}(MAX)"
                : $"Lv.{displayLevel}";
        }

        if (skillDivisionText != null)
            skillDivisionText.text = module == null
                ? ""
                : module.IsActive
                    ? "ACTIVE"
                    : module.IsPassive
                        ? "PASSIVE"
                        : "UTILITY";

        if (selectButton != null)
        {
            selectButton.onClick.RemoveAllListeners();
            selectButton.onClick.AddListener(OnClickSelect);
        }
    }

    public void SetSelected(bool isSelected)
    {
        if (selectedEffectObject != null)
            selectedEffectObject.SetActive(isSelected);
    }

    private void OnClickSelect()
    {
        if (module != null && onSelected != null)
            onSelected.Invoke(this, module);
    }
}
