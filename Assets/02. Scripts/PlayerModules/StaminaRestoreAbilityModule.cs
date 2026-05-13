using UnityEngine;

[CreateAssetMenu(menuName = "ServerSouls/Player Modules/Active/Stamina Restore")]
// 액티브 예시 모듈: 플레이어 스태미나를 즉시 회복한다.
public class StaminaRestoreAbilityModule : PlayerAbilityModule
{
    // 회복할 스태미나 양. 최대 스태미나 제한은 PlayerStats.RestoreStamina 내부에서 처리한다.
    [SerializeField] private float restoreAmount = 300f;

    public override void Activate(PlayerAbilityContext context)
    {
        // context.Stats가 존재하면 RestoreStamina를 호출하고, 없으면 아무 일도 하지 않는다.
        context.Stats?.RestoreStamina(restoreAmount);
    }
}
