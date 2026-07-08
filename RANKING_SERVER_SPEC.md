# 🏆 랭킹(리더보드) 서버 구현 요청서 — for 서버팀 Claude

Unity 클라이언트에 **게임 클리어 랭킹** 기능을 붙였습니다. 이제 서버(PHP + MySQL)에
**기록 등록 / 조회** 엔드포인트 2개와 테이블 1개를 추가해 주세요.

> 이 문서는 클라이언트가 **실제로 보내고/파싱하는 필드 이름과 타입을 그대로** 적어둔 것입니다.
> **필드 이름·타입이 1글자라도 다르면 클라가 파싱을 실패**하니, 아래 스키마를 그대로 맞춰 주세요.

---

## 0. 기존 규격과 동일하게 (중요)

이 프로젝트의 기존 PHP API(`login.php`, `register.php`, `update_skills.php`,
`get_abilities.php`, `check_session.php`)와 **완전히 동일한 관례**를 따릅니다.

- 위치: `soulrush_api/` 폴더 (예: `http://<서버IP>:8080/soulrush_api/submit_ranking.php`)
- 등록(POST)은 `application/x-www-form-urlencoded` (Unity `WWWForm`)로 들어옵니다.
- 조회(GET)는 쿼리스트링(`?limit=10`)으로 들어옵니다.
- 응답은 **항상 JSON**, 최상위에 `status`(`"success"`/`"fail"`)와 `message` 포함.
- 인증은 기존과 동일하게 `login_id` + `session_token`을 함께 받습니다.
  (`check_session.php`가 쓰는 `users` 테이블의 `session_token`과 대조)

### ⚠️ JsonUtility 필수 주의사항
Unity의 `JsonUtility`는 매우 엄격합니다. 서버에서 JSON 만들 때:

1. **숫자 필드는 반드시 JSON 숫자로** 출력하세요. 문자열(`"123"`)로 주면 int 파싱 실패합니다.
   → PHP에서 `(int)`/`(float)` 캐스팅 후 `json_encode($data, JSON_NUMERIC_CHECK)` 권장.
2. 필드 이름은 아래 표의 **snake_case 그대로** (대소문자·언더스코어 정확히).
3. 응답에 클라가 모르는 필드가 더 있어도 무시되니 OK. 반대로 **빠지면** 그 필드는 0/빈문자열로 파싱됩니다(안전).

---

## 1. DB 테이블

```sql
CREATE TABLE IF NOT EXISTS `run_rankings` (
  `id`                 BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  `login_id`           VARCHAR(50)     NULL,                    -- 제출 계정 (게스트 등록 허용 시 NULL 가능)
  `nickname`           VARCHAR(50)     NOT NULL,                -- 표시 이름
  `clear_time_seconds` INT UNSIGNED    NOT NULL,               -- ★ 핵심 정렬 기준: 전투 소요 시간(초). 작을수록 상위
  `cleared_level`      INT UNSIGNED    NOT NULL DEFAULT 0,     -- 클리어한 최종 층(= maxLevel)

  -- ▼▼▼ [확장용] 지금은 0/빈값으로 들어오지만, 나중에 딜량·파티 표시가 붙습니다 ▼▼▼
  `total_damage`       INT UNSIGNED    NOT NULL DEFAULT 0,     -- 파티 총 딜량
  `party_size`         TINYINT UNSIGNED NOT NULL DEFAULT 1,    -- 파티 인원 수
  `players_json`       TEXT            NULL,                    -- 파티원 상세(JSON 배열 문자열). 아래 4번 참고
  -- ▲▲▲ 확장용 ▲▲▲

  `cleared_at`         DATETIME        NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  INDEX `idx_time`  (`clear_time_seconds` ASC),   -- 랭킹 정렬 가속
  INDEX `idx_login` (`login_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
