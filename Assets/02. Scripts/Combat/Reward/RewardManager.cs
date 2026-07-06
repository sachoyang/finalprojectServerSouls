using System;
using System.Collections;
using System.Collections.Generic;
using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;

public class RewardManager : MonoBehaviour
{
    private const string RewardSelectCanvasPrefabId = "RewardSelectCanvas";
    public static RewardManager Active { get; private set; }

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

    [Header("Sound")]
    [Tooltip("보스가 죽었을 때 재생할 승리/보상 테마곡")]
    [SerializeField] private AudioClip rewardBGM;

    [Header("Reward Selection Phase")]
    [SerializeField] private GameObject distortionAllProperties;
    [SerializeField] private RewardDistortionTrigger distortionTrigger;
    [SerializeField] private float rewardSelectionTimeout = 60f;
    [SerializeField] private string nextSceneName = "scPath";

    [Tooltip("다음 층이 '마지막 보스층'일 때 대신 로드할 전용 Path 씬.\n" +
             "(마지막 층 여부는 GameProgressionManager.maxLevel 기준. Build Settings에 등록 필수!)")]
    [SerializeField] private string lastPathSceneName = "scPathLast";

    private bool _rewardStarted;
    private bool _rewardOptionsOpened;
    private bool _sceneLoadRequested;
    private GameObject _spawnedChest;
    private readonly List<PlayerAbilityModule> _pendingOptions = new List<PlayerAbilityModule>();

    public int LastClearedBossStage => bossStage;
    public IReadOnlyList<PlayerAbilityModule> PendingOptions => _pendingOptions;

    // 보상 UI는 플레이어 프리팹을 찾지 않고 현재 스테이지의 RewardManager 이벤트만 구독한다.
    public event Action<int, IReadOnlyList<PlayerAbilityModule>> BossRewardOffered;
    public event Action<PlayerAbilityModule> BossRewardSelected;

    private void Awake()
    {
        Active = this;
        chestOpenFallbackDuration = Mathf.Max(1.5f, chestOpenFallbackDuration);
        SetDistortionActive(false);
    }

    private void OnDestroy()
    {
        if (Active == this)
        {
            Active = null;
        }
    }

    public void BeginReward()
    {
        if (_rewardStarted)
            return;

        _rewardStarted = true;
        bossStage = GetCurrentBossStage();
        StartCoroutine(PlayRewardSequence());
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

        if (rewardBGM != null && SoundManager.Instance != null)
        {
            SoundManager.Instance.PlayBGM(rewardBGM);
            Debug.Log("[RewardManager] 보스 처치! 보상 BGM으로 교체합니다.");
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

        PlayerAbilityInventory inventory = FindLocalAbilityInventory();
        if (inventory == null)
        {
            Debug.LogWarning("[RewardManager] 로컬 PlayerAbilityInventory를 찾지 못했습니다.");
            return;
        }

        // 보상 후보와 선택 대기 상태는 스테이지 보상 흐름을 소유한 RewardManager가 관리한다.
        _pendingOptions.Clear();
        _pendingOptions.AddRange(inventory.GenerateRewardOptions(bossStage, 3));
        if (_pendingOptions.Count == 0)
        {
            Debug.LogWarning("[RewardManager] 표시할 스킬 보상 후보가 없습니다.");
            return;
        }

        ShowRewardSelectCanvas();

        _rewardOptionsOpened = true;
        BossRewardOffered?.Invoke(bossStage, _pendingOptions);
        Debug.Log("[RewardManager] Boss reward offered to local player.");
    }

    // UI는 선택된 후보의 인덱스만 전달하고, 실제 플레이어별 장착과 저장은 해당 플레이어 컴포넌트에 위임한다.
    public bool SelectPendingOption(int optionIndex)
    {
        if (optionIndex < 0 || optionIndex >= _pendingOptions.Count)
        {
            return false;
        }

        NetworkPlayerController localPlayer = PlayerRegistry.LocalPlayer;
        PlayerAbilityInventory inventory =
            localPlayer != null ? localPlayer.GetComponent<PlayerAbilityInventory>() : null;
        if (inventory == null)
        {
            Debug.LogWarning("[RewardManager] 보상을 적용할 로컬 PlayerAbilityInventory를 찾지 못했습니다.");
            return false;
        }

        PlayerAbilityModule selected = _pendingOptions[optionIndex];
        if (!inventory.SelectRewardOption(selected))
        {
            return false;
        }

        // 선택 완료 여부는 플레이어별 네트워크/세션 데이터에 기록해 다음 씬 전환 조건에 사용한다.
        localPlayer.GetComponent<NetworkPlayerData>()?.MarkRewardSelected(bossStage);

        _pendingOptions.Clear();
        BossRewardSelected?.Invoke(selected);
        return true;
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

    // 보스 클리어 후 이동할 Path 씬 결정.
    //  - 다음 층이 마지막 보스층이면 → 전용 씬(scPathLast)
    //  - 그 외(마지막 보스를 방금 잡은 경우 포함) → 일반 scPath (포탈이 엔딩으로 보내줌)
    private string GetNextSceneName()
    {
        GameProgressionManager gpm = GameProgressionManager.Instance;
        if (gpm != null && !gpm.IsFinalLevel && gpm.IsNextLevelFinal)
        {
            return lastPathSceneName;
        }

        return nextSceneName;
    }

    private void LoadNextScene()
    {
        if (_sceneLoadRequested)
        {
            return;
        }

        _sceneLoadRequested = true;
        string targetSceneName = GetNextSceneName();

        NetworkRunner runner = GetRunner();
        if (runner != null)
        {
            if (runner.IsServer)
            {
                if (!Application.CanStreamedLevelBeLoaded(targetSceneName))
                {
                    Debug.LogError($"[RewardManager] Next scene '{targetSceneName}' is not registered in Build Settings.");
                    return;
                }

                PlayerSessionStore.SaveActivePlayerStats(runner);
                runner.LoadScene(targetSceneName, LoadSceneMode.Single);
            }

            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(targetSceneName))
        {
            Debug.LogError($"[RewardManager] Next scene '{targetSceneName}' is not registered in Build Settings.");
            return;
        }

        SceneManager.LoadScene(targetSceneName);
    }

    private static PlayerAbilityInventory FindLocalAbilityInventory()
    {
        NetworkPlayerController localPlayer = PlayerRegistry.LocalPlayer;
        return localPlayer != null ? localPlayer.GetComponent<PlayerAbilityInventory>() : null;
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
