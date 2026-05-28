using Fusion;
using Fusion.Sockets;
using System;
using System.Collections.Generic;
using UnityEngine;

public class BossArenaManager : NetworkBehaviour, INetworkRunnerCallbacks
{
    [Header("스폰 설정")]
    [SerializeField] private NetworkPrefabRef _playerPrefab;
    [SerializeField] private Transform[] _spawnPoints;

    private readonly Dictionary<PlayerRef, NetworkObject> _spawnedCharacters = new Dictionary<PlayerRef, NetworkObject>();

    private bool _mouseButton0;
    private bool _mouseButton1;
    private bool _jumpPressed;
    private bool _lockOnPressed;
    private bool _lockOnCancelPressed;

    public override void Spawned()
    {
        Runner.AddCallbacks(this);

        if (HasStateAuthority)
        {
            foreach (var player in Runner.ActivePlayers)
            {
                SpawnPlayer(Runner, player);
            }
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        Runner.RemoveCallbacks(this);
    }

    private void SpawnPlayer(NetworkRunner runner, PlayerRef player)
    {
        if (_spawnedCharacters.ContainsKey(player)) return;

        int playerIndex = player.RawEncoded % Mathf.Max(1, _spawnPoints.Length);
        Vector3 spawnPos = _spawnPoints.Length > 0 ? _spawnPoints[playerIndex].position : Vector3.right * (playerIndex * 3);
        Quaternion spawnRot = _spawnPoints.Length > 0 ? _spawnPoints[playerIndex].rotation : Quaternion.identity;

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

    private void Update()
    {
        _mouseButton0 = _mouseButton0 || Input.GetMouseButton(0);
        _mouseButton1 = _mouseButton1 || Input.GetMouseButtonDown(1);
        _jumpPressed = _jumpPressed || Input.GetKeyDown(KeyCode.Space);
        _lockOnPressed = _lockOnPressed || Input.GetKeyDown(KeyCode.Q);
        _lockOnCancelPressed = _lockOnCancelPressed || Input.GetKeyDown(KeyCode.E);
    }

    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        // [수정됨] 누락되었던 이동 벡터(WASD)를 구해서 data.direction에 넣어줍니다!
        var data = new NetworkInputData
        {
            direction = GetCameraRelativeMove(ReadMoveInput())
        };
        
        data.buttons.Set(NetworkInputData.MOUSEBUTTON0, _mouseButton0); _mouseButton0 = false;
        data.buttons.Set(NetworkInputData.MOUSEBUTTON1, _mouseButton1); _mouseButton1 = false;
        data.buttons.Set(NetworkInputData.SHIFT, Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift));
        data.buttons.Set(NetworkInputData.JUMP, _jumpPressed); _jumpPressed = false;
        data.buttons.Set(NetworkInputData.LOCKON, _lockOnPressed); _lockOnPressed = false;
        data.buttons.Set(NetworkInputData.LOCKON_CANCEL, _lockOnCancelPressed); _lockOnCancelPressed = false;

        input.Set(data);
    }

    // ==========================================
    // [복구됨] 방향키(WASD) 입력을 읽어오는 함수
    // ==========================================
    private static Vector2 ReadMoveInput()
    {
        Vector2 moveInput = Vector2.zero;

        if (Input.GetKey(KeyCode.W)) moveInput.y += 1f;
        if (Input.GetKey(KeyCode.S)) moveInput.y -= 1f;
        if (Input.GetKey(KeyCode.A)) moveInput.x -= 1f;
        if (Input.GetKey(KeyCode.D)) moveInput.x += 1f;

        return moveInput.sqrMagnitude > 1f ? moveInput.normalized : moveInput;
    }

    // ==========================================
    // [복구됨] 입력된 방향을 메인 카메라 기준으로 변환하는 함수
    // ==========================================
    private static Vector3 GetCameraRelativeMove(Vector2 moveInput)
    {
        if (moveInput.sqrMagnitude <= 0.0001f) return Vector3.zero;

        Transform reference = Camera.main != null ? Camera.main.transform : null;
        Vector3 forward = reference != null ? Vector3.ProjectOnPlane(reference.forward, Vector3.up) : Vector3.forward;
        Vector3 right = reference != null ? Vector3.ProjectOnPlane(reference.right, Vector3.up) : Vector3.right;

        if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
        else forward.Normalize();

        if (right.sqrMagnitude < 0.0001f) right = Vector3.right;
        else right.Normalize();

        Vector3 moveDirection = forward * moveInput.y + right * moveInput.x;
        return moveDirection.sqrMagnitude > 1f ? moveDirection.normalized : moveDirection;
    }

    // ==========================================
    // INetworkRunnerCallbacks 빈 껍데기들
    // ==========================================
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
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
    public void OnConnectedToServer(NetworkRunner runner) { }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnConnectFailed(NetworkRunner runner, Fusion.Sockets.NetAddress remoteAddress, NetConnectFailedReason reason) { }
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
