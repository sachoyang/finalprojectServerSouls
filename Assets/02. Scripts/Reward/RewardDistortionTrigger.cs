using System;
using Fusion;
using UnityEngine;

[DisallowMultipleComponent]
public class RewardDistortionTrigger : MonoBehaviour
{
    public event Action Triggered;

    private bool _triggered;

    private void OnEnable()
    {
        _triggered = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsLocalPlayer(other))
        {
            return;
        }

        TriggerReward();
    }

    public void TriggerReward()
    {
        if (_triggered)
        {
            return;
        }

        _triggered = true;
        Debug.Log("[RewardDistortionTrigger] Local player entered reward trigger.");
        Triggered?.Invoke();
    }

    private static bool IsLocalPlayer(Collider other)
    {
        if (other == null)
        {
            return false;
        }

        NetworkObject networkObject = other.GetComponentInParent<NetworkObject>();
        if (networkObject != null)
        {
            return networkObject.HasInputAuthority;
        }

        return other.CompareTag("Player") || other.GetComponentInParent<NetworkPlayerController>() != null;
    }
}
