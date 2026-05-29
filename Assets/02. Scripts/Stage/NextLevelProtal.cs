using Fusion;
using UnityEngine;
using System.Collections.Generic;
using System.Linq; 

public class NextLevelPortal : NetworkBehaviour
{
    private NetworkObject _localPlayerNetObj;
    private bool _isReady = false; 

    // 서버(방장) 전용: 누가 F를 눌렀는지 기억하는 명부
    private HashSet<PlayerRef> _readyPlayers = new HashSet<PlayerRef>();

    // [핵심] 모든 클라이언트 화면에 보여줄 현재 레디 완료 인원수
    [Networked] public int ReadyCount { get; set; }

    public override void FixedUpdateNetwork()
    {
        // 방장(Host)이 매 프레임 인원수를 체크하여 모두 모였는지 확인합니다.
        if (HasStateAuthority && ReadyCount > 0)
        {
            // 혹시 레디한 사람 중 게임을 튕기거나 나간 사람이 있다면 명부에서 삭제
            _readyPlayers.RemoveWhere(p => !Runner.ActivePlayers.Contains(p));
            ReadyCount = _readyPlayers.Count;

            int totalPlayers = Runner.ActivePlayers.Count();
            
            // 🔥 디버그: 방장이 F를 눌렀을 때 총 인원을 제대로 세고 있는지 콘솔로 확인!
            Debug.Log($"[Portal] 씬 전환 체크 중... (현재 레디: {ReadyCount} / 총 인원: {totalPlayers})");

            // 전원 레디 완료 시 다음 층으로 발사!
            if (ReadyCount >= totalPlayers && totalPlayers > 0)
            {
                Debug.Log("[Portal] 전원 레디 완료! 다음 층으로 이동합니다.");
                ReadyCount = 0; 
                _readyPlayers.Clear();
                ChangeToNextLevel(Runner);
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        NetworkObject netObj = other.GetComponentInParent<NetworkObject>();
        if (netObj != null && netObj.HasInputAuthority)
        {
            _localPlayerNetObj = netObj;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        NetworkObject netObj = other.GetComponentInParent<NetworkObject>();
        if (netObj != null && netObj.HasInputAuthority)
        {
            _localPlayerNetObj = null;

            // 포탈 밖으로 나가면 레디 취소!
            if (_isReady)
            {
                _isReady = false;
                RPC_SetReady(Runner.LocalPlayer, false);
            }
        }
    }

    private void Update()
    {
        // 🔥 여기가 핵심! 구버전의 "if (playerObj.HasStateAuthority)" 방장 프리패스 로직을 완전히 삭제했습니다.
        // 이제 방장이든 클라이언트든 평등하게 RPC 통신만 쏩니다.
        if (_localPlayerNetObj != null && Input.GetKeyDown(KeyCode.F) && !_isReady)
        {
            _isReady = true; 
            RPC_SetReady(Runner.LocalPlayer, true); 
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_SetReady(PlayerRef player, NetworkBool isReady)
    {
        if (isReady)
        {
            _readyPlayers.Add(player);
        }
        else
        {
            _readyPlayers.Remove(player);
        }
        
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

        if (currentLevel >= 8)
        {
            runner.LoadScene("scEnding", UnityEngine.SceneManagement.LoadSceneMode.Single);
        }
        else
        {
            GameProgressionManager.Instance.GoToNextLevel(runner);
        }
    }
}