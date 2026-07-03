using System;
using UnityEngine;

// 플레이어가 가진 액티브 능력 1칸을 나타낸다.
// "어떤 모듈이 들어있는지", "어떤 키로 사용하는지", "언제 다시 쓸 수 있는지"를 함께 보관한다.
[Serializable]
public class PlayerAbilitySlot : ISerializationCallbackReceiver
{
    // 이 슬롯에 들어간 액티브 능력 모듈.
    [SerializeField] private PlayerAbilityModule module;

    // 이 슬롯을 발동시키는 키.
    // 기본값은 획득 순서대로 1, 2, 3...이지만 PlayerAbilityInventory에서 변경할 수 있다.
    [SerializeField] private KeyCode keyCode;

    // 다음 사용 가능 시간.
    // Fusion Runner가 있으면 Runner.SimulationTime 기준, 없으면 Time.time 기준으로 비교한다.
    [SerializeField] private float cooldownSeconds;
    [SerializeField, Min(1)] private int level = 1;
    [SerializeField, HideInInspector] private float nextReadyTime;

    public PlayerAbilityModule Module => module;
    public KeyCode KeyCode => keyCode;
    public int Level => Mathf.Clamp(level, 1, module != null ? module.MaxLevel : 1);
    public float CooldownSeconds => module != null ? module.GetCooldownSeconds(Level) : cooldownSeconds;
    public float NextReadyTime => nextReadyTime;

    // 액티브 능력을 획득할 때 PlayerAbilityInventory가 슬롯을 생성한다.
    public PlayerAbilitySlot(PlayerAbilityModule module, KeyCode keyCode, int level = 1)
    {
        this.module = module;
        this.keyCode = keyCode;
        this.level = Mathf.Clamp(level, 1, module != null ? module.MaxLevel : 1);
        RefreshInspectorCooldown();
    }

    // 키 변경 UI에서 호출할 수 있는 함수.
    public void SetKey(KeyCode newKey)
    {
        keyCode = newKey;
    }

    public void SetLevel(int newLevel)
    {
        level = Mathf.Clamp(newLevel, 1, module != null ? module.MaxLevel : 1);
        RefreshInspectorCooldown();
    }

    // 현재 시간이 쿨다운 종료 시간 이후인지 확인한다.
    public bool IsReady(float currentTime)
    {
        return currentTime >= nextReadyTime;
    }

    // 능력 사용 후 쿨다운을 시작한다.
    public void StartCooldown(float currentTime)
    {
        RefreshInspectorCooldown();
        nextReadyTime = currentTime + Mathf.Max(0f, CooldownSeconds);
    }

    public void SetCooldownEndTime(float readyTime)
    {
        nextReadyTime = Mathf.Max(0f, readyTime);
    }

    public void OnBeforeSerialize()
    {
        RefreshInspectorCooldown();
    }

    public void OnAfterDeserialize()
    {
    }

    private void RefreshInspectorCooldown()
    {
        if (module != null)
        {
            cooldownSeconds = module.GetCooldownSeconds(Level);
        }
    }
}
