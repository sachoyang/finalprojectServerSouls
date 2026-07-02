using System.Collections;
using Fusion;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

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

    [Header("Gate")]
    [SerializeField] private Animation gateAnimation;
    [SerializeField] private AnimationClip gateOpenClip;
    [SerializeField] private float gateOpenDelay = 0.35f;

    [Header("Sound")]
    [SerializeField] private AudioClip gateOpenSound;
    [SerializeField, Range(0f, 1f)] private float gateOpenSoundVolume = 1f;

    private bool _isPlaying;
    private bool _bossWakeUpCutscenePlayed;
    private bool _gateOpenCompleted;
    private PlayableGraph _playerKickGraph;

    public bool IsPlaying => _isPlaying;
    public bool GateOpenCompleted => _gateOpenCompleted;

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
            StartCoroutine(PlayGateKickRoutine(null));
        }
    }

    public void PlayGateKickCutscene(NetworkRunner runner, PlayerRef kickPlayer)
    {
        if (!_isPlaying)
        {
            StartCoroutine(PlayGateKickRoutine(runner, kickPlayer));
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

    private IEnumerator PlayGateKickRoutine(NetworkRunner runner, PlayerRef kickPlayer = default)
    {
        _isPlaying = true;
        _gateOpenCompleted = false;

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

        yield return PlayGateOpenAnimationRoutine();
        _gateOpenCompleted = true;
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
            // 씬 전체 NetworkObject 탐색 대신 PlayerRegistry가 관리하는 로컬 플레이어를 사용한다.
            NetworkPlayerController localPlayer = PlayerRegistry.LocalPlayer;
            if (localPlayer != null)
            {
                playerObject = localPlayer.Object;
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

    private IEnumerator PlayGateOpenAnimationRoutine()
    {
        if (gateAnimation == null)
        {
            Debug.LogWarning("[CutsceneManager] Gate Animation is not assigned.");
            yield break;
        }

        AnimationState gateOpenState = null;
        if (gateOpenClip != null)
        {
            if (gateAnimation.GetClip(gateOpenClip.name) == null)
            {
                gateAnimation.AddClip(gateOpenClip, gateOpenClip.name);
            }

            gateOpenState = gateAnimation[gateOpenClip.name];
        }
        else if (gateAnimation.clip != null)
        {
            gateOpenState = gateAnimation[gateAnimation.clip.name];
        }

        // 기본 클립을 지정하지 않은 경우에도 등록된 첫 단일 클립을 사용한다.
        foreach (AnimationState state in gateAnimation)
        {
            if (gateOpenState == null && state != null && state.clip != null)
            {
                gateOpenState = state;
            }
        }

        if (gateOpenState == null)
        {
            Debug.LogWarning("[CutsceneManager] Gate Animation has no clips.");
            yield break;
        }

        gateOpenState.time = 0f;
        gateOpenState.wrapMode = WrapMode.ClampForever;

        if (gateOpenSound != null && SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySFX_3D(
                gateOpenSound,
                gateAnimation.transform.position,
                SoundCategory.BossGimmick,
                gateOpenSoundVolume);
        }

        float speed = Mathf.Abs(gateOpenState.speed);
        if (speed <= 0.0001f)
        {
            speed = 1f;
        }

        float duration = gateOpenState.length / speed;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            gateOpenState.clip.SampleAnimation(
                gateAnimation.gameObject,
                Mathf.Min(elapsed * speed, gateOpenState.length));
            elapsed += Time.deltaTime;
            yield return null;
        }

        gateOpenState.clip.SampleAnimation(gateAnimation.gameObject, gateOpenState.length);
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

}
