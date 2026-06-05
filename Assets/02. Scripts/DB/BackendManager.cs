using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System;

// 1. PHP에서 보내주는 JSON 규격에 맞춘 데이터 클래스들
[Serializable]
public class AuthResponse
{
    public string status;
    public string message;
    public string nickname;
    public long unlocked_skills; // 64개 스킬 비트마스크 (BIGINT)
    public string session_token; // 서버에서 보내줄 토큰 변수(마지막 세션 접근한 사람이 로그인)
}

[Serializable]
public class SkillResponse
{
    public string status;
    public string message;
    public long updated_skills;
}

public class BackendManager : MonoBehaviour
{
    // 싱글톤 세팅: 어디서든 BackendManager.Instance 로 접근 가능!
    public static BackendManager Instance { get; private set; }

    [Header("서버 IP 자동화 세팅")]
    [Tooltip("서버 PC의 내부 IP (예: 192.168.0.15)")]
    public string lanIP = "192.168.0.5"; 
    
    [Tooltip("서버 PC의 공인 IP (네이버 '내 아이피' 검색)")]
    public string publicIP = "1.233.102.137"; 

    // 더 이상 수동으로 적지 않고, 유니티가 알아서 채워넣을 변수
    public string BASE_URL { get; private set; }

    [HideInInspector]
    public bool isServerReady = false; // 서버 주소 세팅이 끝났는지 확인하는 플래그

    // 로그인한 유저의 정보를 캐싱해둘 변수
    public string CurrentLoginID { get; private set; }
    public string CurrentNickname { get; private set; }
    public long CurrentSkillsBitmask { get; private set; }

    public Action OnSessionExpired; // 세션이 만료되었을 때 호출될 이벤트
    private Coroutine _heartbeatCoroutine;

    // 로그인 성공 시 캐싱해둘 내 신분증(토큰)
    public string CurrentSessionToken { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // 씬이 넘어가도 파괴되지 않음
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        // 게임이 켜지면 무조건 서버 환경부터 자동 탐지 시작!
        StartCoroutine(AutoDetectServerRoutine());
    }

    private IEnumerator AutoDetectServerRoutine()
    {
        // 1. 내부망(LAN)에 접속이 되는지 가볍게 찔러보기 (log_viewer.php 활용)
        string testLanUrl = $"http://{lanIP}:8080/soulrush_api/log_viewer.php";
        
        using (UnityWebRequest ping = UnityWebRequest.Get(testLanUrl))
        {
            ping.timeout = 1; // 1초 이상 응답 없으면 바로 포기 (핵심)
            yield return ping.SendWebRequest();

            if (ping.result == UnityWebRequest.Result.Success)
            {
                // 성공하면 같은 공유기(LAN) 환경임!
                BASE_URL = $"http://{lanIP}:8080/soulrush_api/";
                Debug.Log("<color=green>[자동화] 🟢 내부망(LAN) 접속 확인! 로컬 IP로 세팅됩니다.</color>");
            }
            else
            {
                // 실패하면 집 밖(WAN) 환경임!
                BASE_URL = $"http://{publicIP}:8080/soulrush_api/";
                Debug.Log("<color=cyan>[자동화] 🔵 외부망(WAN) 접속 확인! 공인 IP로 세팅됩니다.</color>");
            }
        }
        
        isServerReady = true; // 세팅 완료! 이제 통신 가능
    }

    // ==========================================
    // [1] 회원가입 (Register)
    // ==========================================
    public void RegisterUser(string id, string pw, string nickname, Action<bool, string> onComplete = null)
    {
        StartCoroutine(RegisterRoutine(id, pw, nickname, onComplete));
    }

