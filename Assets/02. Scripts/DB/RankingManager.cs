using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

// ==========================================
// 🏆 RankingManager — 랭킹(리더보드) 서버 통신 담당
//  - BackendManager / AbilityManager 와 동일한 패턴(MonoSingleton + UnityWebRequest 코루틴).
//  - 서버 주소는 BackendManager.BASE_URL 을 그대로 재사용한다(자동 LAN/WAN 탐지 결과).
//  - 엔드포인트 2개:
//      submit_ranking.php  (POST)  : 한 판 클리어 기록 등록
//      get_rankings.php    (GET)   : 상위 랭킹 목록 조회
//  - 로그인 인증은 기존과 동일하게 login_id + session_token 을 함께 전송한다.
//  - "지금은 소요 시간만" 쓰지만, RunRecordPayload 에 딜량/파티 필드가 이미 있으므로
//    나중에 값만 채우면 서버/표에 그대로 확장된다.
// ==========================================
public class RankingManager : MonoSingleton<RankingManager>
{
    private const string SubmitEndpoint = "submit_ranking.php";
    private const string ListEndpoint = "get_rankings.php";

    // 서버 통신에 필요한 BackendManager 가 준비됐는지 확인한다.
    private bool TryGetBaseUrl(out string baseUrl)
    {
        baseUrl = null;
        if (!BackendManager.HasInstance)
        {
            Debug.LogWarning("[RankingManager] BackendManager 가 없어 랭킹 통신을 할 수 없습니다.");
            return false;
        }

        BackendManager backend = BackendManager.Instance;
        if (!backend.isServerReady || string.IsNullOrEmpty(backend.BASE_URL))
        {
            Debug.LogWarning("[RankingManager] 서버 주소(BASE_URL)가 아직 준비되지 않았습니다.");
            return false;
        }

        baseUrl = backend.BASE_URL;
        return true;
    }

    // ==========================================
    // [1] 기록 등록 (한 판 클리어 시 방장이 1번만 호출하는 것을 권장)
    // ==========================================
    public void SubmitRun(RunRecordPayload payload, Action<bool, RankingSubmitResponse> onComplete = null)
    {
        if (payload == null)
        {
            onComplete?.Invoke(false, null);
            return;
        }

        if (!TryGetBaseUrl(out string baseUrl))
        {
            onComplete?.Invoke(false, null);
            return;
        }

        StartCoroutine(SubmitRoutine(baseUrl, payload, onComplete));
    }

    private IEnumerator SubmitRoutine(string baseUrl, RunRecordPayload payload, Action<bool, RankingSubmitResponse> onComplete)
    {
        BackendManager backend = BackendManager.Instance;

        WWWForm form = new WWWForm();
        // 인증(기존 규격과 동일) — 서버가 세션을 검증하고, 없으면 무시하도록 확장 가능
        form.AddField("login_id", backend.CurrentLoginID ?? "");
        form.AddField("session_token", backend.CurrentSessionToken ?? "");

        // 기록 본문
        form.AddField("nickname", string.IsNullOrEmpty(payload.nickname) ? "Unknown" : payload.nickname);
        form.AddField("clear_time_seconds", payload.clear_time_seconds.ToString());
        form.AddField("cleared_level", payload.cleared_level.ToString());
        // 확장 필드(지금은 0/빈값이지만 규격을 미리 맞춰 전송)
        form.AddField("total_damage", payload.total_damage.ToString());
        form.AddField("party_size", payload.party_size.ToString());
        form.AddField("players_json", payload.players_json ?? "");

        using (UnityWebRequest www = UnityWebRequest.Post(baseUrl + SubmitEndpoint, form))
        {
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                string rawText = www.downloadHandler.text;
                Debug.Log("<color=cyan>[랭킹 등록 응답]</color> " + rawText);

                RankingSubmitResponse res = SafeParse<RankingSubmitResponse>(rawText);
                bool ok = res != null && res.status == "success";
                onComplete?.Invoke(ok, res);
            }
            else
            {
                Debug.LogWarning("[RankingManager] 기록 등록 네트워크 에러: " + www.error);
                onComplete?.Invoke(false, null);
            }
        }
    }

    // ==========================================
    // [2] 랭킹 목록 조회 (상위 limit개)
    // ==========================================
    public void FetchRankings(int limit, Action<bool, List<RankingEntry>> onComplete = null)
    {
        if (!TryGetBaseUrl(out string baseUrl))
        {
            onComplete?.Invoke(false, null);
            return;
        }

        StartCoroutine(FetchRoutine(baseUrl, limit, onComplete));
    }

    private IEnumerator FetchRoutine(string baseUrl, int limit, Action<bool, List<RankingEntry>> onComplete)
    {
        if (limit <= 0) limit = 10;
        string url = $"{baseUrl}{ListEndpoint}?limit={limit}";

        using (UnityWebRequest www = UnityWebRequest.Get(url))
        {
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                string rawText = www.downloadHandler.text;
                Debug.Log("<color=cyan>[랭킹 조회 응답]</color> " + rawText);

                RankingListResponse res = SafeParse<RankingListResponse>(rawText);
                if (res != null && res.status == "success")
                {
                    onComplete?.Invoke(true, res.data ?? new List<RankingEntry>());
                }
                else
                {
                    onComplete?.Invoke(false, null);
                }
            }
            else
            {
                Debug.LogWarning("[RankingManager] 랭킹 조회 네트워크 에러: " + www.error);
                onComplete?.Invoke(false, null);
            }
        }
    }

    // JsonUtility 파싱은 형식이 어긋나면 예외를 던지므로 안전하게 감싼다.
    private static T SafeParse<T>(string json) where T : class
    {
        if (string.IsNullOrEmpty(json)) return null;
        try
        {
            return JsonUtility.FromJson<T>(json);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[RankingManager] JSON 파싱 실패: {e.Message}\n원본: {json}");
            return null;
        }
    }
}
