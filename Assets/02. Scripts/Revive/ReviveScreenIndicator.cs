using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class ReviveScreenIndicator : MonoBehaviour
{
    private const int SegmentCount = 3;
    // 12시 방향부터 시계방향으로 회전할 때 한 세그먼트(1/3)가 차지하는 fillAmount 비율 (~0.3333f)
    private const float SingleSegmentRatio = 1f / SegmentCount; 
    private static readonly Color TextColor = new Color(0.86f, 0.84f, 1f, 1f);

    private Image fillImage; // 단일 게이지 통 이미지
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
        Sprite[] segmentFillSprites) // 글로우 매개변수 제거
    {
        if (targetStats != null)
            targetStats.ReviveStateChanged -= HandleReviveStateChanged;

        targetStats = stats;
        targetTransform = stats != null ? stats.transform : null;
        this.canvasRoot = canvasRoot;
        headOffset = offset;
        this.screenOffset = screenOffset;

        // fillSprites[0]에 등록된 통 이미지(Revive_gage3.png)를 가져와 바인딩
        Sprite fillSprite = (segmentFillSprites != null && segmentFillSprites.Length > 0) ? segmentFillSprites[0] : null;

        BuildVisuals(indicatorSize, backgroundSprite, fillSprite);

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
        
        // PlayerStats가 FixedUpdateNetwork에서 수치를 실시간 복구하므로 매 프레임 UI 동기화
        Refresh();
    }

    private void HandleReviveStateChanged(PlayerStats stats)
    {
        Refresh();
    }

    private void BuildVisuals(Vector2 indicatorSize, Sprite backgroundSprite, Sprite fillSprite)
    {
        if (rootRect != null)
            return;

        rootRect = GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0.5f, 0.5f);
        rootRect.anchorMax = new Vector2(0.5f, 0.5f);
        rootRect.pivot = new Vector2(0.5f, 0.5f);
        rootRect.sizeDelta = indicatorSize;

        // 1. 배경 이미지 생성 (Revive_Background.png)
        Image background = CreateImage("Background", backgroundSprite, indicatorSize);
        background.raycastTarget = false;
        background.type = Image.Type.Simple;

        // 2. 게이지 이미지 생성 (Revive_gage3.png)
        fillImage = CreateFilledImage("Fill_Total", fillSprite, indicatorSize);

        // 3. 중앙 텍스트 생성
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
        
        // 원본 이미지 규격인 12시 방향(Top)에서 시작하여 시계 방향으로 배치
        image.fillOrigin = (int)Image.Origin360.Top;
        image.fillClockwise = true;
        image.fillAmount = 1f;
        return image;
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
        float totalRequiredGauge = targetStats.ReviveRequiredGauge; 
        float remainingProgress = Mathf.Max(0f, targetStats.ReviveProgress); 

        // 1. 최대 요구치 대비 현재 남은 수치의 총체적 비율 (1.0에서 0.0으로 감소)
        float currentTotalRatio = totalRequiredGauge > 0.001f ? (remainingProgress / totalRequiredGauge) : 0f;

        // 2. 현재 활성화된 세그먼트에 맞춘 최대 fillAmount 상한선 (1단계: 0.3333, 2단계: 0.6666, 3단계: 1.0000)
        float maxFillLimit = activeSegments * SingleSegmentRatio;

        // 3. 최종 fillAmount 연산
        float targetFillAmount = Mathf.Clamp(currentTotalRatio * maxFillLimit, 0f, maxFillLimit);

        if (fillImage != null)
        {
            fillImage.gameObject.SetActive(targetFillAmount > 0.001f);
            fillImage.fillAmount = targetFillAmount;
        }

        // 중앙 텍스트에 실시간 남은 수치 표시
        if (valueText != null)
            valueText.text = $"{Mathf.CeilToInt(remainingProgress)}";
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
}