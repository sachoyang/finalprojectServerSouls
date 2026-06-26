using Fusion;
using System.Collections.Generic;
using UnityEngine;

/// 네트워크 상의 모든 플레이어(NetworkPlayerController)를 중앙 관리하는 정적 레지스트리 클래스입니다.
public static class PlayerRegistry
{
    // 현재 게임 세션에 존재하는 모든 플레이어 컨트롤러를 저장하는 내부 리스트
    private static readonly List<NetworkPlayerController> Players = new List<NetworkPlayerController>();


    /// 외부에서 플레이어 목록을 조회할 수 있는 읽기 전용 프로퍼티입니다.
    /// (외부에서 리스트를 직접 수정(Add/Remove)하는 것을 방지합니다.)
    public static IReadOnlyList<NetworkPlayerController> All => Players;

    /// 현재 이 클라이언트에서 조작하는 '로컬 플레이어'의 레퍼런스입니다.
    public static NetworkPlayerController LocalPlayer { get; private set; }

    /// NetworkObject가 현재 클라이언트의 로컬 플레이어인지 PlayerRef 기준으로 확인합니다.
    public static bool IsLocalPlayer(NetworkObject networkObject)
    {
        if (networkObject == null || networkObject.Runner == null)
        {
            return false;
        }

        return networkObject.InputAuthority == networkObject.Runner.LocalPlayer;
    }

    /// 플레이어 컨트롤러가 현재 클라이언트의 로컬 플레이어인지 확인합니다.
    public static bool IsLocalPlayer(NetworkPlayerController player)
    {
        return player != null && IsLocalPlayer(player.Object);
    }

    /// NetworkObject가 살아있는 플레이어인지 확인합니다.
    public static bool IsAlivePlayer(NetworkObject networkObject)
    {
        return TryGetStats(networkObject, out PlayerStats stats) && !stats.IsDead;
    }

    /// 플레이어 NetworkObject에서 PlayerStats를 안전하게 가져옵니다.
    public static bool TryGetStats(NetworkObject networkObject, out PlayerStats stats)
    {
        stats = networkObject != null ? networkObject.GetComponent<PlayerStats>() : null;
        return stats != null;
    }

    /// HUD가 PlayerStats 네트워크 값을 읽어도 안전한 상태인지 확인합니다.
    public static bool TryGetHUDData(NetworkPlayerController player, out PlayerHUDData hudData)
    {
        hudData = default;
        if (!TryGetStats(player != null ? player.Object : null, out PlayerStats stats) ||
            !stats.IsSpawnedReady ||
            stats.Object == null ||
            !stats.Object.IsValid)
        {
            return false;
        }

        hudData = stats.GetHUDData();
        return true;
    }

    /// 로컬 플레이어의 HUD 관련 런타임 컴포넌트를 한 번에 안전 조회합니다.
    public static bool TryGetLocalHUDReferences(
        out NetworkPlayerController player,
        out PlayerAbilityInventory abilityInventory,
        out PlayerStatusController statusController,
        out PlayerStats stats)
    {
        player = LocalPlayer;
        abilityInventory = null;
        statusController = null;
        stats = null;

        if (player == null)
        {
            return false;
        }

        abilityInventory = player.GetComponent<PlayerAbilityInventory>();
        statusController = player.GetComponent<PlayerStatusController>();
        TryGetStats(player.Object, out stats);
        return abilityInventory != null || statusController != null || stats != null;
    }

    /// 파티 HUD가 표시할 플레이어 데이터를 안전하게 생성합니다.
    public static bool TryGetPartyMemberUIData(NetworkPlayerController player, out PartyMemberUIData uiData)
    {
        uiData = default;
        if (player == null || player.Object == null || IsLocalPlayer(player))
        {
            return false;
        }

        if (!TryGetHUDData(player, out PlayerHUDData hudData))
        {
            return false;
        }

        int playerKey = player.Object.InputAuthority.RawEncoded;
        uiData = new PartyMemberUIData(
            playerKey,
            $"Player {playerKey}",
            hudData.CurrentHealth,
            hudData.MaxHealth,
            hudData.CurrentStamina,
            hudData.MaxStamina,
            !hudData.IsDead,
            hudData.IsDead,
            false);
        return true;
    }

