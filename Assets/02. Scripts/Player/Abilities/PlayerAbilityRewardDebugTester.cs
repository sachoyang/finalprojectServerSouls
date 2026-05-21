using System.Collections.Generic;
using Fusion;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerAbilityRewardController))]
public class PlayerAbilityRewardDebugTester : MonoBehaviour
{
    [SerializeField, Range(1, 8)] private int debugBossStage = 1;
    [SerializeField] private KeyCode offerRewardKey = KeyCode.F5;
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
        if (_networkObject != null && !_networkObject.HasInputAuthority)
        {
            return;
        }

        if (Input.GetKeyDown(offerRewardKey))
        {
            IReadOnlyList<PlayerAbilityModule> options = _rewardController.OfferBossReward(debugBossStage);
            Debug.Log($"[Reward Debug] Offered boss stage {debugBossStage}: {FormatOptions(options)}");
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
        IReadOnlyList<PlayerAbilityModule> options = _rewardController.PendingOptions;
        string optionName = optionIndex >= 0 && optionIndex < options.Count && options[optionIndex] != null
            ? options[optionIndex].DisplayName
            : "None";

        bool selected = _rewardController.SelectPendingOption(optionIndex);
        Debug.Log($"[Reward Debug] Select {optionIndex + 1}: {optionName} / {(selected ? "Success" : "Failed")}");
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
