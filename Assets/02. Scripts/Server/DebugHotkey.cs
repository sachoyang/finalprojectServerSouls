using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;

// 전투 씬의 DebugGame 오브젝트에서 사용하는 공통 단축키.
// F5: 보스 강제 처치 + 상자 등장 컷신 생략
// F6: 보상 상자 재소환
// F7: 로컬 플레이어를 상자 생성 위치로 이동
// F8: Path 씬으로 강제 이동
// F9: 크래시 리포트 테스트용 예외 발생 (Sentry + CrashReporter 동작 확인)
[DisallowMultipleComponent]
public class DebugHotkey : MonoBehaviour
{
    [Header("Reward Debug Keys")]
    [SerializeField] private KeyCode killBossAndSkipCutsceneKey = KeyCode.F5;
    [SerializeField] private KeyCode respawnChestKey = KeyCode.F6;
    [SerializeField] private KeyCode teleportToChestKey = KeyCode.F7;
    [SerializeField] private KeyCode loadPathSceneKey = KeyCode.F8;
    [SerializeField] private string pathSceneName = "scPathNew";

    [Header("Crash Report Test")]
    [Tooltip("누르면 일부러 예외를 던진다. Sentry 대시보드와 crash_reports 테이블에 들어오는지 확인용.")]
    [SerializeField] private bool enableCrashTestKey = true;
    [SerializeField] private KeyCode crashTestKey = KeyCode.F9;

