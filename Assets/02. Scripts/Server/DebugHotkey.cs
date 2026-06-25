using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Fusion;
using UnityEngine;

// 전투 씬의 DebugGame 오브젝트에서 사용하는 공통 단축키.
// F5: 보스 강제 처치 + 상자 등장 컷신 생략
// F6: 보상 상자 재소환
// F7: 로컬 플레이어를 상자 생성 위치로 이동
[DisallowMultipleComponent]
public class DebugHotkey : MonoBehaviour
{
    [Header("Reward Debug Keys")]
    [SerializeField] private KeyCode killBossAndSkipCutsceneKey = KeyCode.F5;
    [SerializeField] private KeyCode respawnChestKey = KeyCode.F6;
    [SerializeField] private KeyCode teleportToChestKey = KeyCode.F7;

    private void Update()
    {
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

        boss.RPC_DebugKillBoss();
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
