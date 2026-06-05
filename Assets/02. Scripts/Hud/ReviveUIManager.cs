using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class ReviveUIManager : MonoBehaviour
{
    [Header("Scan")]
    [SerializeField] private float refreshInterval = 0.2f;

    [Header("Screen Indicator")]
    [SerializeField] private Vector3 headOffset = new Vector3(0f, 2.35f, 0f);
    [SerializeField] private Vector2 screenOffset = new Vector2(0f, 0f);
    [SerializeField] private Vector2 indicatorSize = new Vector2(96f, 96f);
    [SerializeField] private bool hideLocalPlayerReviveUI = false;

    [Header("Sprites")]
    [SerializeField] private Sprite backgroundSprite;
    [SerializeField] private Sprite[] fillSprites = new Sprite[3];
    [SerializeField] private Sprite[] glowSprites = new Sprite[3];

    private readonly Dictionary<PlayerStats, ReviveScreenIndicator> indicators = new Dictionary<PlayerStats, ReviveScreenIndicator>();
    private readonly List<PlayerStats> removeBuffer = new List<PlayerStats>();
    private RectTransform canvasRoot;
    private float nextRefreshTime;

    private void Awake()
    {
        EnsureCanvasRoot();
    }

    private void OnEnable()
    {
        EnsureCanvasRoot();
        RefreshIndicators();
    }

    private void OnDisable()
    {
        ClearIndicators();
    }

    private void Update()
    {
        if (Time.time >= nextRefreshTime)
        {
            nextRefreshTime = Time.time + refreshInterval;
            RefreshIndicators();
        }

        foreach (ReviveScreenIndicator indicator in indicators.Values)
        {
            if (indicator != null)
                indicator.Tick();
        }
    }

    private void RefreshIndicators()
    {
        PlayerStats[] statsList = FindObjectsOfType<PlayerStats>();

        for (int i = 0; i < statsList.Length; i++)
        {
            PlayerStats stats = statsList[i];
            if (stats == null || !stats.IsDead || ShouldHide(stats))
                continue;

            if (indicators.ContainsKey(stats))
                continue;

            ReviveScreenIndicator indicator = CreateIndicator(stats);
            indicators.Add(stats, indicator);
        }

        removeBuffer.Clear();
        foreach (KeyValuePair<PlayerStats, ReviveScreenIndicator> pair in indicators)
        {
            PlayerStats stats = pair.Key;
            if (stats == null || !stats.IsDead || ShouldHide(stats))
                removeBuffer.Add(stats);
        }

        for (int i = 0; i < removeBuffer.Count; i++)
            RemoveIndicator(removeBuffer[i]);
    }

    private bool ShouldHide(PlayerStats stats)
    {
        if (!hideLocalPlayerReviveUI)
            return false;

        NetworkPlayerController controller = stats.GetComponent<NetworkPlayerController>();
        return controller != null && controller.Object != null && controller.Object.HasInputAuthority;
    }

    private ReviveScreenIndicator CreateIndicator(PlayerStats stats)
    {
        EnsureCanvasRoot();

        GameObject indicatorObject = new GameObject("ReviveScreenIndicator", typeof(RectTransform));
        indicatorObject.transform.SetParent(canvasRoot, false);

        ReviveScreenIndicator indicator = indicatorObject.AddComponent<ReviveScreenIndicator>();
        indicator.Bind(stats, canvasRoot, headOffset, screenOffset, indicatorSize, backgroundSprite, fillSprites, glowSprites);
        return indicator;
    }

    private void EnsureCanvasRoot()
    {
        if (canvasRoot != null)
            return;

        GameObject canvasObject = new GameObject("UICanvas", typeof(RectTransform));
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObject.AddComponent<GraphicRaycaster>();

        canvasRoot = canvasObject.GetComponent<RectTransform>();
        canvasRoot.anchorMin = Vector2.zero;
        canvasRoot.anchorMax = Vector2.one;
        canvasRoot.offsetMin = Vector2.zero;
        canvasRoot.offsetMax = Vector2.zero;
    }

    private void RemoveIndicator(PlayerStats stats)
    {
        if (!indicators.TryGetValue(stats, out ReviveScreenIndicator indicator))
            return;

        indicators.Remove(stats);

        if (indicator != null)
            Destroy(indicator.gameObject);
    }

    private void ClearIndicators()
    {
        foreach (ReviveScreenIndicator indicator in indicators.Values)
        {
            if (indicator != null)
                Destroy(indicator.gameObject);
        }

        indicators.Clear();
        removeBuffer.Clear();
    }
}
