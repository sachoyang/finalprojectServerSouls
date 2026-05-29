using Fusion;
using Fusion.Sockets;
using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class NetworkInputProvider : MonoBehaviour, INetworkRunnerCallbacks
{
    // Fusion의 OnInput 호출 시점까지 프레임 입력을 잃지 않도록 Update에서 먼저 캐싱한다.
    private NetworkRunner _runner;
    private bool _mouseButton0;
    private bool _mouseButton1;
    private bool _jumpPressed;
    private bool _lockOnPressed;
    private bool _lockOnCancelPressed;

    private void OnEnable()
    {
        // NetworkRunner가 DontDestroyOnLoad 오브젝트에 있을 수 있으므로 활성화 시점에 먼저 등록을 시도한다.
        TryRegisterRunner();
    }

    private void Update()
    {
        TryRegisterRunner();

        // GetKeyDown 계열 입력은 OnInput보다 먼저 지나갈 수 있어 bool로 누적해 둔다.
        _mouseButton0 = _mouseButton0 || Input.GetMouseButtonDown(0);
        _mouseButton1 = _mouseButton1 || Input.GetMouseButtonDown(1);
        _jumpPressed = _jumpPressed || Input.GetKeyDown(KeyCode.Space);
        _lockOnPressed = _lockOnPressed || Input.GetKeyDown(KeyCode.Q);
        _lockOnCancelPressed = _lockOnCancelPressed || Input.GetKeyDown(KeyCode.E);
    }

    private void OnDisable()
    {
        if (_runner == null)
        {
            return;
        }

        _runner.RemoveCallbacks(this);
        _runner = null;
    }

    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        // 클라이언트 입력을 Fusion 네트워크 입력 구조체로 변환해 플레이어 컨트롤러에 전달한다.
        var data = new NetworkInputData
        {
            direction = GetCameraRelativeMove(ReadMoveInput())
        };

        data.buttons.Set(NetworkInputData.MOUSEBUTTON0, _mouseButton0);
        data.buttons.Set(NetworkInputData.MOUSEBUTTON1, _mouseButton1);
        data.buttons.Set(NetworkInputData.SHIFT, Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift));
        data.buttons.Set(NetworkInputData.JUMP, _jumpPressed);
        data.buttons.Set(NetworkInputData.LOCKON, _lockOnPressed);
        data.buttons.Set(NetworkInputData.LOCKON_CANCEL, _lockOnCancelPressed);

        _mouseButton0 = false;
        _mouseButton1 = false;
        _jumpPressed = false;
        _lockOnPressed = false;
        _lockOnCancelPressed = false;

        input.Set(data);
    }

    private void TryRegisterRunner()
    {
        if (_runner != null)
        {
            return;
        }

        NetworkRunner runner = NetworkManager.Instance != null ? NetworkManager.Instance.Runner : null;
        runner ??= FindObjectOfType<NetworkRunner>();
        if (runner == null)
        {
            return;
        }

        _runner = runner;
        _runner.AddCallbacks(this);
    }

    private static Vector2 ReadMoveInput()
    {
        Vector2 moveInput = Vector2.zero;

        if (Input.GetKey(KeyCode.W)) moveInput.y += 1f;
        if (Input.GetKey(KeyCode.S)) moveInput.y -= 1f;
        if (Input.GetKey(KeyCode.A)) moveInput.x -= 1f;
        if (Input.GetKey(KeyCode.D)) moveInput.x += 1f;

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

        if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
        else forward.Normalize();

        if (right.sqrMagnitude < 0.0001f) right = Vector3.right;
        else right.Normalize();

        Vector3 moveDirection = forward * moveInput.y + right * moveInput.x;
        return moveDirection.sqrMagnitude > 1f ? moveDirection.normalized : moveDirection;
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
    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player) { }
    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
}
