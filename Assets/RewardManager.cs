using System.Collections;
using Fusion;
using UnityEngine;

public class RewardManager : MonoBehaviour
{
    [Header("Boss")]
    [SerializeField] private DragonBoss boss;
    [SerializeField, Range(1, 8)] private int bossStage = 1;
    [SerializeField] private float bossDeathDelay = 2f;

    [Header("Chest")]
    [SerializeField] private GameObject goldChestPrefab;
    [SerializeField] private Transform goldChestSpawnPoint;
    [SerializeField] private string chestOpenStateName = "BoxOpen";
    [SerializeField] private float chestOpenDelay = 0.25f;
    [SerializeField] private float rewardOfferDelay = 1.2f;

    private bool _rewardStarted;
    private bool _chestOpenEnded;
    private GameObject _spawnedChest;
    private GoldChestAnimationEventReceiver _chestEventReceiver;

    private void Awake()
    {
        boss ??= FindObjectOfType<DragonBoss>();
    }

    private void Update()
    {
        if (_rewardStarted || boss == null)
        {
            return;
        }

        // if (!boss.IsSpawnedReady)
        // {
        //     return;
        // }

        if (boss.CurrentState == BossState.Die || (boss.CurrentHP <= 0f && boss.CurrentState != BossState.Sleep))
        {
            _rewardStarted = true;
            StartCoroutine(PlayRewardSequence());
        }
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
            _chestOpenEnded = false;
            _chestEventReceiver = PrepareChestEventReceiver(_spawnedChest);

            CameraManager cameraManager = CameraManager.GetOrCreate();
            if (cameraManager != null)
            {
                cameraManager.BeginRewardCutscene();
                yield return cameraManager.ZoomToRewardPoint();
            }

            yield return new WaitForSeconds(chestOpenDelay);
            PlayChestOpenAnimation(_spawnedChest);

            yield return new WaitUntil(() => _chestOpenEnded);
            yield return new WaitForSeconds(rewardOfferDelay);

            if (cameraManager != null)
            {
                cameraManager.EndCutscene();
            }

            CleanupChestEventReceiver();
        }

        OfferRewardToLocalPlayer();
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

    private GoldChestAnimationEventReceiver PrepareChestEventReceiver(GameObject chest)
    {
        Animator chestAnimator = chest.GetComponentInChildren<Animator>();
        if (chestAnimator == null)
        {
            Debug.LogWarning("[RewardManager] Gold Chest does not have an Animator.");
            return null;
        }

        GoldChestAnimationEventReceiver receiver = chestAnimator.GetComponent<GoldChestAnimationEventReceiver>();
        if (receiver == null)
        {
            receiver = chestAnimator.gameObject.AddComponent<GoldChestAnimationEventReceiver>();
        }

        receiver.BoxOpenEnded += OnChestOpenEnded;
        return receiver;
    }

    private void CleanupChestEventReceiver()
    {
        if (_chestEventReceiver != null)
        {
            _chestEventReceiver.BoxOpenEnded -= OnChestOpenEnded;
            _chestEventReceiver = null;
        }
    }

    private void OnChestOpenEnded()
    {
        _chestOpenEnded = true;
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

    private void OfferRewardToLocalPlayer()
    {
        PlayerAbilityRewardController rewardController = FindLocalRewardController();
        if (rewardController == null)
        {
            Debug.LogWarning("[RewardManager] Local PlayerAbilityRewardController was not found.");
            return;
        }

        rewardController.OfferBossReward(bossStage);

        InventoryPanelController inventoryPanel = FindObjectOfType<InventoryPanelController>(true);
        if (inventoryPanel != null)
        {
            inventoryPanel.SetRewardSelectOpen(true);
        }
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
}
