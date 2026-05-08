using UnityEngine;

[CreateAssetMenu(menuName = "ServerSouls/Player Modules/Active/Heal")]
public class HealAbilityModule : PlayerAbilityModule
{
    [SerializeField] private float healAmount = 1500f;

    public override void Activate(PlayerAbilityContext context)
    {
        context.Stats?.Heal(healAmount);
    }
}
