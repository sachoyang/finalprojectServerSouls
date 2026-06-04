using UnityEngine;

public class CameraPointManager : MonoBehaviour
{
    public static CameraPointManager Instance { get; private set; }

    [Header("Gold Chest Cutscene")]
    [SerializeField] private Transform goldChestCameraPoint;

    [Header("Boss Wake Up Cutscene")]
    [SerializeField] private Transform bossWakeUpCameraPoint;

    [Header("Gate Kick Cutscene")]
    [SerializeField] private Transform gateKickCameraPoint;

    public Transform GoldChestCameraPoint => goldChestCameraPoint;
    public Transform BossWakeUpCameraPoint => bossWakeUpCameraPoint;
    public Transform GateKickCameraPoint => gateKickCameraPoint;

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
}
