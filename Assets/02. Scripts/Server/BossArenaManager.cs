using Fusion;
using Fusion.Sockets;
using System;
using System.Collections.Generic;
using UnityEngine;

public class BossArenaManager : NetworkBehaviour, INetworkRunnerCallbacks
{
    [Header("플레이어 스폰 설정")]
    [SerializeField] private NetworkPrefabRef _playerPrefab;
    [SerializeField] private Transform[] _spawnPoints;

    [Header("보스 스폰 설정")]
    [SerializeField] private Transform _bossSpawnPoint;

    private readonly Dictionary<PlayerRef, NetworkObject> _spawnedCharacters = new Dictionary<PlayerRef, NetworkObject>();
    private NetworkObject _currentBossObject;

    // 방장이 클라이언트들에게 현재 층수를 알려주기 위한 통신 변수
    [Networked] public int NetworkedStageLevel { get; set; }

    // 🕒 방장이 잰 '전투 소요 시간(초)'을 클라이언트에게 동기화하기 위한 통신 변수.
    //    같이 플레이한 사람 모두 엔딩에서 같은 시간을 봐야 하므로 호스트 값이 기준이다.
    [Networked] public float NetworkedRunSeconds { get; set; }

    public override void Spawned()
    {
        Runner.AddCallbacks(this);

        // 스폰과 보스 세팅은 서버 권한에서만 처리한다.
        if (HasStateAuthority)
        {
            // 방장은 자신의 로컬 층수를 네트워크 변수에 올립니다.
            if (GameProgressionManager.Instance != null)
                NetworkedStageLevel = GameProgressionManager.Instance.CurrentLevel;

            foreach (PlayerRef player in Runner.ActivePlayers)
            {
                SpawnPlayer(Runner, player);
            }

            SetupAndSpawnBoss();
        }
    }

    public override void FixedUpdateNetwork()
    {
        // 🕒 방장은 자신이 잰 전투 소요 시간을 매 틱 네트워크 변수에 올린다. (게스트가 이 값으로 맞춤)
        if (HasStateAuthority && GameProgressionManager.Instance != null)
        {
            NetworkedRunSeconds = GameProgressionManager.Instance.RunCombatSeconds;
        }
    }

    public override void Render()
    {
        // 클라이언트는 방장이 올려둔 층수를 계속 읽어와서 자기 통제실을 업데이트합니다.
        if (!HasStateAuthority && GameProgressionManager.Instance != null)
        {
            if (NetworkedStageLevel > 0)
                GameProgressionManager.Instance.SetLevelFromHost(NetworkedStageLevel);

            // 🕒 전투 소요 시간도 방장 값으로 계속 덮어써서 모두 동일하게 유지한다.
            GameProgressionManager.Instance.SetRunSecondsFromHost(NetworkedRunSeconds);
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        Runner.RemoveCallbacks(this);
    }

    private void SpawnPlayer(NetworkRunner runner, PlayerRef player)
    {
        if (_spawnedCharacters.ContainsKey(player))
        {
            return;
        }

        int playerIndex = player.RawEncoded % Mathf.Max(1, _spawnPoints.Length);
        Vector3 spawnPos = _spawnPoints.Length > 0 ? _spawnPoints[playerIndex].position : Vector3.right * (playerIndex * 3);
        Quaternion spawnRot = _spawnPoints.Length > 0 ? _spawnPoints[playerIndex].rotation : Quaternion.identity;

        // 이전 씬에서 유지된 플레이어 오브젝트가 있으면 새 위치로 옮기고 세션 상태만 복원한다.
        if (runner.TryGetPlayerObject(player, out NetworkObject networkPlayerObject) &&
            networkPlayerObject != null &&
            networkPlayerObject.gameObject.scene == gameObject.scene)
        {
            networkPlayerObject.transform.SetPositionAndRotation(spawnPos, spawnRot);
            RestoreSessionState(networkPlayerObject, player);
            _spawnedCharacters.Add(player, networkPlayerObject);
            return;
        }

        networkPlayerObject = runner.Spawn(_playerPrefab, spawnPos, spawnRot, player);
        runner.SetPlayerObject(player, networkPlayerObject);
        RestoreSessionState(networkPlayerObject, player);
        _spawnedCharacters.Add(player, networkPlayerObject);
    }

    private static void RestoreSessionState(NetworkObject playerObject, PlayerRef player)
    {
        if (playerObject == null)
        {
            return;
        }

        PlayerAbilityInventory inventory = playerObject.GetComponent<PlayerAbilityInventory>();
        if (inventory != null)
        {
            inventory.RestoreFromSessionData(player);
        }

        PlayerStats stats = playerObject.GetComponent<PlayerStats>();
        if (stats != null && PlayerSessionStore.TryGetStats(player, out PlayerStats.SessionSnapshot snapshot))
        {
            stats.RestoreSessionSnapshot(snapshot);
        }
    }

    private void SetupAndSpawnBoss()
    {
        if (GameProgressionManager.Instance == null)
        {
            return;
        }

        int level = GameProgressionManager.Instance.CurrentLevel;
        BossEncounterData bossData = GameProgressionManager.Instance.CurrentBossData;

        if (bossData == null || bossData.bossPrefab == null || _bossSpawnPoint == null)
        {
            return;
        }

        int maxPhase = level >= 1 ? 2 : 1;
        float hpMult = 1.0f + ((level - 1) * 0.2f);
        float dmgMult = 1.0f + ((level - 1) * 0.1f);

        Debug.Log($"[BossArenaManager] {level}층 보스 '{bossData.bossName}' 스폰. MaxPhase: {maxPhase}, HP: {hpMult}x, Damage: {dmgMult}x");

        // 보스 프리팹은 NetworkBossCore를 공통 진입점으로 사용해 층수 보정을 주입한다.
        _currentBossObject = Runner.Spawn(
            bossData.bossPrefab,
            _bossSpawnPoint.position,
            _bossSpawnPoint.rotation,
            null,
            (runner, obj) =>
            {
                NetworkBossCore bossCore = obj.GetComponent<NetworkBossCore>();
                if (bossCore != null)
                {
                    bossCore.AllowedMaxPhase = maxPhase;
                    bossCore.DamageMultiplier = dmgMult;

                    //baseMaxHP를 바탕으로 뻥튀기된 진짜 maxHP를 설정합니다.
                    bossCore.maxHP = bossCore.baseMaxHP * hpMult;

                    bossCore.bossName = bossData.bossName;
                }
            }
        );
    }

    public void OnInput(NetworkRunner runner, NetworkInput input) { }
    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        if (HasStateAuthority)
        {
            SpawnPlayer(runner, player);
        }
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        if (HasStateAuthority && _spawnedCharacters.TryGetValue(player, out NetworkObject networkObject))
        {
            runner.Despawn(networkObject);
            _spawnedCharacters.Remove(player);
        }
    }

    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }
    void INetworkRunnerCallbacks.OnConnectedToServer(NetworkRunner runner) { }
    void INetworkRunnerCallbacks.OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnSceneLoadDone(NetworkRunner runner) { }
    public void OnSceneLoadStart(NetworkRunner runner) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
}