    // 디버그 키 사용 가능 여부.
    //  - 에디터/개발(Development) 빌드: 항상 허용
    //  - 릴리즈 빌드: admin 계정으로 로그인한 경우에만 허용
    //    (F5 보스 킬은 여기서 걸러도, 최종 검증은 호스트가 NetworkBossCore.RPC_DebugKillBoss에서
    //     login_id + 세션 토큰을 백엔드로 재확인하므로 조작된 클라이언트도 뚫을 수 없다)
    private static bool DebugKeysAllowed
    {
        get
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            return true;
#else
            return BackendManager.HasInstance && BackendManager.Instance.IsAdminAccount;
#endif
        }
    }

    private void Update()
    {
        if (!DebugKeysAllowed)
        {
            return;
        }

        if (Input.GetKeyDown(killBossAndSkipCutsceneKey))
        {
            StartCoroutine(KillBossAndSkipChestCutscene());
        }

        if (Input.GetKeyDown(respawnChestKey))
        {
            RespawnRewardChest();
        }

        if (Input.GetKeyDown(teleportToChestKey))
        {
            TeleportLocalPlayerToChest();
        }

        if (Input.GetKeyDown(loadPathSceneKey))
        {
            LoadPathScene();
        }

        if (enableCrashTestKey && Input.GetKeyDown(crashTestKey))
        {
            // Update에서 던진 예외는 Unity가 잡아서 LogType.Exception으로 흘려보낸다.
            // 게임은 죽지 않고, Sentry와 CrashReporter가 각각 이 프레임에 리포트를 만든다.
            throw new System.Exception("[DebugHotkey] F9 크래시 리포트 테스트용 예외입니다.");
        }
    }

    private static IEnumerator KillBossAndSkipChestCutscene()
    {
        RewardManager rewardManager = ResolveRewardManager();
        NetworkBossCore boss = FindObjectOfType<NetworkBossCore>();
        if (rewardManager == null || boss == null)
        {
            Debug.LogWarning("[DebugHotkey] RewardManager 또는 NetworkBossCore를 찾지 못했습니다.");
            yield break;
        }

        if (boss.Object == null || !boss.Object.IsValid)
        {
            Debug.LogWarning("[DebugHotkey] 보스 NetworkObject가 아직 Spawn되지 않았습니다.");
            yield break;
        }

        // 릴리즈 빌드에서는 호스트가 이 login_id + 세션 토큰을 백엔드로 검증한 뒤에만 처치한다.
        string loginId = BackendManager.HasInstance ? BackendManager.Instance.CurrentLoginID : null;
        string sessionToken = BackendManager.HasInstance ? BackendManager.Instance.CurrentSessionToken : null;
        boss.RPC_DebugKillBoss(loginId ?? string.Empty, sessionToken ?? string.Empty);
        Debug.Log("[DebugHotkey] F5: 보스 강제 처치를 요청했습니다.");

        // 클라이언트의 RPC 왕복과 RewardManager의 사망 감지를 기다린 뒤,
        // 보상 코루틴을 멈추고 디버그 스크립트가 상자를 바로 생성한다.
        float waitTime = 0f;
        while (!GetPrivateField<bool>(rewardManager, "_rewardStarted") && waitTime < 3f)
        {
            waitTime += Time.unscaledDeltaTime;
            yield return null;
        }

        SpawnDebugRewardChest(rewardManager);
        Debug.Log("[DebugHotkey] F5: 상자 등장 컷신을 생략하고 보상 상자를 생성했습니다.");
    }

    private static void RespawnRewardChest()
    {
        RewardManager rewardManager = ResolveRewardManager();
        if (rewardManager == null)
        {
            Debug.LogWarning("[DebugHotkey] RewardManager를 찾지 못해 상자를 재소환할 수 없습니다.");
            return;
        }

        SpawnDebugRewardChest(rewardManager);
        Debug.Log("[DebugHotkey] F6: 보상 상자를 재소환했습니다.");
    }

    private static void TeleportLocalPlayerToChest()
    {
        RewardManager rewardManager = ResolveRewardManager();
        NetworkPlayerController localPlayer = PlayerRegistry.LocalPlayer;
        if (rewardManager == null || localPlayer == null)
        {
            Debug.LogWarning("[DebugHotkey] RewardManager 또는 로컬 플레이어를 찾지 못했습니다.");
            return;
        }

        Transform spawnPoint = GetPrivateField<Transform>(rewardManager, "goldChestSpawnPoint");
        if (spawnPoint == null)
        {
            spawnPoint = rewardManager.transform;
        }

        NetworkCharacterController characterController =
            localPlayer.GetComponent<NetworkCharacterController>();
        if (characterController != null)
        {
            characterController.Teleport(spawnPoint.position);
        }
        else
        {
            localPlayer.transform.position = spawnPoint.position;
        }

        Debug.Log("[DebugHotkey] F7: 로컬 플레이어를 GoldChestSpawnPoint로 이동했습니다.");
    }

    private static void SpawnDebugRewardChest(RewardManager rewardManager)
    {
        // RewardManager 원본 코드는 수정하지 않고, DebugGame이 존재할 때만
        // 진행 중인 보상 연출을 중단하고 디버그용 반복 보상 상태로 바꾼다.
        rewardManager.StopAllCoroutines();
        InvokePrivate(rewardManager, "CleanupDistortionTrigger");
        InvokePrivate(rewardManager, "SetDistortionActive", false);

        GameObject previousChest = GetPrivateField<GameObject>(rewardManager, "_spawnedChest");
        if (previousChest != null)
        {
            Destroy(previousChest);
        }

        GetPrivateField<List<PlayerAbilityModule>>(rewardManager, "_pendingOptions")?.Clear();
        SetPrivateField(rewardManager, "_rewardStarted", true);
        SetPrivateField(rewardManager, "_rewardOptionsOpened", false);
        SetPrivateField(rewardManager, "_sceneLoadRequested", false);

        GameObject chestPrefab = GetPrivateField<GameObject>(rewardManager, "goldChestPrefab");
        Transform spawnPoint = GetPrivateField<Transform>(rewardManager, "goldChestSpawnPoint");
        if (chestPrefab == null)
        {
            Debug.LogWarning("[DebugHotkey] RewardManager에 Gold Chest Prefab이 지정되지 않았습니다.");
            return;
        }

        Transform target = spawnPoint != null ? spawnPoint : rewardManager.transform;
        GameObject chest = Instantiate(chestPrefab, target.position, target.rotation);
        SetPrivateField(rewardManager, "_spawnedChest", chest);

        Animator chestAnimator = chest.GetComponentInChildren<Animator>();
        if (chestAnimator != null)
        {
            string stateName = GetPrivateField<string>(rewardManager, "chestOpenStateName");
            chestAnimator.enabled = true;
            if (!string.IsNullOrWhiteSpace(stateName))
            {
                chestAnimator.Play(stateName, 0, 0f);
            }
        }

        // 비활성화 후 다시 켜면 RewardDistortionTrigger의 1회 실행 상태도 초기화된다.
        InvokePrivate(rewardManager, "PrepareDistortionTrigger");
        InvokePrivate(rewardManager, "SetDistortionActive", true);
    }

    private static RewardManager ResolveRewardManager()
    {
        return RewardManager.Active != null
            ? RewardManager.Active
            : FindObjectOfType<RewardManager>();
    }

    private void LoadPathScene()
    {
        if (string.IsNullOrWhiteSpace(pathSceneName) ||
            !Application.CanStreamedLevelBeLoaded(pathSceneName))
        {
            Debug.LogError($"[DebugHotkey] F8 이동 씬 '{pathSceneName}'이 Build Settings에 없습니다.");
            return;
        }

        // 일반 게임은 NetworkManager가 Runner를 소유하지만, DebugQuickEntry는
        // 별도의 DebugNetworkRunner를 생성한다. 둘 다 찾지 않으면 일반 씬 로드가
        // 실행되어 Path 씬의 NetworkObject와 플레이어 스포너가 등록되지 않는다.
        NetworkRunner runner =
            NetworkManager.Instance != null ? NetworkManager.Instance.Runner : null;
        if (runner == null || !runner.IsRunning)
        {
            NetworkRunner[] runners = FindObjectsOfType<NetworkRunner>(true);
            foreach (NetworkRunner candidate in runners)
            {
                if (candidate != null && candidate.IsRunning)
                {
                    runner = candidate;
                    break;
                }
            }
        }

        if (runner == null)
        {
            SceneManager.LoadScene(pathSceneName);
            Debug.Log($"[DebugHotkey] F8: {pathSceneName} 씬으로 강제 이동합니다.");
            return;
        }

        if (!runner.IsServer)
        {
            Debug.LogWarning("[DebugHotkey] 네트워크 씬 전환은 서버/호스트에서 F8을 눌러야 합니다.");
            return;
        }

        PlayerSessionStore.SaveActivePlayerStats(runner);
        runner.LoadScene(pathSceneName, LoadSceneMode.Single);
        Debug.Log($"[DebugHotkey] F8: 서버에서 {pathSceneName} 씬으로 강제 이동합니다.");
    }

    private static T GetPrivateField<T>(object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        return field != null && field.GetValue(target) is T value ? value : default;
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        field?.SetValue(target, value);
    }

    private static void InvokePrivate(object target, string methodName, params object[] arguments)
    {
        MethodInfo method = target.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        method?.Invoke(target, arguments);
    }
}
