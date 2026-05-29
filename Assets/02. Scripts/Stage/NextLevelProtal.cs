using Fusion;
using UnityEngine;

public class NextLevelPortal : NetworkBehaviour
{
    private bool _isLocalPlayerInPortal = false;

    private void OnTriggerEnter(Collider other)
    {
        // 1. 누가 닿았는지 이름과 태그 확인
        Debug.Log($"[Portal] 닿은 오브젝트: {other.gameObject.name} (태그: {other.gameObject.tag})");

        NetworkObject netObj = other.GetComponentInParent<NetworkObject>();
        
        // 2. NetworkObject를 아예 못 찾았을 경우
        if (netObj == null)
        {
            Debug.Log($"[Portal] ❌ {other.gameObject.name}에서 NetworkObject를 찾을 수 없습니다! (루트 부모에 없거나 분리됨)");
            return;
        }

        // 3. NetworkObject는 찾았는데 권한이 어떻게 되어있는지 확인
        Debug.Log($"[Portal] 🔍 NetworkObject 찾음! (ID: {netObj.Id}) | InputAuthority: {netObj.HasInputAuthority} | StateAuthority: {netObj.HasStateAuthority}");

        // 4. 내 캐릭터가 맞는지 최종 확인
        if (netObj.HasInputAuthority)
        {
            _isLocalPlayerInPortal = true;
            Debug.Log("[Portal] ✅ 내 캐릭터 포탈 진입 인식 성공! F키를 누르세요.");
        }
        else
        {
            Debug.Log("[Portal] ⚠️ 내 캐릭터가 아닙니다. (다른 유저의 캐릭터이거나 권한 없음)");
        }
    }

    // 플레이어가 콜라이더 밖으로 나갔을 때
    private void OnTriggerExit(Collider other)
    {
        NetworkObject netObj = other.GetComponent<NetworkObject>();
        if (netObj != null && netObj.HasInputAuthority)
        {
            _isLocalPlayerInPortal = false;
            // 💡 팁: 여기서 상호작용 UI를 다시 숨겨줍니다.
        }
    }

    private void Update()
    {
        // 콜라이더 안에 있고 + F키를 눌렀다면?
        if (_isLocalPlayerInPortal && Input.GetKeyDown(KeyCode.F))
        {
            _isLocalPlayerInPortal = false; // 중복 클릭 방지
            RPC_RequestNextLevel(); // 방장에게 씬 전환 요청!
        }
    }

    // [핵심] 클라이언트가 F를 눌러도, 씬 전환(LoadScene)은 무조건 방장(StateAuthority)이 해야 합니다.
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestNextLevel()
    {
        if (GameProgressionManager.Instance == null) return;

        int currentLevel = GameProgressionManager.Instance.CurrentLevel;

        if (currentLevel >= 8)
        {
            Debug.Log("🎉 8층 클리어! 엔딩 씬으로 이동합니다.");
            // 'scEnding' 부분은 실제 만들어두실 엔딩 씬 이름으로 변경하세요!
            Runner.LoadScene("scEnding", UnityEngine.SceneManagement.LoadSceneMode.Single);
        }
        else
        {
            Debug.Log($"현재 {currentLevel}층 클리어. 다음 층으로 이동합니다!");
            GameProgressionManager.Instance.GoToNextLevel(Runner);
        }
    }
}