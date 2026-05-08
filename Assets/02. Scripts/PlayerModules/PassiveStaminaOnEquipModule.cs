using UnityEngine;

[CreateAssetMenu(menuName = "ServerSouls/Player Modules/Passive/Stamina On Equip")]
public class PassiveStaminaOnEquipModule : PlayerAbilityModule
{
    [SerializeField] private float restoreAmount = 200f;

    public override void OnEquipped(PlayerAbilityContext context)
    {
        context.Stats?.RestoreStamina(restoreAmount);
    }
}
