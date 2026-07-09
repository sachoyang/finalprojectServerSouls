using System;
using System.Collections.Generic;

// ==========================================
// 🏆 랭킹(리더보드) 통신용 데이터 규격  — "팀 단위" 랭킹
//  - 랭킹은 3인 협동 클리어에서만 등록/조회한다.
//  - 랭킹 1줄 = 한 "팀"의 클리어 기록. 정렬 기준은 팀의 전투 소요 시간.
//  - 그 팀 기록 안에 파티원 각각의 (이름, 딜량)이 members 리스트로 들어간다
//    → 표에서 팀을 펼치면 팀원별 딜량/이름을 볼 수 있다.
//  - 지금은 "소요 시간"만 실제 값이 채워진다. 딜량/이름 채우기는 나중에 담당자가
//    EndingSceneController.PartyMembersProvider 훅으로 주입한다(구조만 준비됨).
//  - AbilityManager / BackendManager 와 동일하게 [Serializable] + JsonUtility 로 파싱.
// ==========================================

// 팀원 1명의 기록 (이름 + 개인 딜량). 팀 기록 안에 리스트로 들어간다.
[Serializable]
public class PartyMember
{
    public string nickname;   // 팀원 표시 이름
    public int damage;        // 개인 딜량
    public int death_count;   // 죽어서 부활 대기 상태가 된 횟수
}

// members 배열을 폼 전송용 JSON 문자열로 감쌀 때 쓰는 래퍼.
//  (JsonUtility 는 최상위 배열을 직접 못 다루므로 항상 이 래퍼로 감싼다)
[Serializable]
public class PartyMemberList
{
    public List<PartyMember> members = new List<PartyMember>();
}

// 📤 [업로드] 한 팀이 클리어했을 때 서버로 보낼 기록 한 건.
//  방장(호스트)이 EndingSceneController 에서 채워 RankingManager.SubmitRun 에 넘긴다.
public class RunRecordPayload
{
    public string team_name;         // 팀 대표 이름 (지금은 방장 닉네임)
    public int clear_time_seconds;   // 팀 전투 소요 시간(초) — 랭킹 정렬 기준(작을수록 상위)
    public int cleared_level;        // 클리어한 최종 층(= maxLevel)

    public int party_size;           // 파티 인원 수 (랭킹은 3에서만 등록)
    public int total_damage;         // 팀 총 딜량 (members damage 합계, 지금은 0)
    public List<PartyMember> members = new List<PartyMember>(); // 팀원별 이름/딜량
}

// 📥 [다운로드] 랭킹 표에 한 팀(=한 줄 + 팀원 소줄들)로 그려질 기록.
[Serializable]
public class RankingEntry
{
    public int rank;                 // 순위 (서버가 소요 시간 오름차순으로 매김)
    public string team_name;         // 팀 대표 이름
    public int clear_time_seconds;   // 팀 소요 시간(초)
    public int cleared_level;        // 클리어 층
    public int total_damage;         // 팀 총 딜량

    // 팀 안의 팀원별 상세 (이름/딜량). 서버가 중첩 JSON 배열로 내려준다.
    //  → 표에서 이 팀을 펼치면 팀원별로 한 줄씩 보여준다. (지금은 비어 있어 팀 줄만 표시됨)
    public List<PartyMember> members;

    public string cleared_at;        // 서버 기록 시각 (문자열, 표시용)
}

// 📥 get_rankings.php 응답 전체
[Serializable]
public class RankingListResponse
{
    public string status;            // "success" / "fail"
    public string message;
    public List<RankingEntry> data;  // 순위 오름차순 팀 목록
}

// 📥 submit_ranking.php 응답 (우리 팀이 몇 등인지 알려주면 표시/하이라이트)
[Serializable]
public class RankingSubmitResponse
{
    public string status;            // "success" / "fail"
    public string message;
    public int rank;                 // 이번에 등록된 우리 팀의 순위 (없으면 0)
}
