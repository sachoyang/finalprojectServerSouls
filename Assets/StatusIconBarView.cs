using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class StatusIconBarView : MonoBehaviour
{
    [Header("Unified Status Slots")]
    [SerializeField] private Image[] statusIconImages;

    [Header("More Icon")]
    [SerializeField] private Sprite moreStatusSprite;

    public void SetStatuses(IReadOnlyList<ActiveStatusUIInfo> statuses)
    {
        Clear();

        if (statuses == null || statusIconImages == null || statusIconImages.Length == 0)
        {
            return;
        }

        List<ActiveStatusUIInfo> sortedStatuses = new List<ActiveStatusUIInfo>();

        for (int i = 0; i < statuses.Count; i++)
        {
            ActiveStatusUIInfo status = statuses[i];

            if (status.Data == null || status.Data.icon == null)
            {
                continue;
            }

            sortedStatuses.Add(status);
        }

        if (sortedStatuses.Count == 0)
        {
            return;
        }

        sortedStatuses.Sort(CompareStatus);

        int slotCount = statusIconImages.Length;
        bool needMoreSlot = sortedStatuses.Count > slotCount;

        int visibleStatusCount;

        if (needMoreSlot)
        {
            visibleStatusCount = Mathf.Min(sortedStatuses.Count, slotCount - 1);
        }
        else
        {
            visibleStatusCount = Mathf.Min(sortedStatuses.Count, slotCount);
        }

        for (int i = 0; i < visibleStatusCount; i++)
        {
            SetSlot(i, sortedStatuses[i].Data.icon);
        }

        if (needMoreSlot && slotCount > 0)
        {
            SetSlot(slotCount - 1, moreStatusSprite);
        }
    }

    public void Clear()
    {
        if (statusIconImages == null)
        {
            return;
        }

        for (int i = 0; i < statusIconImages.Length; i++)
        {
            Image slot = statusIconImages[i];

            if (slot == null)
            {
                continue;
            }

            slot.sprite = null;
            slot.gameObject.SetActive(false);
        }
    }

    private int CompareStatus(ActiveStatusUIInfo left, ActiveStatusUIInfo right)
    {
        bool leftIsDebuff = left.Data != null && left.Data.isDebuff;
        bool rightIsDebuff = right.Data != null && right.Data.isDebuff;

        if (leftIsDebuff != rightIsDebuff)
        {
            return leftIsDebuff ? 1 : -1;
        }

        return left.RemainingTime.CompareTo(right.RemainingTime);
    }

    private void SetSlot(int index, Sprite sprite)
    {
        if (statusIconImages == null || index < 0 || index >= statusIconImages.Length)
        {
            return;
        }

        Image slot = statusIconImages[index];

        if (slot == null)
        {
            return;
        }

        slot.gameObject.SetActive(true);
        slot.sprite = sprite;
        slot.color = Color.white;
    }
}