using System.Collections;
using UnityEngine;

[DefaultExecutionOrder(1000)]
public class CameraManager : MonoBehaviour
{
    public static CameraManager Instance { get; private set; }

    [SerializeField] private Camera managedCamera;
    [SerializeField] private Transform rewardCameraPoint;
    [SerializeField] private float rewardCameraMoveDuration = 1.2f;
    [SerializeField] private float rewardZoomFieldOfView = 35f;
    [SerializeField] private bool restoreControllersAfterReward = true;

    private MonoBehaviour[] _disabledCameraControllers;
    private bool[] _cameraControllerWasEnabled;
    private bool _restoreControllersOnEnd;
    private bool _isCutsceneActive;
    private float _originalFieldOfView;
    private Vector3 _cutscenePosition;
    private Quaternion _cutsceneRotation;
    private float _cutsceneFieldOfView;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        ResolveCamera();
    }

    private void LateUpdate()
    {
        if (!_isCutsceneActive || managedCamera == null)
        {
            return;
        }

        managedCamera.transform.SetPositionAndRotation(_cutscenePosition, _cutsceneRotation);
        managedCamera.fieldOfView = _cutsceneFieldOfView;
    }

    public static CameraManager GetOrCreate()
    {
        if (Instance != null)
        {
            return Instance;
        }

        CameraManager existing = FindObjectOfType<CameraManager>(true);
        if (existing != null)
        {
            Instance = existing;
            return existing;
        }

        GameObject managerObject = new GameObject("CameraManager");
        return managerObject.AddComponent<CameraManager>();
    }

    public void BeginRewardCutscene()
    {
        BeginCutscene(restoreControllersAfterReward);
    }

    public IEnumerator ZoomToRewardPoint()
    {
        if (rewardCameraPoint == null)
        {
            Debug.LogWarning("[CameraManager] Reward Camera Point is not assigned.");
            yield break;
        }

        yield return ZoomToPoint(rewardCameraPoint, rewardCameraMoveDuration, rewardZoomFieldOfView);
    }

    public void BeginCutscene(bool restoreControllersOnEnd = true)
    {
        if (!ResolveCamera())
        {
            Debug.LogWarning("[CameraManager] No active camera was found for cutscene.");
            return;
        }

        _restoreControllersOnEnd = restoreControllersOnEnd;
        _originalFieldOfView = managedCamera.fieldOfView;

        _disabledCameraControllers = managedCamera.GetComponents<MonoBehaviour>();
        _cameraControllerWasEnabled = new bool[_disabledCameraControllers.Length];

        for (int i = 0; i < _disabledCameraControllers.Length; i++)
        {
            MonoBehaviour controller = _disabledCameraControllers[i];
            _cameraControllerWasEnabled[i] = controller != null && controller.enabled;

            if (controller != null && controller != this)
            {
                controller.enabled = false;
            }
        }
    }

    public IEnumerator ZoomToTarget(Transform target, Vector3 localOffset, float lookHeight, float duration, float fieldOfView)
    {
        if (!ResolveCamera() || target == null)
        {
            yield break;
        }

        Vector3 startPosition = managedCamera.transform.position;
        Quaternion startRotation = managedCamera.transform.rotation;
        float startFieldOfView = managedCamera.fieldOfView;

        Vector3 lookPoint = target.position + Vector3.up * lookHeight;
        Vector3 targetPosition = lookPoint + target.TransformDirection(localOffset);
        Quaternion targetRotation = Quaternion.LookRotation(lookPoint - targetPosition, Vector3.up);
        float targetFieldOfView = Mathf.Clamp(fieldOfView, 1f, 179f);

        float elapsed = 0f;
        float safeDuration = Mathf.Max(0.01f, duration);
        while (elapsed < safeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / safeDuration);
            ApplyCutsceneCamera(
                Vector3.Lerp(startPosition, targetPosition, t),
                Quaternion.Slerp(startRotation, targetRotation, t),
                Mathf.Lerp(startFieldOfView, targetFieldOfView, t));

            yield return null;
        }

        ApplyCutsceneCamera(targetPosition, targetRotation, targetFieldOfView);
    }

    public IEnumerator ZoomToPoint(Transform point, float duration, float fieldOfView)
    {
        if (!ResolveCamera() || point == null)
        {
            yield break;
        }

        Vector3 startPosition = managedCamera.transform.position;
        Quaternion startRotation = managedCamera.transform.rotation;
        float startFieldOfView = managedCamera.fieldOfView;
        float targetFieldOfView = Mathf.Clamp(fieldOfView, 1f, 179f);

        float elapsed = 0f;
        float safeDuration = Mathf.Max(0.01f, duration);
        while (elapsed < safeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / safeDuration);
            ApplyCutsceneCamera(
                Vector3.Lerp(startPosition, point.position, t),
                Quaternion.Slerp(startRotation, point.rotation, t),
                Mathf.Lerp(startFieldOfView, targetFieldOfView, t));

            yield return null;
        }

        ApplyCutsceneCamera(point.position, point.rotation, targetFieldOfView);
    }

    public void EndCutscene()
    {
        _isCutsceneActive = false;

        if (managedCamera != null)
        {
            managedCamera.fieldOfView = _originalFieldOfView;
        }

        if (!_restoreControllersOnEnd ||
            _disabledCameraControllers == null ||
            _cameraControllerWasEnabled == null)
        {
            return;
        }

        for (int i = 0; i < _disabledCameraControllers.Length; i++)
        {
            if (_disabledCameraControllers[i] != null)
            {
                _disabledCameraControllers[i].enabled = _cameraControllerWasEnabled[i];
            }
        }
    }

    private bool ResolveCamera()
    {
        if (managedCamera != null && managedCamera.isActiveAndEnabled)
        {
            return true;
        }

        managedCamera = Camera.main;
        if (managedCamera != null)
        {
            return true;
        }

        Camera[] cameras = FindObjectsOfType<Camera>(true);
        foreach (Camera camera in cameras)
        {
            if (camera != null && camera.isActiveAndEnabled && camera.targetTexture == null)
            {
                managedCamera = camera;
                return true;
            }
        }

        return false;
    }

    private void ApplyCutsceneCamera(Vector3 position, Quaternion rotation, float fieldOfView)
    {
        _isCutsceneActive = true;
        _cutscenePosition = position;
        _cutsceneRotation = rotation;
        _cutsceneFieldOfView = fieldOfView;

        if (managedCamera != null)
        {
            managedCamera.transform.SetPositionAndRotation(position, rotation);
            managedCamera.fieldOfView = fieldOfView;
        }
    }
}
