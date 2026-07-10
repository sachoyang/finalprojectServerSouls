using Fusion;
using UnityEngine;

public class LockOnIndicatorView : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Canvas targetCanvas;
    [SerializeField] private Transform indicatorRoot;
    [SerializeField] private GameObject lockOnMin;
    [SerializeField] private GameObject lockOnFull;
    [SerializeField] private LockOnGaugeView gaugeView;

    [Header("Camera")]
    [SerializeField] private Camera worldCamera;

    [Header("Position")]
    [SerializeField] private Vector3 worldOffset = Vector3.zero;
    [SerializeField] private bool hideWhenBehindCamera = true;

    [Header("Player Search")]
    [SerializeField] private bool autoFindLocalPlayer = true;
    [SerializeField] private float playerSearchInterval = 0.5f;

    private NetworkPlayerController _localPlayer;
    private RectTransform _canvasRect;
    private RectTransform _indicatorRect;
    private float _nextPlayerSearchTime;

    private void Awake()
    {
        if (targetCanvas == null)
            targetCanvas = GetComponentInParent<Canvas>();

        if (targetCanvas != null)
            _canvasRect = targetCanvas.transform as RectTransform;

        if (indicatorRoot == null)
            indicatorRoot = transform;

        _indicatorRect = indicatorRoot as RectTransform;

        if (gaugeView == null)
            gaugeView = GetComponentInChildren<LockOnGaugeView>(true);

        HideVisuals();
    }

    private void LateUpdate()
    {
        RefreshCamera();

        if (autoFindLocalPlayer && (_localPlayer == null || !_localPlayer.isActiveAndEnabled))
            TryFindLocalPlayer();

        if (_localPlayer == null || !_localPlayer.IsLockOnActive || _localPlayer.CurrentLockOnTarget == null)
        {
            HideVisuals();
            return;
        }

        Transform target = _localPlayer.CurrentLockOnTarget;

        if (worldCamera == null)
        {
            HideVisuals();
            return;
        }

        Vector3 screenPosition = worldCamera.WorldToScreenPoint(target.position + worldOffset);

        if (hideWhenBehindCamera && screenPosition.z <= 0f)
        {
            HideVisuals();
            return;
        }

        MoveIndicator(screenPosition);
        ShowVisuals(target);
    }

    public void SetLocalPlayer(NetworkPlayerController player)
    {
        _localPlayer = player;
    }

    public void RefreshLocalPlayer()
    {
        _localPlayer = null;
        _nextPlayerSearchTime = 0f;
        TryFindLocalPlayer();
    }

    private void RefreshCamera()
    {
        if (worldCamera != null)
            return;

        if (targetCanvas != null && targetCanvas.worldCamera != null)
        {
            worldCamera = targetCanvas.worldCamera;
            return;
        }

        worldCamera = Camera.main;

        if (worldCamera == null)
            worldCamera = FindObjectOfType<Camera>();
    }

    private void TryFindLocalPlayer()
    {
        if (Time.unscaledTime < _nextPlayerSearchTime)
            return;

        _nextPlayerSearchTime = Time.unscaledTime + playerSearchInterval;

        NetworkPlayerController[] players = FindObjectsOfType<NetworkPlayerController>(false);
        foreach (NetworkPlayerController player in players)
        {
            if (player == null || player.Object == null)
                continue;

            if (player.Object.HasInputAuthority)
            {
                _localPlayer = player;
                return;
            }
        }
    }

    private void MoveIndicator(Vector3 screenPosition)
    {
        if (indicatorRoot == null)
            return;

        if (_indicatorRect != null && targetCanvas != null && _canvasRect != null)
        {
            Camera uiCamera = targetCanvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : targetCanvas.worldCamera;

            if (uiCamera == null && targetCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
                uiCamera = worldCamera;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _canvasRect,
                    screenPosition,
                    uiCamera,
                    out Vector2 localPoint))
            {
                _indicatorRect.anchoredPosition = localPoint;
            }

            return;
        }

        indicatorRoot.position = screenPosition;
    }

    private void ShowVisuals(Transform target)
    {
        SetObjectActive(lockOnMin, true);
        SetObjectActive(lockOnFull, true);

        if (gaugeView != null)
            gaugeView.SetTarget(target);
    }

    private void HideVisuals()
    {
        if (gaugeView != null)
            gaugeView.ClearTarget();

        SetObjectActive(lockOnMin, false);
        SetObjectActive(lockOnFull, false);
    }

    private static void SetObjectActive(GameObject target, bool active)
    {
        if (target != null && target.activeSelf != active)
            target.SetActive(active);
    }
}