    public static bool TryGetAbilityInventory(NetworkPlayerController player, out PlayerAbilityInventory inventory)
    {
        inventory = player != null ? player.GetComponent<PlayerAbilityInventory>() : null;
        return inventory != null;
    }

    public static bool TryGetNetworkPlayerData(NetworkPlayerController player, out NetworkPlayerData playerData)
    {
        playerData = player != null ? player.GetComponent<NetworkPlayerData>() : null;
        return playerData != null;
    }

    /// 등록된 플레이어 중 살아있는 플레이어만 대상으로 가장 가까운 플레이어를 찾습니다.
    public static NetworkPlayerController GetClosestAlivePlayer(Vector3 position)
    {
        NetworkPlayerController closest = null;
        float closestSqrDistance = float.MaxValue;

        for (int i = 0; i < Players.Count; i++)
        {
            NetworkPlayerController player = Players[i];
            if (player == null || player.Object == null || !IsAlivePlayer(player.Object))
            {
                continue;
            }

            float sqrDistance = (player.transform.position - position).sqrMagnitude;
            if (sqrDistance >= closestSqrDistance)
            {
                continue;
            }

            closestSqrDistance = sqrDistance;
            closest = player;
        }

        return closest;
    }

    /// 새로운 플레이어가 생성되거나 네트워크에 진입했을 때 레지스트리에 등록합니다.
    /// <param name="player">등록할 플레이어 컨트롤러 객체</param>
    public static void Register(NetworkPlayerController player)
    {
        // 1. 예외 처리: 전달된 객체가 null이면 등록을 중단합니다.
        if (player == null)
        {
            return;
        }

        // 2. 중복 등록 방지: 리스트에 존재하지 않을 때만 새로 추가합니다.
        if (!Players.Contains(player))
        {
            Players.Add(player);
        }

        // 3. 로컬 플레이어 판별: 
        // 네트워크 오브젝트가 존재하고, 해당 오브젝트에 입력 권한(Input Authority)이 있다면 이 클라이언트의 주인공(LocalPlayer)으로 지정합니다.
        if (player.Object != null && player.Object.HasInputAuthority)
        {
            LocalPlayer = player;
        }
    }

    /// 플레이어가 게임을 떠나거나 파괴되었을 때 레지스트리에서 제거합니다.
    /// <param name="player">제거할 플레이어 컨트롤러 객체</param>
    public static void Unregister(NetworkPlayerController player)
    {
        // 1. 예외 처리: 전달된 객체가 null이면 중단합니다.
        if (player == null)
        {
            return;
        }

        // 2. 리스트에서 해당 플레이어를 제거합니다.
        Players.Remove(player);

        // 3. 로컬 플레이어 갱신:
        // 방금 제거된 플레이어가 하필 현재 관리 중이던 'LocalPlayer'였다면, 남은 플레이어 중 로컬 플레이어가 있는지 다시 찾아 갱신합니다.
        if (LocalPlayer == player)
        {
            LocalPlayer = FindLocalPlayer();
        }
    }

    /// 내부 리스트를 순회하며 입력 권한(HasInputAuthority)을 가진 로컬 플레이어를 찾아 반환합니다.
    /// <returns>찾은 로컬 플레이어 객체 (없다면 null)</returns>
    private static NetworkPlayerController FindLocalPlayer()
    {
        // 가비지 컬렉션(GC) 방지를 위해 foreach 대신 인덱스 기반의 for 문을 사용해 리스트를 순회합니다.
        for (int i = 0; i < Players.Count; i++)
        {
            NetworkPlayerController player = Players[i];
            
            // 유효성 검사: 플레이어 객체와 네트워크 오브젝트가 존재하고, 본인 조작 권한이 있는지 확인
            if (player != null && player.Object != null && player.Object.HasInputAuthority)
            {
                return player; // 조건을 만족하는 첫 번째 로컬 플레이어를 즉시 반환
            }
        }

        // 만족하는 플레이어가 리스트에 없다면 null을 반환
        return null;
    }
}
