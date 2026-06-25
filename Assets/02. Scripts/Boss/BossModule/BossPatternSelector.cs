using System.Collections.Generic;
using UnityEngine;

// 패턴 룰렛(사거리 필터 + 가중치 추첨) 책임 분리 (Strategy)
public class BossPatternSelector
{
    private readonly List<int> _validIndices = new List<int>(); // 매 호출 재사용으로 GC 절감

    // 현재 사거리에 발동 가능한 패턴을 가중치 기반으로 추첨, 없으면 -1
    // excludeIndex: 후보에서 강제로 빼고 싶은 인덱스(예: 비행 룰렛에서 TakeOff 제외). 기본 -1(제외 안 함).
    public int SelectByRange(List<BossPatternModule> patterns, float currentDistance, int excludeIndex = -1)
    {
        _validIndices.Clear();
        int totalWeight = 0;

        for (int i = 0; i < patterns.Count; i++)
        {
            if (i == excludeIndex) continue; // 후보에서 제외

            var pattern = patterns[i];
            if (currentDistance >= pattern.minRange && currentDistance <= pattern.maxRange)
            {
                _validIndices.Add(i);
                totalWeight += pattern.weight;
            }
        }

        if (_validIndices.Count == 0) return -1;

        int randomVal = Random.Range(0, totalWeight);
        int accumulatedWeight = 0;
        foreach (int idx in _validIndices)
        {
            accumulatedWeight += patterns[idx].weight;
            if (randomVal <= accumulatedWeight)
                return idx;
        }
        return _validIndices[0];
    }
}
