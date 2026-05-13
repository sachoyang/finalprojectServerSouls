using UnityEngine;

[CreateAssetMenu(menuName = "ServerSouls/Player Modules/Active/Heal")]
// 액티브 예시 모듈: 플레이어 체력을 회복한다.
// Project 창에서 우클릭 > Create > ServerSouls > Player Modules > Active > Heal 로 asset을 만들 수 있다.
public class HealAbilityModule : PlayerAbilityModule
{
    // 회복량. 실제 최대 체력 제한은 PlayerStats.Heal 내부에서 처리한다.
    [SerializeField] private float healAmount = 1500f;

    public override void Activate(PlayerAbilityContext context)
    {
        // context.Stats가 존재하면 Heal을 호출하고, 없으면 아무 일도 하지 않는다.
        context.Stats?.Heal(healAmount);
    }
}
