using Fusion;
using TMPro;
using UnityEngine;

public class Player : NetworkBehaviour
{
  [SerializeField] private Ball _prefabBall;
  [SerializeField] private PhysxBall _prefabPhysxBall;


  [Networked] private TickTimer delay { get; set; }

  [Networked]
  public bool spawnedProjectile { get; set; }
  public Material _material;


  private NetworkCharacterController _cc;
  private Vector3 _forward;
  private ChangeDetector _changeDetector;


  private void Awake()
  {
    _cc = GetComponent<NetworkCharacterController>();
    _forward = transform.forward;
    _material = GetComponentInChildren<MeshRenderer>().material;
  }

  private void Update()
  {
    if (Object.HasInputAuthority && Input.GetKeyDown(KeyCode.R))
    {
      RPC_SendMessage("Hey Mate!");
    }
  }
  public override void Spawned()
  {
    _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);

    // [추가된 코드] 이 캐릭터가 '나의 입력 권한'을 가진 로컬 플레이어라면
    if (Object.HasInputAuthority)
    {
      Camera mainCam = Camera.main;
      if (mainCam != null)
      {
        // 카메라에 Follow 스크립트가 없다면 추가, 있다면 타겟 설정
        CameraFollow followScript = mainCam.gameObject.GetComponent<CameraFollow>();
        if (followScript == null)
        {
          followScript = mainCam.gameObject.AddComponent<CameraFollow>();
        }
        followScript.target = this.transform;
      }
    }
  }

  public override void Render()
  {
    foreach (var change in _changeDetector.DetectChanges(this))
    {
      switch (change)
      {
        case nameof(spawnedProjectile):
          _material.color = Color.white;
          break;
      }
    }
    _material.color = Color.Lerp(_material.color, Color.blue, Time.deltaTime);
  }

  public override void FixedUpdateNetwork()
  {
    if (GetInput(out NetworkInputData data))
    {
      data.direction.Normalize();
      _cc.Move(5 * data.direction * Runner.DeltaTime);

      if (data.direction.sqrMagnitude > 0)
        _forward = data.direction;

      if (HasStateAuthority && delay.ExpiredOrNotRunning(Runner))
      {
        if (data.buttons.IsSet(NetworkInputData.MOUSEBUTTON0))
        {
          delay = TickTimer.CreateFromSeconds(Runner, 0.5f);
          Runner.Spawn(_prefabBall,
            transform.position + _forward,
            Quaternion.LookRotation(_forward),
            Object.InputAuthority,
            (runner, o) =>
            {
              // Initialize the Ball before synchronizing it
              o.GetComponent<Ball>().Init();
            });
          spawnedProjectile = !spawnedProjectile;

        }
        else if (data.buttons.IsSet(NetworkInputData.MOUSEBUTTON1))
        {
          delay = TickTimer.CreateFromSeconds(Runner, 0.5f);
          Runner.Spawn(_prefabPhysxBall,
            transform.position + _forward,
            Quaternion.LookRotation(_forward),
            Object.InputAuthority,
            (runner, o) =>
            {
              o.GetComponent<PhysxBall>().Init(10 * _forward);
            });
          spawnedProjectile = !spawnedProjectile;
        }
      }
    }
  }

  private TMP_Text _messages;

  [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority, HostMode = RpcHostMode.SourceIsHostPlayer)]
  public void RPC_SendMessage(string message, RpcInfo info = default)
  {
    RPC_RelayMessage(message, info.Source);
  }

  [Rpc(RpcSources.StateAuthority, RpcTargets.All, HostMode = RpcHostMode.SourceIsServer)]
  public void RPC_RelayMessage(string message, PlayerRef messageSource)
  {
    if (_messages == null)
      _messages = FindObjectOfType<TMP_Text>();

    if (messageSource == Runner.LocalPlayer)
    {
      message = $"You said: {message}\n";
    }
    else
    {
      message = $"Some other player said: {message}\n";
    }

    _messages.text += message;
  }
}