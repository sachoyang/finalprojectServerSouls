using Fusion;
using System.Collections.Generic;
using UnityEngine;

public class HealingAltar : NetworkBehaviour
{
    [Header("회복 설정")]
    [Tooltip("초당 회복되는 체력량")]
    public float healPerSecond = 500f;

    // 제단 영역 안에 들어온 플레이어들의 스탯 스크립트를 보관하는 장부
    private List<PlayerStats> _playersInZone = new List<PlayerStats>();

    // 1. 플레이어가 영역(Sphere Collider)에 들어왔을 때
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            PlayerStats stats = other.GetComponent<PlayerStats>();
            
            // 아직 장부에 없는 플레이어면 추가
            if (stats != null && !_playersInZone.Contains(stats))
            {
                _playersInZone.Add(stats);
            }
        }
    }

    // 2. 플레이어가 영역 밖으로 나갔을 때
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            PlayerStats stats = other.GetComponent<PlayerStats>();
            
            // 장부에서 삭제하여 힐 중단
            if (stats != null && _playersInZone.Contains(stats))
            {
                _playersInZone.Remove(stats);
            }
        }
    }

    // 3. 매 네트워크 틱마다 힐 적용
    public override void FixedUpdateNetwork()
    {
        // 서버(방장) 권한에서만 힐 로직을 계산하여 뿌려줍니다.
        if (!HasStateAuthority || _playersInZone.Count == 0) return;

        float healAmountThisTick = healPerSecond * Runner.DeltaTime;

        for (int i = _playersInZone.Count - 1; i >= 0; i--)
        {
            PlayerStats player = _playersInZone[i];

            if (player == null || !player.gameObject.activeInHierarchy || player.IsDead)
            {
                _playersInZone.RemoveAt(i);
                continue;
            }

            // 🔥 스킬과 무관한 기믹 전용 힐 함수 호출!
            player.ForceHeal(healAmountThisTick);
        }
    }
}