using System.Collections;
using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;

public class RewardManager : MonoBehaviour
{
    private const string RewardSelectCanvasPrefabId = "RewardSelectCanvas";

    [Header("Boss")]
    [SerializeField] private NetworkBossCore boss;
    [SerializeField, Range(1, 8)] private int bossStage = 1;
    [SerializeField] private bool useCurrentProgressionLevel = true;
    [SerializeField] private float bossDeathDelay = 2f;

    [Header("Chest")]
    [SerializeField] private GameObject goldChestPrefab;
    [SerializeField] private Transform goldChestSpawnPoint;
    [SerializeField] private string chestOpenStateName = "BoxOpen";
    [SerializeField] private float chestOpenDelay = 0.25f;
    [SerializeField] private float chestOpenFallbackDuration = 1.5f;

    [Header("Reward Selection Phase")]
    [SerializeField] private GameObject distortionAllProperties;
    [SerializeField] private RewardDistortionTrigger distortionTrigger;
    [SerializeField] private float rewardSelectionTimeout = 60f;
    [SerializeField] private string nextSceneName = "scPath";

    private bool _rewardStarted;
    private bool _rewardOptionsOpened;
    private bool _sceneLoadRequested;
    private GameObject _spawnedChest;

    private void Awake()
    {
        chestOpenFallbackDuration = Mathf.Max(1.5f, chestOpenFallbackDuration);
        SetDistortionActive(false);
    }

    private void Update()
    {
        if (_rewardStarted)
        {
            return;
        }

        boss ??= FindObjectOfType<NetworkBossCore>();
        if (boss == null || !boss.IsSpawnedReady)
        {
            return;
        }

        if (boss.CurrentState == BossState.Die || (boss.CurrentHP <= 0f && boss.CurrentState != BossState.Sleep))
        {
            _rewardStarted = true;
            bossStage = GetCurrentBossStage();
            StartCoroutine(PlayRewardSequence());
        }
    }

    private int GetCurrentBossStage()
    {
        if (useCurrentProgressionLevel && GameProgressionManager.Instance != null)
        {
            return Mathf.Clamp(GameProgressionManager.Instance.CurrentLevel, 1, 8);
        }

        return Mathf.Clamp(bossStage, 1, 8);
    }

    private IEnumerator PlayRewardSequence()
    {
        if (bossDeathDelay > 0f)
        {
            yield return new WaitForSeconds(bossDeathDelay);
        }

        Transform spawnPoint = goldChestSpawnPoint != null ? goldChestSpawnPoint : transform;
        _spawnedChest = SpawnGoldChest(spawnPoint);

        if (_spawnedChest != null)
        {
            CutsceneManager cutsceneManager = CutsceneManager.Instance;
            if (cutsceneManager != null)
            {
                yield return cutsceneManager.PlayGoldChestCutscene();
            }
            else
            {
                Debug.LogWarning("[RewardManager] CutsceneManager was not found.");
            }

            yield return new WaitForSeconds(chestOpenDelay);
            PrepareDistortionTrigger();
            SetDistortionActive(true);
            PlayChestOpenAnimation(_spawnedChest);

            yield return WaitForChestOpenAnimation(_spawnedChest);

            if (cutsceneManager != null)
            {
                yield return cutsceneManager.RestoreGameplayCamera();
            }
        }

        yield return WaitForRewardSelectionPhase();
    }

    private GameObject SpawnGoldChest(Transform spawnPoint)
    {
        if (goldChestPrefab == null)
        {
            Debug.LogWarning("[RewardManager] Gold Chest Prefab is not assigned.");
            return null;
        }

        GameObject chest = Instantiate(goldChestPrefab, spawnPoint.position, spawnPoint.rotation);
        Animator chestAnimator = chest.GetComponentInChildren<Animator>();
        if (chestAnimator != null)
        {
            chestAnimator.enabled = false;
        }

        return chest;
    }

    private void PlayChestOpenAnimation(GameObject chest)
    {
        Animator chestAnimator = chest.GetComponentInChildren<Animator>();
        if (chestAnimator == null)
        {
            Debug.LogWarning("[RewardManager] Gold Chest does not have an Animator.");
            return;
        }

        chestAnimator.enabled = true;
        chestAnimator.Play(chestOpenStateName, 0, 0f);
    }

