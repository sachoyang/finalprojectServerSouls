using Fusion;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq; 

public class NextLevelPortal : NetworkBehaviour
{
    [SerializeField] private bool playGateKickCutsceneBeforeLoad = true;
    [SerializeField] private float gateKickCutsceneFallbackDuration = 2.7f;

    private NetworkObject _localPlayerNetObj;
    private Collider _enteredCollider; // [핵심] 꼬임 방지를 위해 들어온 콜라이더의 영수증을 보관합니다.
    private bool _isReady = false; 
    private bool _isTransitioning = false;

    private HashSet<PlayerRef> _readyPlayers = new HashSet<PlayerRef>();

    [Networked] public int ReadyCount { get; set; }

    public override void FixedUpdateNetwork()
    {
        if (HasStateAuthority && ReadyCount > 0 && !_isTransitioning)
        {
            _readyPlayers.RemoveWhere(p => !Runner.ActivePlayers.Contains(p));
            ReadyCount = _readyPlayers.Count;

            int totalPlayers = Runner.ActivePlayers.Count();
            
            if (ReadyCount >= totalPlayers && totalPlayers > 0)
            {
                Debug.Log("[Portal] 전원 레디 완료! 다음 층으로 이동합니다.");
                _isTransitioning = true;
                ReadyCount = 0; 
                _readyPlayers.Clear();
                if (playGateKickCutsceneBeforeLoad && CutsceneManager.Instance != null)
                {
                    RPC_PlayGateKickCutscene(GetGateKickPlayer());
                    StartCoroutine(ChangeToNextLevelAfterCutscene(Runner));
                }
                else
                {
                    ChangeToNextLevel(Runner);
                }
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        NetworkObject netObj = other.GetComponentInParent<NetworkObject>();
        
        // 내 캐릭터이고, 아직 포탈에 안 들어온 상태일 때만 등록 (다중 콜라이더 꼬임 방지)
        if (netObj != null &&
            PlayerRegistry.IsLocalPlayer(netObj) &&
            _localPlayerNetObj == null)
        {
            _localPlayerNetObj = netObj;
            _enteredCollider = other; // 들어온 정확한 부위를 기억함
            Debug.Log($"[Portal] ✅ 진입 성공: {other.name}");
        }
    }

    private void OnTriggerExit(Collider other)
    {
        // 다른 잡다한 콜라이더가 아니라, 아까 들어왔던 '정확히 그 부위'가 나갔을 때만 취소
        if (_enteredCollider != null && other == _enteredCollider)
        {
            Debug.Log($"[Portal] 💨 이탈 확인됨: {other.name}. 레디 취소!");
            _localPlayerNetObj = null;
            _enteredCollider = null;

            if (_isReady)
            {
                _isReady = false;
                RPC_SetReady(Runner.LocalPlayer, false);
            }
        }
    }

    private void Update()
    {
        if (_localPlayerNetObj != null && Input.GetKeyDown(KeyCode.F) && !_isReady)
        {
            _isReady = true; 
            RPC_SetReady(Runner.LocalPlayer, true); 
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_SetReady(PlayerRef player, NetworkBool isReady)
    {
        if (isReady) _readyPlayers.Add(player);
        else _readyPlayers.Remove(player);
        
        ReadyCount = _readyPlayers.Count;
    }

    // ==========================================
    // 임시 프로토타입 UI
    // ==========================================
    private void OnGUI()
    {
        if (_localPlayerNetObj != null)
        {
            GUIStyle style = new GUIStyle(GUI.skin.box);
            style.fontSize = 24;
            style.alignment = TextAnchor.MiddleCenter;

            float width = 400f;
            float height = 70f;
            float x = (Screen.width - width) / 2f; 
            float y = Screen.height - height - 150f; 

            int totalPlayers = Runner != null ? Runner.ActivePlayers.Count() : 1;

            string text = _isReady 
                ? $"대기 중... ({ReadyCount} / {totalPlayers})" 
                : $"포탈 이동 [ F ] ({ReadyCount} / {totalPlayers})";

            GUI.Box(new Rect(x, y, width, height), text, style);
        }
    }
    // ==========================================

    private void ChangeToNextLevel(NetworkRunner runner)
    {
        if (GameProgressionManager.Instance == null) return;
        int currentLevel = GameProgressionManager.Instance.CurrentLevel;

        // Path 씬에서 회복된 체력/스태미나를 다음 보스 씬 스폰 전에 세션 저장소에 남긴다.
        PlayerSessionStore.SaveActivePlayerStats(runner);

        if (currentLevel >= 8)
        {
            runner.LoadScene("scEnding", UnityEngine.SceneManagement.LoadSceneMode.Single);
        }
        else
        {
            GameProgressionManager.Instance.GoToNextLevel(runner);
        }
    }

    private IEnumerator ChangeToNextLevelAfterCutscene(NetworkRunner runner)
    {
        CutsceneManager cutsceneManager = CutsceneManager.Instance;
        float delay = cutsceneManager != null
            ? cutsceneManager.GateKickCutsceneDuration
            : gateKickCutsceneFallbackDuration;

        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        ChangeToNextLevel(runner);
    }

    private PlayerRef GetGateKickPlayer()
    {
        if (Runner != null && Runner.LocalPlayer != default)
        {
            return Runner.LocalPlayer;
        }

        foreach (PlayerRef player in Runner.ActivePlayers)
        {
            return player;
        }

        return default;
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayGateKickCutscene(PlayerRef kickPlayer)
    {
        CutsceneManager cutsceneManager = CutsceneManager.Instance;
        if (cutsceneManager == null)
        {
            Debug.LogWarning("[Portal] CutsceneManager was not found.");
            return;
        }

        cutsceneManager.PlayGateKickCutscene(Runner, kickPlayer);
    }
}
