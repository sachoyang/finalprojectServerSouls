using System.Collections;
using Fusion;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;

public class CutsceneManager : MonoBehaviour
{
    public enum AnimatorCommandMode
    {
        Trigger,
        StateName
    }

    public static CutsceneManager Instance { get; private set; }

    [Header("Managers")]
    [SerializeField] private CameraManager cameraManager;
    [SerializeField] private CameraPointManager cameraPointManager;

    [Header("Gold Chest Cutscene")]
    [SerializeField] private float goldChestCameraMoveDuration = 1.2f;
    [SerializeField] private float goldChestCameraFieldOfView = 35f;

    [Header("Boss Wake Up Cutscene")]
    [SerializeField] private NetworkBossCore boss;
    [SerializeField] private Animator bossAnimator;
    [SerializeField] private AnimatorCommandMode bossAnimationMode = AnimatorCommandMode.StateName;
    [SerializeField] private string bossWakeUpAnimationName = "Scream";
    [SerializeField] private float bossWakeUpZoomDuration = 0.8f;
    [SerializeField] private float bossWakeUpFieldOfView = 32f;
    [SerializeField] private float bossWakeUpDuration = 2.8f;
    [SerializeField] private float bossWakeUpHoldDuration = 0.25f;
    [SerializeField] private bool playBossWakeUpOnStart = false;
    [SerializeField] private bool playBossWakeUpOnBossState = true;
    [SerializeField] private float bossWakeUpStartDelay = 0.5f;

    [Header("Gate Kick Cutscene")]
    [SerializeField] private NetworkObject playerObject;
    [SerializeField] private Animator playerAnimator;
    [SerializeField] private Transform playerKickPoint;
    [SerializeField] private AnimatorCommandMode playerAnimationMode = AnimatorCommandMode.Trigger;
    [SerializeField] private string playerKickAnimationName = "Kick2";
    [SerializeField] private AnimationClip playerKickAnimationClip;
    [SerializeField] private float gateKickCameraZoomDuration = 0.7f;
    [SerializeField] private float gateKickCameraFieldOfView = 35f;
    [SerializeField] private float playerKickDuration = 1.2f;

    [Header("Gate")]
    [SerializeField] private Animation gateAnimation;
    [SerializeField] private string gateOpenAnimationName = "DoorOpen";
    [SerializeField] private AnimationClip gateOpenAnimationClip;
    [SerializeField] private float gateOpenDelay = 0.35f;

    [Header("Scene Load")]
    [SerializeField] private string nextSceneName = "scServer_peace";
    [SerializeField] private float loadDelay = 0.25f;
    [SerializeField] private bool restoreCameraBeforeGateLoad = false;

    private bool _isPlaying;
    private bool _bossWakeUpCutscenePlayed;
    private PlayableGraph _playerKickGraph;