    private IEnumerator WaitForChestOpenAnimation(GameObject chest)
    {
        Animator chestAnimator = chest != null ? chest.GetComponentInChildren<Animator>() : null;
        if (chestAnimator == null)
        {
            yield return new WaitForSeconds(GetChestOpenAnimationDuration(null));
            yield break;
        }

        yield return null;

        AnimatorStateInfo stateInfo = chestAnimator.GetCurrentAnimatorStateInfo(0);
        float fallbackDuration = GetChestOpenAnimationDuration(chestAnimator, stateInfo);
        float elapsed = 0f;
        while (elapsed < fallbackDuration)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private float GetChestOpenAnimationDuration(Animator chestAnimator, AnimatorStateInfo stateInfo = default)
    {
        if (chestAnimator == null || chestAnimator.runtimeAnimatorController == null)
        {
            return Mathf.Max(0.01f, chestOpenFallbackDuration);
        }

        AnimationClip[] clips = chestAnimator.runtimeAnimatorController.animationClips;
        foreach (AnimationClip clip in clips)
        {
            if (clip != null && clip.name == chestOpenStateName)
            {
                float speed = Mathf.Abs(stateInfo.speed);
                if (speed <= 0.0001f)
                {
                    speed = 1f;
                }

                return Mathf.Max(0.01f, clip.length / speed);
            }
        }

        return Mathf.Max(0.01f, chestOpenFallbackDuration);
    }

    private void OfferRewardToLocalPlayer()
    {
        if (_rewardOptionsOpened)
        {
            return;
        }

        PlayerAbilityRewardController rewardController = FindLocalRewardController();
        if (rewardController == null)
        {
            Debug.LogWarning("[RewardManager] Local PlayerAbilityRewardController was not found.");
            return;
        }

        ShowRewardSelectCanvas();

        _rewardOptionsOpened = true;
        rewardController.OfferBossReward(bossStage);
        Debug.Log("[RewardManager] Boss reward offered to local player.");
    }

    private static void ShowRewardSelectCanvas()
    {
        if (ScenePrefabManager.Instance != null)
        {
            ScenePrefabManager.Instance.ShowPrefab(RewardSelectCanvasPrefabId);
            return;
        }

        RewardSelectView rewardSelectView = FindObjectOfType<RewardSelectView>(true);
        if (rewardSelectView != null)
        {
            rewardSelectView.gameObject.SetActive(true);
        }
    }

    private IEnumerator WaitForRewardSelectionPhase()
    {
        PrepareDistortionTrigger();
        float elapsed = 0f;
        float timeout = Mathf.Max(0f, rewardSelectionTimeout);

        while (elapsed < timeout && !HaveAllActivePlayersSelectedReward())
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        CleanupDistortionTrigger();
        SetDistortionActive(false);
        LoadNextScene();
    }

    private void PrepareDistortionTrigger()
    {
        if (distortionTrigger == null && distortionAllProperties != null)
        {
            distortionTrigger = distortionAllProperties.GetComponentInChildren<RewardDistortionTrigger>(true);
            if (distortionTrigger == null)
            {
                distortionTrigger = distortionAllProperties.AddComponent<RewardDistortionTrigger>();
            }
        }

        if (distortionTrigger != null)
        {
            distortionTrigger.Triggered -= OnDistortionTriggered;
            distortionTrigger.Triggered += OnDistortionTriggered;
        }
    }

    private void CleanupDistortionTrigger()
    {
        if (distortionTrigger != null)
        {
            distortionTrigger.Triggered -= OnDistortionTriggered;
        }
    }

    private void OnDistortionTriggered()
    {
        OfferRewardToLocalPlayer();
    }

    private void SetDistortionActive(bool isActive)
    {
        if (distortionAllProperties != null)
        {
            distortionAllProperties.SetActive(isActive);
        }
    }

    private bool HaveAllActivePlayersSelectedReward()
    {
        NetworkRunner runner = GetRunner();
        if (runner == null)
        {
            return false;
        }

        int playerCount = 0;
        foreach (PlayerRef player in runner.ActivePlayers)
        {
            playerCount++;
            if (!PlayerSessionStore.HasSelectedReward(player, bossStage))
            {
                return false;
            }
        }

        return playerCount > 0;
    }

    private void LoadNextScene()
    {
        if (_sceneLoadRequested)
        {
            return;
        }

        _sceneLoadRequested = true;
        NetworkRunner runner = GetRunner();
        if (runner != null)
        {
            if (runner.IsServer)
            {
                if (!Application.CanStreamedLevelBeLoaded(nextSceneName))
                {
                    Debug.LogError($"[RewardManager] Next scene '{nextSceneName}' is not registered in Build Settings.");
                    return;
                }

                PlayerSessionStore.SaveActivePlayerStats(runner);
                runner.LoadScene(nextSceneName, LoadSceneMode.Single);
            }

            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(nextSceneName))
        {
            Debug.LogError($"[RewardManager] Next scene '{nextSceneName}' is not registered in Build Settings.");
            return;
        }

        SceneManager.LoadScene(nextSceneName);
    }

    private static PlayerAbilityRewardController FindLocalRewardController()
    {
        PlayerAbilityRewardController[] rewardControllers = FindObjectsOfType<PlayerAbilityRewardController>(true);
        foreach (PlayerAbilityRewardController rewardController in rewardControllers)
        {
            NetworkObject networkObject = rewardController.GetComponent<NetworkObject>();
            if (networkObject == null || networkObject.HasInputAuthority)
            {
                return rewardController;
            }
        }

        return null;
    }

    private static NetworkRunner GetRunner()
    {
        if (NetworkManager.Instance != null && NetworkManager.Instance.Runner != null)
        {
            return NetworkManager.Instance.Runner;
        }

        return FindObjectOfType<NetworkRunner>();
    }
}