```

정렬 규칙: **`clear_time_seconds` 오름차순(빠를수록 1등)**, 동률이면 `cleared_at` 빠른 순.

---

## 2. 엔드포인트 A — 기록 등록: `submit_ranking.php` (POST)

한 판 클리어 시 **방장(호스트)이 로그인 상태일 때만 1번** 호출합니다.
(게스트/게스트클라는 호출하지 않음 → 중복·오염 방지. 서버는 그냥 들어온 것만 저장하면 됩니다.)

### 클라이언트가 보내는 필드 (POST form)
| 필드 | 타입 | 지금 값 | 설명 |
|---|---|---|---|
| `login_id`           | string | 로그인 ID   | 인증용. 세션 검증에 사용 |
| `session_token`      | string | 세션 토큰   | 인증용. `users.session_token`과 대조 |
| `nickname`           | string | 닉네임      | 표시 이름 |
| `clear_time_seconds` | int    | 예: `183`  | 전투 소요 시간(초) |
| `cleared_level`      | int    | 예: `3`    | 클리어 최종 층 |
| `total_damage`       | int    | `0`        | (확장) 파티 총 딜량 |
| `party_size`         | int    | 예: `1~n`  | (확장) 파티 인원 |
| `players_json`       | string | `""`       | (확장) 파티원 상세 JSON |

### 서버가 돌려줄 JSON (클라 `RankingSubmitResponse`가 파싱)
```json
{
  "status": "success",
  "message": "기록이 등록되었습니다.",
  "rank": 4
}
```
- `rank` (int): 방금 등록한 기록이 전체에서 **몇 등인지**. 계산 부담되면 `0`으로 줘도 됩니다(클라는 그 경우 닉네임으로 내 줄을 하이라이트).
  - 계산법 예: `SELECT COUNT(*)+1 FROM run_rankings WHERE clear_time_seconds < :myTime`

### PHP 스켈레톤 (참고)
```php
<?php
header('Content-Type: application/json; charset=utf-8');
require 'db.php'; // 기존 프로젝트의 PDO/mysqli 연결 재사용

$login_id      = $_POST['login_id']           ?? '';
$session_token = $_POST['session_token']       ?? '';
$nickname      = $_POST['nickname']            ?? 'Unknown';
$clear_time    = (int)($_POST['clear_time_seconds'] ?? 0);
$cleared_level = (int)($_POST['cleared_level']  ?? 0);
$total_damage  = (int)($_POST['total_damage']   ?? 0);
$party_size    = (int)($_POST['party_size']     ?? 1);
$players_json  = $_POST['players_json']         ?? '';

// (선택) 세션 검증 — check_session.php와 동일 로직. 실패 시 거절하고 싶으면 여기서 처리.
// 유효하지 않아도 기록은 남기고 싶다면 login_id를 NULL로 저장하는 식으로 완화 가능.

// 기본 방어: 비정상 시간(0 이하, 말도 안 되게 큰 값) 컷
if ($clear_time <= 0 || $clear_time > 86400) {
    echo json_encode(['status'=>'fail','message'=>'invalid time','rank'=>0]);
    exit;
}

