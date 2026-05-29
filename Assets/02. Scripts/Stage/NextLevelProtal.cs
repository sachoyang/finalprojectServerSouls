using Fusion;
using UnityEngine;

public class NextLevelPortal : NetworkBehaviour
{
    private NetworkObject _localPlayerNetObj;

    private void OnTriggerEnter(Collider other)
    {
        NetworkObject netObj = other.GetComponentInParent<NetworkObject>();
        
        // 내 캐릭터가 포탈에 들어왔을 때만 인식
        if (netObj != null && netObj.HasInputAuthority)
        {
            _localPlayerNetObj = netObj;
            Debug.Log("[Portal] ✅ 내 캐릭터 포탈 진입! (OnGUI 활성화)");
        }
    }

    private void OnTriggerExit(Collider other)
    {
        NetworkObject netObj = other.GetComponentInParent<NetworkObject>();
        
        // 내 캐릭터가 포탈에서 나가면 인식 해제
        if (netObj != null && netObj.HasInputAuthority)
        {
            _localPlayerNetObj = null;
            Debug.Log("[Portal] 포탈 벗어남. (OnGUI 비활성화)");
        }
    }

    private void Update()
    {
        if (_localPlayerNetObj != null && Input.GetKeyDown(KeyCode.F))
        {
            NetworkObject playerObj = _localPlayerNetObj;
            _localPlayerNetObj = null; // 중복 클릭 방지 및 UI 즉시 숨김

            if (playerObj.HasStateAuthority)
            {
                ChangeToNextLevel(playerObj.Runner);
            }
            else
            {
                if (Object != null) RPC_RequestNextLevel();
            }
        }
    }

    // ==========================================
    // [UI 담당자 인계용] 임시 프로토타입 UI
    // ==========================================
    private void OnGUI()
    {
        // 내 캐릭터가 콜라이더 안에 있을 때만 화면에 그립니다.
        if (_localPlayerNetObj != null)
        {
            // 글씨가 잘 보이도록 스타일 살짝 조정
            GUIStyle style = new GUIStyle(GUI.skin.box);
            style.fontSize = 24;
            style.alignment = TextAnchor.MiddleCenter;

            float width = 300f;
            float height = 60f;
            float x = (Screen.width - width) / 2f; // 가로 중앙
            float y = Screen.height - height - 150f; // 세로 하단에서 살짝 위

            GUI.Box(new Rect(x, y, width, height), "포탈 이동 [ F ]", style);
        }
    }

    // ==========================================

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestNextLevel()
    {
        ChangeToNextLevel(Runner);
    }

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