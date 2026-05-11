using Fusion;
using Fusion.Addons.Physics;
using Fusion.Sockets;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class CameraRelativeBasicSpawner : MonoBehaviour, INetworkRunnerCallbacks
{
    [SerializeField] private NetworkPrefabRef _playerPrefab;

    private readonly Dictionary<PlayerRef, NetworkObject> _spawnedCharacters = new Dictionary<PlayerRef, NetworkObject>();
    private bool _mouseButton0;
    private bool _mouseButton1;
    private bool _jumpPressed;
    private bool _lockOnPressed;
    private bool _lockOnCancelPressed;
    private NetworkRunner _runner;

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        if (runner.IsServer)
        {
            Vector3 spawnOffset = Vector3.right * ((player.RawEncoded % runner.Config.Simulation.PlayerCount) * 3);
            Vector3 spawnPosition = transform.position + spawnOffset;
            NetworkObject networkPlayerObject = runner.Spawn(_playerPrefab, spawnPosition, Quaternion.identity, player);
            _spawnedCharacters.Add(player, networkPlayerObject);
        }
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        if (_spawnedCharacters.TryGetValue(player, out NetworkObject networkObject))
        {
            runner.Despawn(networkObject);
            _spawnedCharacters.Remove(player);
        }
    }

    private void Update()
    {
        _mouseButton0 = _mouseButton0 | Input.GetMouseButton(0);
        _mouseButton1 = _mouseButton1 || Input.GetMouseButton(1);
        _jumpPressed = _jumpPressed || Input.GetKeyDown(KeyCode.Space);
        _lockOnPressed = _lockOnPressed || Input.GetKeyDown(KeyCode.Q);
        _lockOnCancelPressed = _lockOnCancelPressed || Input.GetKeyDown(KeyCode.E);
    }

    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        var data = new NetworkInputData
        {
            direction = GetCameraRelativeMove(ReadMoveInput())
        };

        data.buttons.Set(NetworkInputData.MOUSEBUTTON0, _mouseButton0);
        _mouseButton0 = false;
        data.buttons.Set(NetworkInputData.MOUSEBUTTON1, _mouseButton1);
        _mouseButton1 = false;
        data.buttons.Set(
            NetworkInputData.SHIFT,
            Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift));
        data.buttons.Set(NetworkInputData.JUMP, _jumpPressed);
        _jumpPressed = false;
        data.buttons.Set(NetworkInputData.LOCKON, _lockOnPressed);
        _lockOnPressed = false;
        data.buttons.Set(NetworkInputData.LOCKON_CANCEL, _lockOnCancelPressed);
        _lockOnCancelPressed = false;

        input.Set(data);
    }

    private static Vector2 ReadMoveInput()
    {
        Vector2 moveInput = Vector2.zero;

        if (Input.GetKey(KeyCode.W))
            moveInput.y += 1f;

        if (Input.GetKey(KeyCode.S))
            moveInput.y -= 1f;

        if (Input.GetKey(KeyCode.A))
            moveInput.x -= 1f;

        if (Input.GetKey(KeyCode.D))
            moveInput.x += 1f;

        return moveInput.sqrMagnitude > 1f ? moveInput.normalized : moveInput;
    }

    private static Vector3 GetCameraRelativeMove(Vector2 moveInput)
    {
        if (moveInput.sqrMagnitude <= 0.0001f)
        {
            return Vector3.zero;
        }

        Transform reference = Camera.main != null ? Camera.main.transform : null;
        Vector3 forward = reference != null ? Vector3.ProjectOnPlane(reference.forward, Vector3.up) : Vector3.forward;
        Vector3 right = reference != null ? Vector3.ProjectOnPlane(reference.right, Vector3.up) : Vector3.right;

        if (forward.sqrMagnitude < 0.0001f)
        {
            forward = Vector3.forward;
        }
        else
        {
            forward.Normalize();
        }

        if (right.sqrMagnitude < 0.0001f)
        {
            right = Vector3.right;
        }
        else
        {
            right.Normalize();
        }

        Vector3 moveDirection = forward * moveInput.y + right * moveInput.x;
        return moveDirection.sqrMagnitude > 1f ? moveDirection.normalized : moveDirection;
    }

    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }
    public void OnConnectedToServer(NetworkRunner runner) { }
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
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

    private async void StartGame(GameMode mode)
    {
        _runner = gameObject.AddComponent<NetworkRunner>();
        _runner.ProvideInput = true;
        var runnerSimulatePhysics3D = gameObject.AddComponent<RunnerSimulatePhysics3D>();
        runnerSimulatePhysics3D.ClientPhysicsSimulation = ClientPhysicsSimulation.SimulateAlways;

        var scene = SceneRef.FromIndex(SceneManager.GetActiveScene().buildIndex);

        await _runner.StartGame(new StartGameArgs()
        {
            GameMode = mode,
            SessionName = "TestRoom",
            Scene = scene,
            SceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>()
        });
    }

    private void OnGUI()
    {
        if (_runner == null)
        {
            if (GUI.Button(new Rect(50, 50, 200, 40), "Host"))
            {
                StartGame(GameMode.Host);
            }

            if (GUI.Button(new Rect(50, 100, 200, 40), "Join"))
            {
                StartGame(GameMode.Client);
            }
        }
    }
}
