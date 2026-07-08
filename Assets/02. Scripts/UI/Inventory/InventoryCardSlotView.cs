using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class InventoryCardSlotView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("UI")]
    [SerializeField] private Image iconImage;

    private PlayerAbilityModule currentModule;
    private int currentLevel;
    private InventoryTooltipView tooltipView;

    public void SetModule(PlayerAbilityModule module, int level, InventoryTooltipView tooltip)
    {
        currentModule = module;
        currentLevel = Mathf.Clamp(level, 1, module != null ? module.MaxLevel : 1);
        tooltipView = tooltip;

        if (iconImage != null)
        {
            iconImage.sprite = currentModule != null ? currentModule.Icon : null;
            iconImage.enabled = currentModule != null && currentModule.Icon != null;
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (tooltipView != null && currentModule != null)
            tooltipView.Show(currentModule, currentLevel);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (tooltipView != null)
            tooltipView.Hide();
    }
}