$stmt = $pdo->prepare(
  "INSERT INTO run_rankings
     (login_id, nickname, clear_time_seconds, cleared_level, total_damage, party_size, players_json)
   VALUES (:lid, :nick, :t, :lv, :dmg, :psz, :pj)");
$stmt->execute([
  ':lid'=>($login_id !== '' ? $login_id : null),
  ':nick'=>$nickname, ':t'=>$clear_time, ':lv'=>$cleared_level,
  ':dmg'=>$total_damage, ':psz'=>$party_size, ':pj'=>$players_json,
]);

$rankStmt = $pdo->prepare(
  "SELECT COUNT(*)+1 AS r FROM run_rankings WHERE clear_time_seconds < :t");
$rankStmt->execute([':t'=>$clear_time]);
$rank = (int)$rankStmt->fetch(PDO::FETCH_ASSOC)['r'];

echo json_encode([
  'status'=>'success', 'message'=>'기록이 등록되었습니다.', 'rank'=>$rank
], JSON_UNESCAPED_UNICODE | JSON_NUMERIC_CHECK);
```

---

## 3. 엔드포인트 B — 랭킹 조회: `get_rankings.php` (GET)

### 클라이언트가 보내는 쿼리스트링
| 파라미터 | 타입 | 예 | 설명 |
|---|---|---|---|
| `limit` | int | `?limit=10` | 상위 몇 개를 받을지. 없으면 10 기본 |

### 서버가 돌려줄 JSON (클라 `RankingListResponse` → `RankingEntry[]`가 파싱)
```json
{
  "status": "success",
  "message": "",
  "data": [
    {
      "rank": 1,
      "nickname": "peace",
      "clear_time_seconds": 152,
      "cleared_level": 3,
      "total_damage": 0,
      "players_json": "",
      "cleared_at": "2026-07-08 15:03:21"
    },
    {
      "rank": 2,
      "nickname": "hyunbin",
      "clear_time_seconds": 187,
      "cleared_level": 3,
      "total_damage": 0,
      "players_json": "",
      "cleared_at": "2026-07-08 14:41:02"
    }
  ]
}
```
- `data`는 **순위 오름차순**으로 이미 정렬해서 주세요. `rank`는 1부터 서버가 매겨 주세요.
- `cleared_at`은 문자열(표시용). 포맷 자유(`"YYYY-MM-DD HH:MM:SS"` 권장).

### PHP 스켈레톤 (참고)
```php
<?php
header('Content-Type: application/json; charset=utf-8');
require 'db.php';

$limit = (int)($_GET['limit'] ?? 10);
if ($limit <= 0 || $limit > 100) $limit = 10;

// MySQL 8+ : ROW_NUMBER() 로 rank 부여
$sql = "SELECT
          ROW_NUMBER() OVER (ORDER BY clear_time_seconds ASC, cleared_at ASC) AS rank,
          nickname, clear_time_seconds, cleared_level,
          total_damage, players_json,
          DATE_FORMAT(cleared_at, '%Y-%m-%d %H:%i:%s') AS cleared_at
        FROM run_rankings
        ORDER BY clear_time_seconds ASC, cleared_at ASC
        LIMIT :lim";
$stmt = $pdo->prepare($sql);
$stmt->bindValue(':lim', $limit, PDO::PARAM_INT);
$stmt->execute();
$rows = $stmt->fetchAll(PDO::FETCH_ASSOC);

// 숫자 필드는 반드시 숫자로! (JsonUtility 대응)
$data = array_map(function($r){
  return [
    'rank'               => (int)$r['rank'],
    'nickname'           => $r['nickname'],
    'clear_time_seconds' => (int)$r['clear_time_seconds'],
    'cleared_level'      => (int)$r['cleared_level'],
    'total_damage'       => (int)$r['total_damage'],
    'players_json'       => $r['players_json'] ?? '',
    'cleared_at'         => $r['cleared_at'],
  ];
}, $rows);

echo json_encode([
  'status'=>'success', 'message'=>'', 'data'=>$data
], JSON_UNESCAPED_UNICODE);
```
> MySQL 5.7 이하라 `ROW_NUMBER()`가 없으면, 정렬된 결과를 PHP `foreach`에서 `$i++`로 rank를 매겨도 됩니다.

---

## 4. (확장) `players_json` 규격 — 딜량/파티원 표시가 붙을 때

지금은 빈 문자열이지만, 나중에 딜량·파티원 이름이 들어오면 아래 형태의 **JSON 문자열**로 보냅니다.
서버는 그대로 저장(TEXT)만 하면 됩니다. 파싱/집계는 필요할 때 추가하면 됩니다.

```json
{"members":[{"nickname":"peace","damage":124500},{"nickname":"hyunbin","damage":98800}]}
```
(Unity 쪽 `PartyMember` / `PartyMemberList` 구조와 1:1 대응)

이때 `total_damage`에는 파티원 damage 합계가 들어올 예정이고, 조회 응답의 `total_damage`가 그대로
클라 표의 **딜량 컬럼**에 자동 표시됩니다(현재는 0이라 "-"로 나옴). 즉 **서버 컬럼은 이미 다 준비돼 있으니
클라가 값을 채우기 시작하면 추가 작업 없이 표시**됩니다.

향후 더 확장하고 싶으면 컬럼 추가 후보:
- `boss_name` / `stage_seed` : 어떤 보스/구성으로 클리어했는지
- `game_mode` : 솔로/협동 구분해 별도 랭킹
- 계정별 최고기록만 노출하려면 `login_id` 기준 `MIN(clear_time_seconds)` 서브쿼리

---

## 5. 요약 체크리스트 (서버팀)

- [ ] `run_rankings` 테이블 생성 (1번 SQL)
- [ ] `submit_ranking.php` : POST 저장 + `rank` 반환 (2번)
- [ ] `get_rankings.php` : GET 상위 `limit`개 정렬 반환 (3번)
- [ ] 숫자 필드는 JSON **숫자**로 출력 (`JSON_NUMERIC_CHECK` 등)
- [ ] 응답 최상위 `status` / `message` 포함
- [ ] (선택) `session_token` 검증 로직 재사용
- [ ] 두 파일을 기존 `soulrush_api/` 폴더에 배치

문의 대응 클라 파일:
`Assets/02. Scripts/DB/RankingManager.cs`, `RankingModels.cs`,
`Assets/02. Scripts/Ending/EndingSceneController.cs`
