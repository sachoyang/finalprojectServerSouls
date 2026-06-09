using System.Collections.Generic;
using Fusion;
using UnityEngine;

public struct PlayerStatusData : INetworkStruct
{
    public int StatusId;
    public float EndTime;
    public float Power;
}

public class PlayerStatusController : NetworkBehaviour
{
    [Header("Status Database")]
    [SerializeField] private List<StatusEffectData> statusDatabase = new List<StatusEffectData>();

    [Networked, Capacity(8)] public NetworkArray<PlayerStatusData> ActiveStatuses { get; }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority)
        {
            return;
        }

        ProcessStatuses();
    }

    public void ApplyStatus(int statusId)
    {
        if (statusId == 0)
        {
            return;
        }

        if (!HasStateAuthority)
        {
            RPC_ApplyStatus(statusId);
            return;
        }

        ApplyStatusInternal(statusId);
    }

    public void RemoveStatus(int statusId)
    {
        if (statusId == 0)
        {
            return;
        }

        if (!HasStateAuthority)
        {
            RPC_RemoveStatus(statusId);
            return;
        }

        RemoveStatusInternal(statusId);
    }

    public float GetIncomingDamageMultiplier()
    {
        return GetMultiplier(StatusEffectTarget.IncomingDamage);
    }

    public float GetOutgoingDamageMultiplier()
    {
        return GetMultiplier(StatusEffectTarget.OutgoingDamage);
    }

    public float GetMoveSpeedMultiplier()
    {
        return GetMultiplier(StatusEffectTarget.MoveSpeed);
    }

    public List<ActiveStatusUIInfo> GetActiveStatusesForUI()
    {
        List<ActiveStatusUIInfo> activeList = new List<ActiveStatusUIInfo>();
        float simulationTime = Runner != null ? Runner.SimulationTime : Time.time;

        for (int i = 0; i < ActiveStatuses.Length; i++)
        {
            PlayerStatusData status = ActiveStatuses[i];
            if (status.StatusId == 0)
            {
                continue;
            }

            StatusEffectData data = FindStatusData(status.StatusId);
            if (data == null)
            {
                continue;
            }

            activeList.Add(new ActiveStatusUIInfo
            {
                Data = data,
                RemainingTime = Mathf.Max(0f, status.EndTime - simulationTime),
                Power = status.Power
            });
        }

        return activeList;
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_ApplyStatus(int statusId)
    {
        ApplyStatusInternal(statusId);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RemoveStatus(int statusId)
    {
        RemoveStatusInternal(statusId);
    }

    private void ApplyStatusInternal(int statusId)
    {
        if (!HasStateAuthority)
        {
            return;
        }

        StatusEffectData data = FindStatusData(statusId);
        if (data == null)
        {
            Debug.LogWarning($"[PlayerStatus] ID {statusId} status data not found.");
            return;
        }

        float duration = data.isInfinite ? 999999f : Mathf.Max(0f, data.defaultDuration);
        float endTime = (Runner != null ? Runner.SimulationTime : Time.time) + duration;

        for (int i = 0; i < ActiveStatuses.Length; i++)
        {
            PlayerStatusData existing = ActiveStatuses[i];
            if (existing.StatusId != statusId)
            {
                continue;
            }

            existing.EndTime = endTime;
            existing.Power = data.Power;
            ActiveStatuses.Set(i, existing);
            return;
        }

        for (int i = 0; i < ActiveStatuses.Length; i++)
        {
            if (ActiveStatuses[i].StatusId != 0)
            {
                continue;
            }

            ActiveStatuses.Set(i, new PlayerStatusData
            {
                StatusId = statusId,
                EndTime = endTime,
                Power = data.Power
            });
            return;
        }
    }

    private void RemoveStatusInternal(int statusId)
    {
        if (!HasStateAuthority)
        {
            return;
        }

        for (int i = 0; i < ActiveStatuses.Length; i++)
        {
            if (ActiveStatuses[i].StatusId == statusId)
            {
                ActiveStatuses.Set(i, new PlayerStatusData());
                return;
            }
        }
    }

    private void ProcessStatuses()
    {
        float simulationTime = Runner != null ? Runner.SimulationTime : Time.time;

        for (int i = 0; i < ActiveStatuses.Length; i++)
        {
            PlayerStatusData status = ActiveStatuses[i];
            if (status.StatusId == 0)
            {
                continue;
            }

            StatusEffectData data = FindStatusData(status.StatusId);
            if (data != null && data.isInfinite)
            {
                continue;
            }

            if (simulationTime >= status.EndTime)
            {
                ActiveStatuses.Set(i, new PlayerStatusData());
            }
        }
    }

    private float GetMultiplier(StatusEffectTarget target)
    {
        float multiplier = 1f;

        for (int i = 0; i < ActiveStatuses.Length; i++)
        {
            PlayerStatusData status = ActiveStatuses[i];
            if (status.StatusId == 0)
            {
                continue;
            }

            StatusEffectData data = FindStatusData(status.StatusId);
            if (data != null && data.effectTarget == target)
            {
                multiplier *= status.Power;
            }
        }

        return multiplier;
    }

    private StatusEffectData FindStatusData(int statusId)
    {
        if (statusDatabase == null)
        {
            return null;
        }

        for (int i = 0; i < statusDatabase.Count; i++)
        {
            StatusEffectData data = statusDatabase[i];
            if (data != null && data.statusId == statusId)
            {
                return data;
            }
        }

        return null;
    }
}
