using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class InventoryCardSlotView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("UI")]
    [SerializeField] private Image iconImage;

    private PlayerAbilityModule currentModule;
    private InventoryTooltipView tooltipView;

    public void SetModule(PlayerAbilityModule module, InventoryTooltipView tooltip)
    {
        currentModule = module;
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
            tooltipView.Show(currentModule);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (tooltipView != null)
            tooltipView.Hide();
    }
}