    public bool IsPlaying => _isPlaying;
    public float GateKickCutsceneDuration =>
        Mathf.Max(0f, gateKickCameraZoomDuration) +
        Mathf.Max(0f, gateOpenDelay) +
        Mathf.Max(0f, playerKickDuration) +
        Mathf.Max(0f, loadDelay);

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        ResolveManagers();
    }

    private void Start()
    {
        if (playBossWakeUpOnStart)
        {
            PlayBossWakeUpCutscene();
        }
    }

    private void Update()
    {
        if (!playBossWakeUpOnBossState ||
            _bossWakeUpCutscenePlayed ||
            _isPlaying)
        {
            return;
        }

        ResolveBossReferences();
        if (boss != null && boss.IsSpawnedReady && boss.CurrentState == BossState.WakeUp)
        {
            PlayBossWakeUpCutscene();
        }
    }

    private void OnDestroy()
    {
        DestroyPlayerKickGraph();

        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void PlayBossWakeUpCutscene()
    {
        if (!_isPlaying)
        {
            _bossWakeUpCutscenePlayed = true;
            StartCoroutine(PlayBossWakeUpRoutine());
        }
    }

    public IEnumerator PlayGoldChestCutscene()
    {
        ResolveManagers();

        if (cameraManager == null || cameraPointManager == null)
        {
            yield break;
        }

        Transform point = cameraPointManager.GoldChestCameraPoint;
        if (point == null)
        {
            Debug.LogWarning("[CutsceneManager] Gold Chest Camera Point is not assigned.");
            yield break;
        }

        cameraManager.BeginCutscene();
        yield return cameraManager.ZoomToPoint(point, goldChestCameraMoveDuration, goldChestCameraFieldOfView);
    }

    public IEnumerator RestoreGameplayCamera()
    {
        ResolveManagers();

        if (cameraManager == null)
        {
            yield break;
        }

        yield return cameraManager.RestoreGameplayCamera();
    }

    public void PlayGateKickCutscene()
    {
        if (!_isPlaying)
        {
            StartCoroutine(PlayGateKickRoutine(null, false));
        }
    }

    public void PlayGateKickCutscene(NetworkRunner runner, PlayerRef kickPlayer)
    {
        if (!_isPlaying)
        {
            StartCoroutine(PlayGateKickRoutine(runner, false, kickPlayer));
        }
    }

    public void PlayGateKickCutsceneAndLoad(NetworkRunner runner)
    {
        if (!_isPlaying)
        {
            StartCoroutine(PlayGateKickRoutine(runner, true));
        }
    }

    private IEnumerator PlayBossWakeUpRoutine()
    {
        _isPlaying = true;
        ResolveLocalPlayer();
        SetPlayerControlEnabled(false);

        if (bossWakeUpStartDelay > 0f)
        {
            yield return new WaitForSeconds(bossWakeUpStartDelay);
        }

        ResolveManagers();
        ResolveBossReferences();

        if (cameraManager != null && cameraPointManager != null)
        {
            Transform point = cameraPointManager.BossWakeUpCameraPoint;
            if (point != null)
            {
                cameraManager.BeginCutscene();
                yield return cameraManager.ZoomToPoint(point, bossWakeUpZoomDuration, bossWakeUpFieldOfView);
            }
            else
            {
                Debug.LogWarning("[CutsceneManager] Boss Wake Up Camera Point is not assigned.");
            }
        }

        if (boss == null || !boss.IsSpawnedReady || boss.CurrentState != BossState.WakeUp)
        {
            PlayAnimatorCommand(bossAnimator, bossAnimationMode, bossWakeUpAnimationName);
        }

        if (bossWakeUpDuration > 0f)
        {
            yield return new WaitForSeconds(bossWakeUpDuration);
        }

        if (bossWakeUpHoldDuration > 0f)
        {
            yield return new WaitForSeconds(bossWakeUpHoldDuration);
        }

        if (cameraManager != null)
        {
            yield return cameraManager.RestoreGameplayCamera();
        }

        SetPlayerControlEnabled(true);
        _isPlaying = false;
    }

    private IEnumerator PlayGateKickRoutine(NetworkRunner runner, bool loadSceneOnEnd, PlayerRef kickPlayer = default)
    {
        _isPlaying = true;

        ResolveManagers();
        ResolveGateKickPlayer(runner, kickPlayer);
        SetPlayerControlEnabled(false);
        AlignPlayerToKickPoint();

        if (cameraManager != null && cameraPointManager != null)
        {
            Transform point = cameraPointManager.GateKickCameraPoint;
            if (point != null)
            {
                cameraManager.BeginCutscene();
                yield return cameraManager.ZoomToPoint(point, gateKickCameraZoomDuration, gateKickCameraFieldOfView);
            }
            else
            {
                Debug.LogWarning("[CutsceneManager] Gate Kick Camera Point is not assigned.");
            }
        }

        PlayPlayerKickAnimation();

        if (gateOpenDelay > 0f)
        {
            yield return new WaitForSeconds(gateOpenDelay);
        }

        PlayGateOpenAnimation();

        if (playerKickDuration > 0f)
        {
            yield return new WaitForSeconds(playerKickDuration);
        }

        if (restoreCameraBeforeGateLoad && cameraManager != null)
        {
            yield return cameraManager.RestoreGameplayCamera();
        }

        if (loadSceneOnEnd && loadDelay > 0f)
        {
            yield return new WaitForSeconds(loadDelay);
        }

        if (loadSceneOnEnd)
        {
            LoadNextScene(runner);
        }

        SetPlayerControlEnabled(true);
        _isPlaying = false;
    }

    private void ResolveManagers()
    {
        if (cameraManager == null)
        {
            cameraManager = CameraManager.GetOrCreate();
        }

        if (cameraPointManager == null)
        {
            cameraPointManager = CameraPointManager.Instance != null
                ? CameraPointManager.Instance
                : FindObjectOfType<CameraPointManager>(true);
        }
    }

    private void ResolveBossReferences()
    {
        if (boss == null)
        {
            boss = FindObjectOfType<NetworkBossCore>();
        }

        if (bossAnimator == null && boss != null)
        {
            bossAnimator = boss.GetComponentInChildren<Animator>(true);
        }

        if (boss != null)
        {
            if (string.IsNullOrEmpty(bossWakeUpAnimationName))
            {
                bossWakeUpAnimationName = boss.wakeUpAnimName;
            }

            if (bossWakeUpDuration <= 0f)
            {
                bossWakeUpDuration = boss.wakeUpDuration;
            }
        }
    }

    private void ResolveLocalPlayer()
    {
        if (playerObject == null)
        {
            NetworkObject[] networkObjects = FindObjectsOfType<NetworkObject>(true);
            foreach (NetworkObject networkObject in networkObjects)
            {
                if (networkObject != null && networkObject.HasInputAuthority)
                {
                    playerObject = networkObject;
                    break;
                }
            }
        }

        if (playerAnimator == null && playerObject != null)
        {
            playerAnimator = playerObject.GetComponentInChildren<Animator>(true);
        }
    }

    private void ResolveGateKickPlayer(NetworkRunner runner, PlayerRef kickPlayer)
    {
        if (runner != null && kickPlayer != default && runner.TryGetPlayerObject(kickPlayer, out NetworkObject resolvedPlayer) && resolvedPlayer != null)
        {
            playerObject = resolvedPlayer;
            playerAnimator = playerObject.GetComponentInChildren<Animator>(true);
            return;
        }

        ResolveLocalPlayer();
    }

    private void AlignPlayerToKickPoint()
    {
        if (playerObject == null || playerKickPoint == null)
        {
            return;
        }

        playerObject.transform.SetPositionAndRotation(playerKickPoint.position, playerKickPoint.rotation);
    }

    private void SetPlayerControlEnabled(bool isEnabled)
    {
        if (playerObject == null)
        {
            return;
        }

        NetworkPlayerController playerController = playerObject.GetComponent<NetworkPlayerController>();
        if (playerController != null)
        {
            playerController.SetControlLock(PlayerControlLockFlags.All, !isEnabled);
        }
    }

    private void PlayPlayerKickAnimation()
    {
        if (playerAnimator == null)
        {
            Debug.LogWarning("[CutsceneManager] Player Animator is not assigned.");
            return;
        }

        if (playerKickAnimationClip == null)
        {
            PlayAnimatorCommand(playerAnimator, playerAnimationMode, playerKickAnimationName);
            return;
        }

        DestroyPlayerKickGraph();

        _playerKickGraph = PlayableGraph.Create("GateKickCutscene");
        AnimationPlayableOutput output = AnimationPlayableOutput.Create(_playerKickGraph, "PlayerKick", playerAnimator);
        AnimationClipPlayable clipPlayable = AnimationClipPlayable.Create(_playerKickGraph, playerKickAnimationClip);
        clipPlayable.SetApplyFootIK(true);
        output.SetSourcePlayable(clipPlayable);
        _playerKickGraph.Play();
    }

    private void DestroyPlayerKickGraph()
    {
        if (_playerKickGraph.IsValid())
        {
            _playerKickGraph.Destroy();
        }
    }

    private void PlayGateOpenAnimation()
    {
        if (gateAnimation == null)
        {
            Debug.LogWarning("[CutsceneManager] Gate Animation is not assigned.");
            return;
        }

        string clipName = !string.IsNullOrEmpty(gateOpenAnimationName)
            ? gateOpenAnimationName
            : gateOpenAnimationClip != null
                ? gateOpenAnimationClip.name
                : string.Empty;

        if (gateOpenAnimationClip != null && !string.IsNullOrEmpty(clipName) && gateAnimation.GetClip(clipName) == null)
        {
            gateAnimation.AddClip(gateOpenAnimationClip, clipName);
        }

        if (!string.IsNullOrEmpty(clipName) && gateAnimation.GetClip(clipName) != null)
        {
            gateAnimation.Play(clipName);
            return;
        }

        if (gateOpenAnimationClip != null)
        {
            gateAnimation.clip = gateOpenAnimationClip;
            gateAnimation.Play();
        }
    }

    private static void PlayAnimatorCommand(Animator animator, AnimatorCommandMode mode, string animationName)
    {
        if (animator == null || string.IsNullOrEmpty(animationName))
        {
            return;
        }

        if (mode == AnimatorCommandMode.Trigger)
        {
            animator.SetTrigger(animationName);
            return;
        }

        animator.CrossFade(Animator.StringToHash(animationName), 0.1f, 0, 0f);
    }

    private void LoadNextScene(NetworkRunner runner)
    {
        if (string.IsNullOrEmpty(nextSceneName))
        {
            return;
        }

        if (runner != null)
        {
            if (runner.IsServer)
            {
                runner.LoadScene(nextSceneName, LoadSceneMode.Single);
            }

            return;
        }

        SceneManager.LoadScene(nextSceneName);
    }
}
