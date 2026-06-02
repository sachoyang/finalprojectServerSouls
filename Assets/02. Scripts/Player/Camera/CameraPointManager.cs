using System.Collections;
using UnityEngine;

public class CameraPointManager : MonoBehaviour
{
    public static CameraPointManager Instance { get; private set; }

    [Header("Gold Chest Cutscene")]
    [SerializeField] private Transform goldChestCameraPoint;
    [SerializeField] private float goldChestCameraMoveDuration = 1.2f;
    [SerializeField] private float goldChestCameraFieldOfView = 35f;

    public Transform GoldChestCameraPoint => goldChestCameraPoint;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public IEnumerator PlayGoldChestCutscene(CameraManager cameraManager)
    {
        if (cameraManager == null)
        {
            yield break;
        }

        if (goldChestCameraPoint == null)
        {
            Debug.LogWarning("[CameraPointManager] Gold Chest Camera Point is not assigned.");
            yield break;
        }

        cameraManager.BeginRewardCutscene();
        yield return cameraManager.ZoomToPoint(
            goldChestCameraPoint,
            goldChestCameraMoveDuration,
            goldChestCameraFieldOfView);
    }
}
