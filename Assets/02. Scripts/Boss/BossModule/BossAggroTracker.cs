using System.Collections.Generic;
using Fusion;
using UnityEngine;

// 딜미터기(누적 데미지 장부) 및 최다 딜러 산출 책임 분리
// 비네트워크 로컬 데이터이므로 순수 클래스로 분리 가능
public class BossAggroTracker
{
    private readonly Dictionary<NetworkObject, float> _damageTracker = new Dictionary<NetworkObject, float>();

    // 공격자별 누적 데미지 기록
    public void Record(NetworkObject attacker, float damage)
    {
        if (attacker == null) return;
        if (_damageTracker.ContainsKey(attacker)) _damageTracker[attacker] += damage;
        else _damageTracker[attacker] = damage;
    }

    // 정산 구간 동안 가장 딜을 많이 넣은 살아있는 플레이어 반환 (없으면 null)
    public NetworkObject GetTopAttacker()
    {
        NetworkObject topDPSPlayer = null;
        float maxDamage = 0f;
        foreach (var kvp in _damageTracker)
        {
            if (kvp.Key != null && kvp.Key.gameObject.CompareTag("Player") && kvp.Value > maxDamage)
            {
                maxDamage = kvp.Value;
                topDPSPlayer = kvp.Key;
            }
        }
        return topDPSPlayer;
    }

    public void Clear() => _damageTracker.Clear();
}
