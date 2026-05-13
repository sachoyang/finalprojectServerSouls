using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerAbilityInventory))]
// 보스를 잡았을 때 "랜덤 3개 능력 중 1개 선택" 흐름을 담당하는 컴포넌트.
// 실제 UI 버튼은 이 컴포넌트의 OfferBossReward와 SelectPendingOption을 호출하면 된다.
public class PlayerAbilityRewardController : MonoBehaviour
{
    // 마지막으로 보상을 생성한 보스 단계.
    [SerializeField, Range(1, 8)] private int lastClearedBossStage;

    // 현재 화면에 띄워야 할 보상 후보 3개.
    // 선택이 끝나면 비워진다.
    [SerializeField] private List<PlayerAbilityModule> pendingOptions = new List<PlayerAbilityModule>();

    private PlayerAbilityInventory _inventory;

    public int LastClearedBossStage => lastClearedBossStage;
    public IReadOnlyList<PlayerAbilityModule> PendingOptions => pendingOptions;

    // 보상 후보가 생성되었을 때 UI가 선택창을 열 수 있게 알려주는 이벤트.
    public event Action<int, IReadOnlyList<PlayerAbilityModule>> BossRewardOffered;

    // 플레이어가 보상 1개를 선택했을 때 UI 닫기, 효과음 재생 등에 사용할 수 있는 이벤트.
    public event Action<PlayerAbilityModule> BossRewardSelected;

    private void Awake()
    {
        _inventory = GetComponent<PlayerAbilityInventory>();
    }

    public IReadOnlyList<PlayerAbilityModule> OfferBossReward(int bossStage)
    {
        if (_inventory == null)
        {
            return pendingOptions;
        }

        // 보스 단계는 기획상 1~8이므로 범위를 안전하게 고정한다.
        lastClearedBossStage = Mathf.Clamp(bossStage, 1, 8);
        pendingOptions = _inventory.GenerateRewardOptions(lastClearedBossStage, 3);
        BossRewardOffered?.Invoke(lastClearedBossStage, pendingOptions);
        return pendingOptions;
    }

    // 보상 선택창의 버튼 index로 호출한다.
    // 예: 첫 번째 카드 버튼은 SelectPendingOption(0), 두 번째는 SelectPendingOption(1).
    public bool SelectPendingOption(int optionIndex)
    {
        if (_inventory == null || optionIndex < 0 || optionIndex >= pendingOptions.Count)
        {
            return false;
        }

        PlayerAbilityModule selected = pendingOptions[optionIndex];
        if (!_inventory.SelectRewardOption(selected))
        {
            return false;
        }

        // 하나를 선택했으면 같은 보상 목록에서 중복 선택할 수 없도록 후보를 비운다.
        pendingOptions.Clear();
        BossRewardSelected?.Invoke(selected);
        return true;
    }
}
