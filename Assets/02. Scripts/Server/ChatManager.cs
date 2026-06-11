using Fusion;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections; // 🔥 이벤트 시스템 제어를 위해 필수!

public class ChatManager : NetworkBehaviour
{
    [Header("UI 연결")]
    public InputField chatInput;
    public Text chatDisplay;
    public ScrollRect scrollRect;

    private static string globalChatHistory = "";
    private bool _canOpenChat = true;

    private void Start()
    {
        chatDisplay.text = globalChatHistory;
        if (scrollRect != null) scrollRect.verticalNormalizedPosition = 0f;

        // 🔥 [핵심 수정] 엔터키(제출) 처리를 유니티 기본 이벤트에 맡깁니다!
        chatInput.onSubmit.AddListener(OnChatSubmit);
    }

    private void Update()
    {
        // 포커스가 '없을 때' 엔터를 누르면 채팅창을 켭니다.
        if (_canOpenChat && !chatInput.isFocused && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
        {
            chatInput.ActivateInputField();
        }
    }

    // 🔥 유니티 InputField가 엔터키 입력을 완전히 처리한 '직후'에 안전하게 호출되는 함수
    private void OnChatSubmit(string text)
    {
        // 내용이 비어있지 않다면 전송
        if (!string.IsNullOrWhiteSpace(text))
        {
            string myNickname = BackendManager.Instance.CurrentNickname;
            if (string.IsNullOrEmpty(myNickname))
            {
                myNickname = "Unknown"; 
            }

            RPC_BroadcastMessage(myNickname, text);
        }

        // 여기서 글자를 비워주면 시스템과 충돌이 나지 않습니다.
        chatInput.text = "";

        // 연속해서 채팅을 칠 수 있도록 포커스 강제 유지 (선택사항)
        // 만약 한 번 치고 이동(WASD)을 해야 한다면 아래 줄을 지워주세요!
        //chatInput.ActivateInputField();
        chatInput.DeactivateInputField(); // 입력창 끄기
        
        // 중요: UI나 게임 오브젝트에 잡혀있는 선택권을 강제로 해제합니다.
        // 이것을 해야 엔터를 눌렀을 때 입력창이 다시 켜지는 루프를 방지하고 게임으로 돌아갑니다.
        EventSystem.current.SetSelectedGameObject(null);
        StartCoroutine(ReopenCooldown());
    }

    private IEnumerator ReopenCooldown()
    {
        _canOpenChat = false; // 일시적으로 엔터 감지 차단
        yield return new WaitForSeconds(0.1f); // 0.1초 뒤에
        _canOpenChat = true; // 다시 엔터 감지 허용
    }

    [Rpc(RpcSources.All, RpcTargets.All)]
    private void RPC_BroadcastMessage(string senderNickname, string message)
    {
        string newLog = $"\n<color=yellow>[{senderNickname}]</color> {message}";
        
        chatDisplay.text += newLog;
        globalChatHistory += newLog;

        Canvas.ForceUpdateCanvases();
        
        if (scrollRect != null)
        {
            scrollRect.verticalNormalizedPosition = 0f; 
        }
    }
}