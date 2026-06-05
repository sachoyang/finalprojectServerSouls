using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class ReviveScreenIndicator : MonoBehaviour
{
    private const int SegmentCount = 3;
    private static readonly float[] SegmentRotations = { 0f, -120f, -240f };
    private static readonly Color TextColor = new Color(0.86f, 0.84f, 1f, 1f);

    private readonly Image[] fillImages = new Image[SegmentCount];
    private readonly Image[] glowImages = new Image[SegmentCount];

    private PlayerStats targetStats;
    private Transform targetTransform;
    private RectTransform canvasRoot;
    private Vector3 headOffset;
    private Vector2 screenOffset;
    private RectTransform rootRect;
    private TextMeshProUGUI valueText;
    private Camera cachedCamera;
    private bool isOnScreen;

    public void Bind(
        PlayerStats stats,
        RectTransform canvasRoot,
        Vector3 offset,
        Vector2 screenOffset,
        Vector2 indicatorSize,
        Sprite backgroundSprite,
        Sprite[] segmentFillSprites,
        Sprite[] segmentGlowSprites)
    {
        if (targetStats != null)
            targetStats.ReviveStateChanged -= HandleReviveStateChanged;

        targetStats = stats;
        targetTransform = stats != null ? stats.transform : null;
        this.canvasRoot = canvasRoot;
        headOffset = offset;
        this.screenOffset = screenOffset;

        BuildVisuals(indicatorSize, backgroundSprite, segmentFillSprites, segmentGlowSprites);

        if (targetStats != null)
            targetStats.ReviveStateChanged += HandleReviveStateChanged;

        UpdateScreenPosition();
        Refresh();
    }

    private void OnDestroy()
    {
        if (targetStats != null)
            targetStats.ReviveStateChanged -= HandleReviveStateChanged;
    }

    public void Tick()
    {
        if (targetStats == null || targetTransform == null)
            return;

        UpdateScreenPosition();
        Refresh();
    }

    private void HandleReviveStateChanged(PlayerStats stats)
    {
        Refresh();
    }

    private void BuildVisuals(Vector2 indicatorSize, Sprite backgroundSprite, Sprite[] segmentFillSprites, Sprite[] segmentGlowSprites)
    {
        if (rootRect != null)
            return;

        rootRect = GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0.5f, 0.5f);
        rootRect.anchorMax = new Vector2(0.5f, 0.5f);
        rootRect.pivot = new Vector2(0.5f, 0.5f);
        rootRect.sizeDelta = indicatorSize;

        Image background = CreateImage("Background", backgroundSprite, indicatorSize);
        background.raycastTarget = false;
        background.type = Image.Type.Simple;

        for (int i = 0; i < SegmentCount; i++)
        {
            glowImages[i] = CreateFilledImage($"Glow_{i}", GetSprite(segmentGlowSprites, i), indicatorSize);
            fillImages[i] = CreateFilledImage($"Fill_{i}", GetSprite(segmentFillSprites, i), indicatorSize);
            SetSegmentRotation(glowImages[i], i);
            SetSegmentRotation(fillImages[i], i);
        }

        valueText = CreateText("ReviveValue", indicatorSize * 0.75f, new Vector2(0f, -2f));
    }

    private Image CreateImage(string objectName, Sprite sprite, Vector2 size)
    {
        GameObject child = new GameObject(objectName);
        child.transform.SetParent(rootRect, false);

        RectTransform rect = child.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = size;

        Image image = child.AddComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
        return image;
    }

    private Image CreateFilledImage(string objectName, Sprite sprite, Vector2 size)
    {
        Image image = CreateImage(objectName, sprite, size);
        image.type = Image.Type.Filled;
        image.fillMethod = Image.FillMethod.Radial360;
        image.fillOrigin = (int)Image.Origin360.Left;
        image.fillClockwise = true;
        image.fillAmount = 1f;
        return image;
    }

    private static void SetSegmentRotation(Image image, int segmentIndex)
    {
        if (image == null)
            return;

        image.rectTransform.localEulerAngles = new Vector3(0f, 0f, SegmentRotations[segmentIndex]);
    }

    private TextMeshProUGUI CreateText(string objectName, Vector2 size, Vector2 anchoredPosition)
    {
        GameObject child = new GameObject(objectName);
        child.transform.SetParent(rootRect, false);

        RectTransform rect = child.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        TextMeshProUGUI text = child.AddComponent<TextMeshProUGUI>();
        text.alignment = TextAlignmentOptions.Center;
        text.color = TextColor;
        text.fontSize = 12f;
        text.fontStyle = FontStyles.Bold;
        text.raycastTarget = false;
        return text;
    }

    private void Refresh()
    {
        if (targetStats == null)
            return;

        bool visible = targetStats.IsDead && isOnScreen;
        if (rootRect != null)
            rootRect.gameObject.SetActive(visible);

        if (!visible)
            return;

        int activeSegments = Mathf.Clamp(targetStats.ReviveSegmentCount, 1, SegmentCount);
        float segmentGauge = Mathf.Max(1f, targetStats.ReviveGaugePerSegment);
        float remaining = Mathf.Max(0f, targetStats.ReviveProgress);

        for (int i = 0; i < SegmentCount; i++)
        {
            bool active = i < activeSegments;
            float fillAmount = active ? Mathf.Clamp01((remaining - segmentGauge * i) / segmentGauge) : 0f;

            if (fillImages[i] != null)
            {
                fillImages[i].gameObject.SetActive(active && fillAmount > 0.001f);
                fillImages[i].fillAmount = fillAmount;
            }

            if (glowImages[i] != null)
            {
                glowImages[i].gameObject.SetActive(active && fillAmount > 0.001f);
                glowImages[i].fillAmount = fillAmount;
            }
        }

        if (valueText != null)
            valueText.text = $"{Mathf.CeilToInt(remaining)}";
    }

    private void UpdateScreenPosition()
    {
        if (cachedCamera == null)
            cachedCamera = Camera.main;

        if (cachedCamera == null || canvasRoot == null || rootRect == null)
            return;

        Vector3 screenPosition = cachedCamera.WorldToScreenPoint(targetTransform.position + headOffset);
        isOnScreen = screenPosition.z > 0f;
        rootRect.gameObject.SetActive(isOnScreen && targetStats != null && targetStats.IsDead);

        if (!isOnScreen)
            return;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRoot, screenPosition, null, out Vector2 localPoint))
            rootRect.anchoredPosition = localPoint + screenOffset;
    }

    private static Sprite GetSprite(Sprite[] sprites, int index)
    {
        return sprites != null && index >= 0 && index < sprites.Length ? sprites[index] : null;
    }
}
