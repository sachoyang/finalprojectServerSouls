using Fusion;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class MatchManager : MonoBehaviour
{
    [Header("UI Reference")]
    public Text roomCodeText;         // 방 생성 시 코드를 보여줄 텍스트
    public InputField joinCodeInput;  // 접속할 코드를 적는 입력창

    private NetworkRunner _runner;

    // 1. 자동 매칭 버튼 클릭 시
    public void OnClickAutoMatch()
    {
        // SessionName을 비워두면 빈 방을 찾거나 무작위 방을 파서 들어갑니다.
        StartSession(GameMode.AutoHostOrClient, ""); 
    }

    // 2. 방 만들기(코드 생성) 버튼 클릭 시
    public void OnClickCreateRoom()
    {
        string newCode = GenerateRoomCode();
        roomCodeText.text = "방 코드: " + newCode; // 화면에 코드 표시
        
        // 코드를 이름으로 하는 방을 호스트로 생성
        StartSession(GameMode.Host, newCode);
    }

    // 3. 코드 입력 접속 버튼 클릭 시
    public void OnClickJoinRoom()
    {
        string inputCode = joinCodeInput.text.ToUpper(); // 소문자로 쳐도 대문자로 변환
        if (string.IsNullOrEmpty(inputCode))
        {
            Debug.LogWarning("코드를 입력해주세요!");
            return;
        }

        // 입력한 코드의 방으로 클라이언트 자격 접속
        StartSession(GameMode.Client, inputCode);
    }

    private string GenerateRoomCode()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        string code = "";
        for (int i = 0; i < 5; i++)
        {
            code += chars[Random.Range(0, chars.Length)];
        }
        return code;
    }

    // 실제 포톤 세션 연결을 담당하는 핵심 함수
    private async void StartSession(GameMode mode, string sessionName)
    {
        if (_runner == null)
        {
            _runner = gameObject.AddComponent<NetworkRunner>();
            _runner.ProvideInput = true;
        }

        var sceneInfo = new NetworkSceneInfo();
        // 일단 현재 씬(타이틀/로비)을 네트워크 씬으로 등록
        sceneInfo.AddSceneRef(SceneRef.FromIndex(SceneManager.GetActiveScene().buildIndex), LoadSceneMode.Additive);

        Debug.Log($"접속 시도 중... 모드: {mode}, 방 이름: {sessionName}");

        await _runner.StartGame(new StartGameArgs()
        {
            GameMode = mode,
            SessionName = sessionName,
            PlayerCount = NetworkManager.MaxPlayers, // 🔒 정원 3명 (미지정 시 config 기본값 10명)
            Scene = sceneInfo,
            SceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>()
        });

        Debug.Log("포톤 세션 연결 성공! 이제 대기실 UI를 활성화하세요.");
        // TODO: 여기서 4단계 기획인 '로비(대기실) 패널'을 켜는 로직이 들어갑니다.
    }
}