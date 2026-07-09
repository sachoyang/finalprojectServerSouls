# 🛠️ Sentry 크래시 리포트 — 팀원 세팅 가이드

배포 빌드에서 게임이 죽었을 때 원인을 받아보기 위해 [Sentry](https://sentry.io)를 붙였습니다.
**DSN은 이미 커밋되어 있어서 아무것도 안 해도 크래시는 수집됩니다.**

이 문서는 **빌드를 뽑는 사람**만 하면 되는 추가 세팅(= 심볼 업로드)을 설명합니다.

---

## 0. 요약

| 나는 누구인가 | 해야 할 일 |
|---|---|
| 그냥 개발만 함 | **없음.** git pull 하고 하던 대로 작업 |
| **빌드를 뽑아서 배포함** | 아래 1~3번 (본인 Auth Token 발급 후 입력) |

---

## 1. 왜 각자 넣어야 하나

Sentry 심볼 업로드에는 **Auth Token**이 필요합니다. 이건 DSN과 다릅니다.

- **DSN** — 이벤트를 보내기만 하는 공개 키. 빌드에 박혀서 유저 PC로 나갑니다. 커밋해도 안전.
  → `Assets/Resources/Sentry/SentryOptions.asset` (커밋됨)
- **Auth Token** — Sentry 조직에 **쓰기 권한**이 있는 진짜 비밀키. 유출되면 남이 우리 프로젝트를 건드릴 수 있음.
  → `Assets/Plugins/Sentry/SentryCliOptions.asset` (**`.gitignore`로 막아둠**)

그래서 이 파일은 git에 올라가지 않고, **각자 로컬에서 한 번 채워야** 합니다.

> ⚠️ **절대 이 파일을 강제로 커밋(`git add -f`)하지 마세요.** 토큰이 GitHub에 올라갑니다.
> 실수로 올렸다면 즉시 Sentry에서 해당 토큰을 **폐기(Revoke)** 하고 새로 발급하세요.

---

## 2. 심볼 업로드가 뭐고, 안 하면 어떻게 되나

우리 빌드는 IL2CPP라서, 크래시가 나면 스택트레이스가 **함수 이름 대신 메모리 주소 덩어리**로 남습니다.
빌드할 때 같이 생성되는 심볼 파일(`.pdb`)을 Sentry에 올려두면, Sentry가 그 주소를 원래 함수/파일/줄 번호로 되돌려줍니다.

- 심볼을 안 올리면 → 크래시는 수집되지만 **어디서 터졌는지 못 읽습니다.** 사실상 무용지물
- 심볼을 올리면 → `RewardManager.cs:363` 처럼 정확히 나옵니다

**빌드하는 사람이 토큰을 안 넣으면 빌드 자체는 정상적으로 됩니다.** 심볼만 조용히 안 올라갑니다.
그래서 배포 빌드를 뽑는 사람은 반드시 확인해야 합니다.

---

## 3. 세팅 방법

### 3-1. Auth Token 발급

1. https://sentry.io 로그인 (조직: **`peace-cdu`**)
2. 우측 상단 **Settings → Auth Tokens** (조직 설정 쪽입니다. 개인 User Auth Token 아님)
3. **Create New Token** → 이름은 아무거나 (`unity-symbol-upload-홍길동` 처럼 본인 식별되게)
4. 생성 직후 딱 한 번만 보여주는 `sntrys_...` 문자열을 복사

### 3-2. Unity에 입력

Unity 에디터에서 **Tools → Sentry → Editor** 탭:

| 항목 | 값 |
|---|---|
| Auth Token | 방금 복사한 `sntrys_...` |
| Organization Slug | `peace-cdu` |
| Project Name | `unity` |
| Upload Symbols | ✅ 체크 |
| Upload Development Symbols | ⬜ 해제 (개발 빌드 심볼까지 올리면 쿼터 낭비) |

> **Organization Slug를 빼먹는 실수가 가장 흔합니다.** 셋 중 하나라도 비면 업로드가 조용히 실패합니다.

### 3-3. 확인

빌드를 한 번 돌리고 Unity 콘솔에서 `sentry-cli` 로그를 확인하세요.
`Uploaded ... debug information files` 같은 줄이 보이면 성공입니다.
`error: An organization slug is required` 가 보이면 3-2를 다시 보세요.

---

## 4. 빌드 시 주의 — `crashpad_handler.exe`

Windows 네이티브 크래시(프로세스 즉사)는 Sentry가 crashpad라는 별도 프로세스로 잡습니다.
빌드하면 SDK가 **빌드 폴더에 `crashpad_handler.exe`를 자동으로 넣어줍니다.**

**배포용으로 압축할 때 이 파일을 빼먹으면 네이티브 크래시가 하나도 안 잡힙니다.**
`.exe`와 `_Data` 폴더만 챙기지 말고 폴더 통째로 압축하세요.

---

## 5. 동작 테스트

전투 씬의 **DebugGame** 오브젝트가 켜져 있으면 **F10**으로 일부러 예외를 던질 수 있습니다.
(F9는 Fusion 통계 토글이라 피했습니다)

> ✅ **이 테스트는 Auth Token이 없어도 됩니다.** git pull만 받으면 바로 F10을 눌러볼 수 있습니다.
> 이벤트를 Sentry로 보내는 건 DSN(커밋되어 있음)이고, Auth Token은 **빌드할 때 심볼을 올리는 용도**라
> 게임 실행 중에는 아예 쓰이지 않습니다.
> 게다가 에디터 플레이 모드는 IL2CPP가 아니라 Mono라서 스택트레이스에 함수 이름과 줄 번호가
> 처음부터 그대로 나옵니다. 심볼라이즈할 게 없습니다.

- **Sentry 대시보드**에 이슈가 뜨는지 확인
- **우리 서버 `crash_reports` 테이블**에도 들어오는지 확인
  → 단, 에디터에서는 서버 전송이 기본 꺼짐입니다.
     **Tools → Crash Reporter → 에디터에서도 서버로 전송**을 켜야 올라갑니다.
     (이 설정은 EditorPrefs라 본인 PC에만 저장되고 커밋되지 않습니다)

배포 전에는 `DebugHotkey` 인스펙터의 **Enable Crash Test Key** 체크를 꺼주세요.

---

## 6. 우리는 왜 Sentry랑 자체 서버를 둘 다 쓰나

| | Sentry | 자체 서버 (`CrashReporter.cs`) |
|---|---|---|
| C# 예외 | O | O |
| 네이티브 크래시 미니덤프 | O (crashpad) | X (발생 사실 + 직전 로그만) |
| 심볼라이즈된 스택 | O | X |
| 우리 DB에 남음 | X | O |

Sentry가 **메인**이고, 자체 서버는 우리 DB에 백업으로 남기는 용도입니다.
자체 리포터는 "지난 세션이 정상 종료되지 않았다"를 마커 파일로 감지해서 `Player-prev.log` 꼬리를 함께 보냅니다.

관련 문서: [`CRASH_REPORT_SERVER_SPEC.md`](CRASH_REPORT_SERVER_SPEC.md) (서버팀용 API 규격)
