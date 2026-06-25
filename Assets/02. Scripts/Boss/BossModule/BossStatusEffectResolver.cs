using System.Collections.Generic;
using Fusion;

// 상태이상 도감(SO) 조회 및 데미지 배율 계산 책임 분리 (SRP)
// 네트워크 상태(ActiveStatuses)는 코어가 보유하고, 순수 계산만 위임받음
public class BossStatusEffectResolver
{
    private readonly List<StatusEffectData> _database;

    public BossStatusEffectResolver(List<StatusEffectData> database)
    {
        _database = database;
    }

    // 도감에서 ID로 SO 1건 조회
    public StatusEffectData GetData(int statusId)
    {
        if (_database == null) return null;
        return _database.Find(x => x.statusId == statusId);
    }

    // 받는 피해 배율 (기믹 감산 + IncomingDamage 계열 상태이상 누적)
    public float GetIncomingMultiplier(NetworkArray<BossStatusData> active, bool gimmickActive, float gimmickReduction)
    {
        float multiplier = 1.0f;
        if (gimmickActive) multiplier *= gimmickReduction;

        for (int i = 0; i < active.Length; i++)
        {
            if (active[i].StatusId == 0) continue;
            StatusEffectData data = GetData(active[i].StatusId);
            if (data != null && data.effectTarget == StatusEffectTarget.IncomingDamage)
                multiplier *= active[i].Power;
        }
        return multiplier;
    }

    // 주는 피해 배율 (스테이지 난이도 base + OutgoingDamage 계열 누적)
    public float GetOutgoingMultiplier(NetworkArray<BossStatusData> active, float baseMultiplier)
    {
        float multiplier = baseMultiplier;
        for (int i = 0; i < active.Length; i++)
        {
            if (active[i].StatusId == 0) continue;
            StatusEffectData data = GetData(active[i].StatusId);
            if (data != null && data.effectTarget == StatusEffectTarget.OutgoingDamage)
                multiplier *= active[i].Power;
        }
        return multiplier;
    }

    // 이동속도 배율 (MoveSpeed 계열 상태이상 누적). 예: Boss_Flying(power 2) → 이동속도 2배
    public float GetMoveSpeedMultiplier(NetworkArray<BossStatusData> active)
    {
        float multiplier = 1.0f;
        for (int i = 0; i < active.Length; i++)
        {
            if (active[i].StatusId == 0) continue;
            StatusEffectData data = GetData(active[i].StatusId);
            if (data != null && data.effectTarget == StatusEffectTarget.MoveSpeed)
                multiplier *= active[i].Power;
        }
        return multiplier;
    }

    // UI 표시용 활성 상태이상 목록 빌드
    public List<ActiveStatusUIInfo> BuildUIList(NetworkArray<BossStatusData> active, float simulationTime)
    {
        List<ActiveStatusUIInfo> activeList = new List<ActiveStatusUIInfo>();
        for (int i = 0; i < active.Length; i++)
        {
            if (active[i].StatusId == 0) continue;
            StatusEffectData so = GetData(active[i].StatusId);
            if (so == null) continue;

            activeList.Add(new ActiveStatusUIInfo
            {
                Data = so,
                RemainingTime = UnityEngine.Mathf.Max(0, active[i].EndTime - simulationTime),
                Power = active[i].Power
            });
        }
        return activeList;
    }
}