    private IEnumerator RegisterRoutine(string id, string pw, string nickname, Action<bool, string> onComplete)
    {
        WWWForm form = new WWWForm();
        form.AddField("login_id", id);
        form.AddField("password", pw);
        form.AddField("nickname", nickname);

        using (UnityWebRequest www = UnityWebRequest.Post(BASE_URL + "register.php", form))
        {
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                // 🔥 파싱하기 전에 서버가 보낸 원본 텍스트를 콘솔에 무조건 찍어봅니다.
                string rawText = www.downloadHandler.text;
                Debug.Log("<color=yellow>[서버 원본 응답]</color> " + rawText);

                AuthResponse res = JsonUtility.FromJson<AuthResponse>(www.downloadHandler.text);
                bool isSuccess = res.status == "success";
                onComplete?.Invoke(isSuccess, res.message);
            }
            else
            {
                onComplete?.Invoke(false, "네트워크 에러: " + www.error);
            }
        }
    }

    // ==========================================
    // [2] 로그인 (Login)
    // ==========================================
    public void LoginUser(string id, string pw, Action<bool, string> onComplete = null)
    {
        StartCoroutine(LoginRoutine(id, pw, onComplete));
    }

    private IEnumerator LoginRoutine(string id, string pw, Action<bool, string> onComplete)
    {
        WWWForm form = new WWWForm();
        form.AddField("login_id", id);
        form.AddField("password", pw);

        using (UnityWebRequest www = UnityWebRequest.Post(BASE_URL + "login.php", form))
        {
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                // 🔥 로그인할 때도 서버 원본 응답을 먼저 콘솔에 찍어봅니다!
                string rawText = www.downloadHandler.text;
                Debug.Log("<color=cyan>[로그인 서버 원본 응답]</color> " + rawText);

                AuthResponse res = JsonUtility.FromJson<AuthResponse>(www.downloadHandler.text);

                if (res.status == "success")
                {
                    // 로그인 성공 시 캐싱
                    CurrentLoginID = id;
                    CurrentNickname = res.nickname;
                    CurrentSkillsBitmask = res.unlocked_skills;
                    CurrentSessionToken = res.session_token;

                    // 로그인에 성공하면 10초마다 세션을 검사하는 심장 박동을 켭니다!
                    if (_heartbeatCoroutine != null) StopCoroutine(_heartbeatCoroutine);
                    _heartbeatCoroutine = StartCoroutine(HeartbeatRoutine());

                    onComplete?.Invoke(true, "로그인 완료! 환영합니다, " + res.nickname);
                }
                else
                {
                    onComplete?.Invoke(false, res.message);
                }
            }
            else
            {
                onComplete?.Invoke(false, "네트워크 에러: " + www.error);
            }
        }
    }

    // ==========================================
    // [3] 스킬 데이터 저장 (Update Skills)
    // ==========================================
    public void UpdateSkills(long newSkillsBitmask, Action<bool, string> onComplete = null)
    {
        if (string.IsNullOrEmpty(CurrentLoginID))
        {
            Debug.LogError("로그인된 유저가 없습니다!");
            return;
        }

        StartCoroutine(UpdateSkillsRoutine(CurrentLoginID, newSkillsBitmask, onComplete));
    }

    private IEnumerator UpdateSkillsRoutine(string id, long newSkillsBitmask, Action<bool, string> onComplete)
    {
        WWWForm form = new WWWForm();
        form.AddField("login_id", id);
        form.AddField("unlocked_skills", newSkillsBitmask.ToString());
        form.AddField("session_token", CurrentSessionToken);

        using (UnityWebRequest www = UnityWebRequest.Post(BASE_URL + "update_skills.php", form))
        {
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                // 서버 원본 응답을 먼저 콘솔에 찍어봅니다!
                string rawText = www.downloadHandler.text;
                Debug.Log("<color=cyan>[서버 원본 응답]</color> " + rawText);

                SkillResponse res = JsonUtility.FromJson<SkillResponse>(www.downloadHandler.text);

                if (res.status == "success")
                {
                    // 로컬 데이터 동기화
                    CurrentSkillsBitmask = res.updated_skills;
                    onComplete?.Invoke(true, res.message);
                }
                else
                {
                    onComplete?.Invoke(false, res.message);
                    // UI 스크립트 쪽에서 이 메시지를 받으면 강제로 타이틀 씬으로 돌려보내면 됨

                }
            }
            else
            {
                onComplete?.Invoke(false, "네트워크 에러: " + www.error);
            }
        }
    }

    // ==========================================
    // [4] 하트비트 (세션 유효성 검사)
    // ==========================================
    private IEnumerator HeartbeatRoutine()
    {
        // 로그인되어 있는 동안 무한 반복
        while (!string.IsNullOrEmpty(CurrentLoginID) && !string.IsNullOrEmpty(CurrentSessionToken))
        {
            // 15초 대기 (게임 서버 부하를 줄이려면 30초 정도로 늘려도 됩니다)
            yield return new WaitForSeconds(15.0f);

            WWWForm form = new WWWForm();
            form.AddField("login_id", CurrentLoginID);
            form.AddField("session_token", CurrentSessionToken);

            using (UnityWebRequest www = UnityWebRequest.Post(BASE_URL + "check_session.php", form))
            {
                // 통신 실패는 일시적인 인터넷 끊김일 수 있으니 무시하고,
                // 통신이 성공했는데 서버가 "invalid"를 뱉었을 때만 쫓아냅니다.
                yield return www.SendWebRequest();

                if (www.result == UnityWebRequest.Result.Success)
                {
                    // 간단하게 텍스트에 "invalid"가 포함되어 있는지 검사
                    if (www.downloadHandler.text.Contains("invalid"))
                    {
                        Debug.LogWarning("<color=red>[경고] 중복 접속 감지! 세션이 만료되었습니다.</color>");
                        
                        // 하트비트 정지 및 정보 초기화
                        CurrentLoginID = null;
                        CurrentSessionToken = null;
                        
                        // 강제 로그아웃 이벤트 발생!
                        OnSessionExpired?.Invoke(); 
                        break;
                    }
                }
            }
        }
    }
}