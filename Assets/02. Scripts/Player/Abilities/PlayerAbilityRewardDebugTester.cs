using System.Collections.Generic;
using Fusion;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerAbilityRewardController))]
public class PlayerAbilityRewardDebugTester : MonoBehaviour
{
    // 보스 처치 보상 시스템을 실제 보스 처치 없이 빠르게 확인하기 위한 디버그 컴포넌트.
    // '/'로 여는 플레이어 디버그 패널과 함께 쓰면 현재 보상 후보 이름을 화면에서 확인할 수 있다.
    // F5: 지정한 보스 단계 기준으로 보상 후보를 생성한다.
    [SerializeField] private KeyCode offerRewardKey = KeyCode.F5;

    // F6~F8: 현재 생성된 보상 후보 1~3번을 선택한다.
    [SerializeField] private KeyCode selectFirstOptionKey = KeyCode.F6;
    [SerializeField] private KeyCode selectSecondOptionKey = KeyCode.F7;
    [SerializeField] private KeyCode selectThirdOptionKey = KeyCode.F8;

    private PlayerAbilityRewardController _rewardController;
    private NetworkObject _networkObject;

    private void Awake()
    {
        _rewardController = GetComponent<PlayerAbilityRewardController>();
        _networkObject = GetComponent<NetworkObject>();
    }

    private void Update()
    {
        // 네트워크 플레이어가 여러 명이어도 자신의 InputAuthority를 가진 클라이언트에서만 디버그 입력을 처리한다.
        if (_networkObject != null && !_networkObject.HasInputAuthority)
        {
            return;
        }

        if (Input.GetKeyDown(offerRewardKey))
        {
            KillCurrentBoss();
        }

        if (Input.GetKeyDown(selectFirstOptionKey))
        {
            SelectOption(0);
        }
        else if (Input.GetKeyDown(selectSecondOptionKey))
        {
            SelectOption(1);
        }
        else if (Input.GetKeyDown(selectThirdOptionKey))
        {
            SelectOption(2);
        }
    }

    private void SelectOption(int optionIndex)
    {
        // 선택 실패도 로그로 남겨 pool 조건, 중복 방지, 후보 개수 문제를 빠르게 확인할 수 있게 한다.
        IReadOnlyList<PlayerAbilityModule> options = _rewardController.PendingOptions;
        string optionName = optionIndex >= 0 && optionIndex < options.Count && options[optionIndex] != null
            ? options[optionIndex].DisplayName
            : "None";

        bool selected = _rewardController.SelectPendingOption(optionIndex);
        Debug.Log($"[Reward Debug] Select {optionIndex + 1}: {optionName} / {(selected ? "Success" : "Failed")}");
    }

    private static void KillCurrentBoss()
    {
        NetworkBossCore boss = FindObjectOfType<NetworkBossCore>();
        if (boss == null)
        {
            Debug.LogWarning("[Reward Debug] NetworkBossCore was not found.");
            return;
        }

        boss.RPC_DebugKillBoss();
        Debug.Log("[Reward Debug] Requested boss debug kill.");
    }

    private static string FormatOptions(IReadOnlyList<PlayerAbilityModule> options)
    {
        if (options == null || options.Count == 0)
        {
            return "None";
        }

        List<string> names = new List<string>();
        for (int i = 0; i < options.Count; i++)
        {
            names.Add(options[i] != null ? $"{i + 1}. {options[i].DisplayName}" : $"{i + 1}. Empty");
        }

        return string.Join(", ", names);
    }
}
