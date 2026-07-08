using System;
using System.Collections.Generic;

// ==========================================
// 🏆 랭킹(리더보드) 통신용 데이터 규격
//  - 서버(PHP)와 주고받는 JSON 스키마를 한곳에 모아둔 파일.
//  - AbilityManager / BackendManager 와 동일하게 [Serializable] + JsonUtility 로 파싱한다.
//  - "지금은 소요 시간만" 쓰지만, 나중에 딜량/플레이어 이름/파티 정보가 붙을 수 있게
//    필드를 미리 넉넉히 잡아 두었다. (서버가 아직 안 채워주면 0/빈문자열로 파싱됨 → 안전)
// ==========================================

// 📤 [업로드] 한 판(런)을 클리어했을 때 서버로 보낼 기록 한 건.
//  EndingSceneController 가 이 값을 채워 RankingManager.SubmitRun 에 넘긴다.
[Serializable]
public class RunRecordPayload
{
    public string nickname;          // 대표 표시 이름 (지금은 방장 닉네임)
    public int clear_time_seconds;   // 전투 소요 시간(초) — 지금 랭킹의 핵심 값
    public int cleared_level;        // 몇 층까지 클리어했는지 (= maxLevel)

    // ▼▼▼ [확장용] 지금은 0/기본값으로 보낸다. 나중에 채우면 서버/랭킹에 자동 반영 ▼▼▼
    public int total_damage;         // 파티 총 딜량 (딜량 집계 붙으면 채움)
    public int party_size;           // 파티 인원 수
    public string players_json;      // 파티원 상세(JSON 배열 문자열). 아래 PartyMember 참고.
    // ▲▲▲ 확장용 ▲▲▲
}

// 📤 [확장용] 파티원 1명의 상세. players_json 안에 배열로 직렬화해서 보낸다.
//  지금은 안 써도 되지만, 딜량/이름 표시가 붙으면 이 구조를 그대로 쓰면 된다.
[Serializable]
public class PartyMember
{
    public string nickname;
    public int damage;
}

[Serializable]
public class PartyMemberList
{
    // JsonUtility 는 최상위 배열을 못 다루므로 래퍼로 감싼다.
    public List<PartyMember> members = new List<PartyMember>();
}

// 📥 [다운로드] 랭킹 표에 한 줄로 그려질 기록 한 건.
[Serializable]
public class RankingEntry
{
    public int rank;                 // 순위 (서버가 정렬해서 매겨줌)
    public string nickname;          // 표시 이름
    public int clear_time_seconds;   // 소요 시간(초)
    public int cleared_level;        // 클리어 층
    public int total_damage;         // 파티 총 딜량 (확장)
    public string players_json;      // 파티원 상세 (확장)
    public string cleared_at;        // 서버 기록 시각 (문자열, 표시용)
}

// 📥 get_rankings.php 응답 전체
[Serializable]
public class RankingListResponse
{
    public string status;            // "success" / "fail"
    public string message;
    public List<RankingEntry> data;  // 순위 오름차순으로 정렬된 목록
}

// 📥 submit_ranking.php 응답 (내 기록이 몇 등인지 알려주면 표시)
[Serializable]
public class RankingSubmitResponse
{
    public string status;            // "success" / "fail"
    public string message;
    public int rank;                 // 이번에 등록된 내 기록의 순위 (없으면 0)
}
