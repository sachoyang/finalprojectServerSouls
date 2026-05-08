using UnityEngine;

[CreateAssetMenu(menuName = "ServerSouls/Player Modules/Active/Stamina Restore")]
public class StaminaRestoreAbilityModule : PlayerAbilityModule
{
    [SerializeField] private float restoreAmount = 300f;

    public override void Activate(PlayerAbilityContext context)
    {
        context.Stats?.RestoreStamina(restoreAmount);
    }
}
