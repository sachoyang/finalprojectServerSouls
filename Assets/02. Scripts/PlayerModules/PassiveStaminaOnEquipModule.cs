using UnityEngine;

[CreateAssetMenu(menuName = "ServerSouls/Player Modules/Passive/Stamina On Equip")]
// 패시브 예시 모듈: 능력을 선택해서 장착하는 순간 스태미나를 회복한다.
// 지속적으로 최대 스태미나를 늘리는 패시브를 만들려면 PlayerStats에 별도 보정 API를 추가하는 편이 좋다.
public class PassiveStaminaOnEquipModule : PlayerAbilityModule
{
    // 장착 순간 회복할 스태미나 양.
    [SerializeField] private float restoreAmount = 200f;

    public override void OnEquipped(PlayerAbilityContext context)
    {
        // 패시브는 Activate가 아니라 OnEquipped에서 효과를 적용하는 경우가 많다.
        context.Stats?.RestoreStamina(restoreAmount);
    }
}
