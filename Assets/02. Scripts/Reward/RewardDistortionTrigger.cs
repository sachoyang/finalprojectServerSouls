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
        // 태그 fallback을 제거하고 Fusion PlayerRef 기준으로 로컬 플레이어만 통과시킨다.
        return PlayerRegistry.IsLocalPlayer(networkObject);
    }
}
