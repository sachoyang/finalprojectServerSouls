using Fusion;
using UnityEngine;

public readonly struct PlayerAbilityContext
{
    public PlayerAbilityContext(GameObject owner, PlayerStats stats, NetworkRunner runner)
    {
        Owner = owner;
        Transform = owner != null ? owner.transform : null;
        Stats = stats;
        Runner = runner;
    }

    public GameObject Owner { get; }
    public Transform Transform { get; }
    public PlayerStats Stats { get; }
    public NetworkRunner Runner { get; }
}
