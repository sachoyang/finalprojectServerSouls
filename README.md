# Soul Rush

> 소울라이크 전투에 로그라이크 성장을 얹은, 최대 3인 협동 보스러시

보스를 처치할 때마다 능력 카드를 하나 골라 강해지고, 포탈로 다음 층에 올라가 더 강해진 보스와 다시 싸웁니다.
한 판(런)이 끝나면 획득한 능력은 초기화되지만 계정에 해금된 스킬은 영구히 남고, 클리어 소요 시간은 팀 랭킹에 등록됩니다.

| | |
|---|---|
| **엔진** | Unity 2022.3.62f3 (LTS) · URP 14.0.12 |
| **실시간 네트워크** | Photon Fusion 2.0.12 (호스트 권한 모델, 최대 3인) |
| **백엔드** | Apache + PHP + MySQL (REST API) |
| **모니터링** | Sentry Unity 4.6.0 + 자체 CrashReporter |
| **플랫폼** | PC (Windows) |
| **개발 기간** | 2026.04 ~ 2026.08 (3인 팀) |

> 📸 **[플레이 영상 / 스크린샷]**

---

## 목차

- [게임 소개](#게임-소개)
- [주요 시스템](#주요-시스템)
- [아키텍처](#아키텍처)
- [시작하기](#시작하기)
- [리포지토리 구조](#리포지토리-구조)
- [씬 흐름](#씬-흐름)
- [문서](#문서)
- [팀](#팀)

---

## 게임 소개

**핵심 루프**

```
보스 처치 → 능력 카드 선택 → 포탈로 다음 층 이동 → 난이도 상승 → … → 클리어 → 팀 랭킹 등록
```

**전투** — 구르기 회피, 패링, 스태미나 관리를 기반으로 한 소울라이크 전투입니다. 모든 행동이 스태미나를 소모하므로 공격 버튼을 무작정 누를 수 없습니다.

**성장** — 보스를 잡을 때마다 능력 카드 후보 중 하나를 골라 등록하거나 레벨을 올립니다. 능력은 Active(직접 사용) / Passive(스탯 상승) / Utility(기능성) 세 타입으로 나뉩니다.

**협동** — 최대 3인이 함께 플레이합니다. 체력이 0이 되어도 즉시 패배가 아니라 다운 상태로 전환되고, 동료가 부활시킬 수 있습니다. 전원이 다운되면 패배입니다.

**진행** — 현재 빌드는 3스테이지 구성입니다. 보스가 3종이라 층수를 여기에 맞춰 둔 것이고, 층수 상한은 값 하나로 관리되므로 보스를 추가하는 만큼 그대로 늘어납니다.

---

## 주요 시스템

### 보스 — 로직과 연출의 분리

모든 보스는 `NetworkBossCore` 하나를 상속합니다. **상태(체력·페이즈·어그로·패턴 진행도)는 호스트만 확정**해 네트워크로 동기화하고, **연출은 `IBossVisual` 구현체가 각 클라이언트에서 로컬로 재생**합니다. 덕분에 인원수만큼 데미지가 중복 적용되지 않으면서도 애니메이션은 지연 없이 나옵니다.

```
Sleep → WakeUp → (Idle ↔ Walk ↔ ExecutingPattern) → PhaseTransition → … → Die
                            ↑                ↓
                          Groggy ← 그로기 게이지 최대
```

- **패턴은 ScriptableObject 조립** — 사거리로 후보를 거른 뒤 가중치로 추첨하므로 같은 보스라도 매번 순서가 다릅니다
- **2페이즈** — 체력 50% 이하가 되면 패턴 리스트 자체가 교체됩니다
- **그로기** — 누적 그로기 수치가 최대치에 도달하면 보스가 멈추고, 그동안 받는 데미지가 1.5배가 됩니다
- **어그로** — 누적 딜량 장부를 주기적으로 정산해 최다 딜러에게 어그로를 옮깁니다 (다운된 플레이어는 후보에서 제외)

> 새 보스 제작 방법은 [`Assets/02. Scripts/Boss/BossModule/README_BossSystem.md`](Assets/02.%20Scripts/Boss/BossModule/README_BossSystem.md)에 단계별로 정리되어 있습니다. **코드 수정 없이 인스펙터 조작만으로** 추가할 수 있습니다.

### 스킬 — 하이브리드 데이터

연출값과 밸런스 값을 저장 위치로 나눴습니다.

| | 저장 위치 | 예시 |
|---|---|---|
| **연출값** | Unity 로컬 에셋 | 애니메이션 · VFX · 사운드 · 히트박스 |
| **밸런스 값** | 서버 DB | 쿨타임 · 스태미나 소모 · 레벨별 배율 · 최대 레벨 |

밸런스 수치는 로그인 시점에 서버에서 내려받으므로, **수치를 고칠 때 클라이언트를 다시 빌드할 필요가 없습니다.** 계정별 스킬 해금 상태는 64비트 비트마스크 한 컬럼으로 관리합니다 (Active 1–19 / Passive 20–39 / Utility 40–60).

### 로그라이크 진행 — 3단 파이프라인

```
BossEncounterData          GameProgressionManager        BossArenaManager
(보스 프리팹 + 맵 + BGM)  →  (층수 관리 · 보스 추첨 · 이동)  →  (난이도 스케일링 · 스폰)
      데이터                        통제 (런 동안 유지)              현장 (맵마다 배치)
```

보스 추첨은 완전 랜덤이 아니라 셔플백 방식입니다. 풀의 보스가 전부 한 번씩 등장할 때까지 중복 없이 뽑고, 가방이 비면 다시 채웁니다.

### 최적화 · 안정성

- **이펙트 풀 + 셰이더 예열** — 이펙트 첫 렌더 시 발생하던 끊김을 로딩 시점으로 이동. 셰이더 변형 컴파일과 GPU 리소스 업로드를 미리 끝냅니다
- **셰이더 배리언트 정리** — 빌트인 → URP 마이그레이션 잔재를 정리해 **풀 빌드 3시간+ → 10분 미만**
- **맵 물리 최적화** — 메시 콜라이더를 박스 콜라이더로 교체 후 인접 콜라이더 병합, 라이트맵 사전 베이크
- **품질 등급 분리** — Low / Medium / High 별로 URP 렌더러를 나눠 그림자·후처리를 차등 적용
- **크래시 수집** — Sentry + 자체 `CrashReporter`. 프로세스가 즉사하는 **네이티브 크래시**도 마커를 남겨 다음 실행 때 보고합니다

> 자세한 내용과 유지보수 규칙은 [`Assets/02. Scripts/Optimization/README_Optimization.md`](Assets/02.%20Scripts/Optimization/README_Optimization.md)에 있습니다.

---

## 아키텍처

게임은 **성격이 다른 두 개의 서버 채널**과 통신합니다.

```
        ┌─────────────────────────────────────────┐
        │   Unity 클라이언트 (URP · 최대 3인)      │
        │   호스트 1 + 게스트 2                    │
        └──────────┬───────────────────┬──────────┘
                   │                   │
     실시간 · 휘발  │                   │  영속 · 요청/응답
                   ▼                   ▼
          ┌────────────────┐   ┌──────────────────────┐
          │  Photon Cloud  │   │  Apache + PHP (REST) │
          │  세션 릴레이    │   │      soulrush_api     │
          │  상태 동기화    │   └───────────┬──────────┘
          └────────────────┘               ▼
                                  ┌──────────────────┐      ┌────────────┐
                                  │      MySQL       │ ◀──▶ │ 관리용 웹   │
                                  │ 계정·스킬·랭킹·   │      │ (브라우저)  │
                                  │    크래시         │      └────────────┘
                                  └──────────────────┘
```

| | ☁ Photon Fusion | 🖥 웹 API (Apache · PHP · MySQL) |
|---|---|---|
| **목적** | 전투 순간을 서로 똑같이 보이게 | 다음 접속에도 남아야 하는 것 |
| **데이터** | 위치 · 애니메이션 · 보스 상태 · 데미지 | 계정 · 스킬 해금 · 랭킹 · 크래시 로그 |
| **방식** | 계속 연결된 양방향 통신 | 필요할 때 1회 요청 → 응답 |
| **빈도** | 초당 수십 번 | 로그인 · 보상 · 클리어 시점에만 |
| **보존** | 방을 나가면 소멸 | DB에 영구 저장 |

**설계 원칙 (전 시스템 공통)**

1. **권한 분리** — 체력·데미지·스킬 사용 같은 게임 결과는 호스트 또는 서버만 확정하고, 나머지 클라이언트는 요청만 보내고 화면 표현만 합니다
2. **데이터-표시 분리** — UI는 상태를 읽기만 하고 직접 수정하지 않습니다
3. **모듈화** — 보스와 스킬을 코드가 아니라 ScriptableObject 조립으로 확장합니다

### 백엔드 API

> ⚠️ **백엔드(PHP) 소스는 이 리포지토리에 포함되어 있지 않습니다.** 별도 서버에서 운영됩니다.
> 구현 규격은 [`docs/server_spec/`](docs/server_spec/)에 클라이언트가 실제로 파싱하는 필드명·타입 그대로 문서화되어 있습니다.

| 분류 | 엔드포인트 |
|---|---|
| 인증 · 세션 | `register.php` · `login.php` · `check_session.php` (15초 하트비트, 중복 로그인 차단) · `check_admin.php` |
| 스킬 | `get_abilities.php` · `update_skills.php` · `upload_ability.php` |
| 랭킹 · 크래시 | `submit_ranking.php` · `get_rankings.php` · `crash_report.php` |
| 관리용 웹 | `admin_hub` · `crash_viewer` · `ranking_viewer` · `log_viewer` |

**DB 테이블** — `users` · `abilities`(공통 1 + 타입별 6 = 7개) · `team_rankings` · `crash_reports`

---

## 시작하기

### 요구 사항

- **Unity 2022.3.62f3** (다른 LTS 버전에서는 URP 에셋·라이트맵 데이터가 재임포트될 수 있습니다)
- Photon Fusion App Id (Fusion 2.0 · [Photon 대시보드](https://dashboard.photonengine.com)에서 발급)
- (선택) 백엔드 서버 — 없어도 오프라인/디버그 진입으로 전투 씬 실행은 가능합니다

### 실행

```bash
git clone <repo-url>
cd finalprojectServerSouls
```

1. Unity Hub에서 프로젝트 폴더를 열고 **2022.3.62f3**로 실행합니다
2. 첫 임포트는 셰이더 컴파일 때문에 시간이 걸립니다
3. `Assets/01. Scenes/scIntro.unity`를 열고 Play (전투만 확인하려면 `scServer_stage.unity`에서 바로 시작해도 됩니다)

### 설정

**Photon** — `Assets/Photon/Fusion/Resources/PhotonAppSettings.asset`의 `AppIdFusion`에 본인의 App Id를 넣습니다.

**백엔드 주소** — `Assets/02. Scripts/DB/SoulRushApiSettings.asset`에서 LAN IP와 공인 IP를 설정합니다. 클라이언트는 실행 시 **LAN 주소를 1초 타임아웃으로 먼저 찔러보고**, 응답이 없으면 공인 IP로 자동 전환합니다.

**Sentry** — 설정 방법은 [`docs/server_spec/SENTRY_SETUP.md`](docs/server_spec/SENTRY_SETUP.md) 참고. 토큰이 없어도 게임은 정상 동작합니다.

### 개발용 단축키

| 키 | 기능 |
|---|---|
| `F5` | 보스 즉사 + 컷신 스킵 |
| `F6` | 보상 상자 리스폰 |
| `F7` | 보상 상자 위치로 텔레포트 |
| `F8` | 통로(Path) 씬 로드 |
| `F9` | Photon 네트워크 통계 표시 |
| `F10` | 크래시 리포트 테스트용 예외 발생 |

> `F5`~`F8`은 에디터에서는 자유롭게 쓸 수 있지만, **릴리즈 빌드에서는 서버가 `is_admin`으로 판정한 계정에서만** 동작합니다. 클라이언트가 스스로 권한을 판단하지 않습니다.

### 빌드 시 주의

- **새 씬은 반드시 Build Settings에 추가하세요.** 셰이더 스트리핑이 Build Settings의 씬을 기준으로 유지할 변형을 판단하기 때문에, 누락되면 런타임에 머티리얼이 깨집니다
- **Graphics → Shader Stripping의 Lightmap/Fog Modes를 Custom으로 바꾸지 마세요.** Automatic이 맞습니다 (이 값을 잘못 건드려 빌드가 3시간 이상으로 늘어난 전례가 있습니다)
- 셰이더 그래프를 새로 만들거나 에셋스토어에서 가져오면 Active Targets에 **Universal만** 남아 있는지 확인하세요

---

## 리포지토리 구조

```
Assets/
├─ 01. Scenes/          씬 (인트로·타이틀·로그인·로비·전투·통로·엔딩)
├─ 02. Scripts/         게임 코드 (173 파일 / 33,929줄)
│  ├─ Boss/             보스 코어·패턴 모듈·히트박스·보스 3종·에디터 툴
│  ├─ Combat/           전투 판정·락온·보상·부활·전투 결과
│  ├─ Cutscene/         컷신 (보스 등장·보상 상자·문 발차기)
│  ├─ DB/               백엔드 통신 (인증·스킬 카탈로그·랭킹) + 에디터 업로드/베이크 툴
│  ├─ Ending/           엔딩·전투 결과 씬
│  ├─ Optimization/     이펙트 풀·셰이더 예열·로딩 화면
│  ├─ Player/           플레이어 조작·스킬·카메라·스탯
│  ├─ Server/           Photon 방 관리·씬 전환·세션 가드·채팅
│  ├─ Stage/            로그라이크 진행 (인카운터 SO·층수 관리·맵 기믹)
│  ├─ System/           크래시 리포터·싱글톤 기반 클래스
│  └─ UI/               HUD·인벤토리·옵션·사운드 매니저
├─ 03. Materials/ · 04. Images/ · 05. Models/ · 06. Prefabs/
├─ 07. Animations/ · 08. Effects/ · 09. Sounds/
├─ 10. Renderings/      URP 에셋 (Low/Medium/High) · 셰이더 배리언트 컬렉션
└─ Photon/              Photon Fusion 2.0.12
docs/
├─ server_spec/         백엔드 구현 스펙 5종
├─ PROJECT_OVERVIEW_FOR_PPT.md
├─ PRESENTATION_SCRIPT.md
├─ PORTFOLIO_NOTION.md
└─ soul-rush-deck.html  웹 발표 덱
```

---

## 씬 흐름

```
scIntro ──▶ scTitle uicreate Main ──▶ scLogin ──(서버 인증)──▶ scLobbyMain
                                                                   │
                                                    (파티 구성)     ▼
                                                                scLoading
                                                (셰이더 예열 · 이펙트 풀 사전 생성)
                                                                   │
                    ┌──────────────────────────────────────────────┘
                    ▼
        보스 스테이지 ──(처치 · 보상 선택)──▶ scPathNew ──▶ 다음 층 ─┐
    (scServer_stage · Gothic_Stage …)                              │
                    ▲                                              │
                    └──────────────────────────────────────────────┘
                    │
         (마지막 층) └──▶ scPathLast ──▶ scEnding  (소요 시간 · 팀 랭킹)
```

보스 스테이지는 `BossEncounterData` SO가 지정한 맵 씬으로 이동합니다. 네트워크 씬 전환 중에는 `TransitionLoadingScreen`이 화면을 덮어 로딩을 가립니다.

---

## 문서

| 문서 | 내용 |
|---|---|
| [`Assets/02. Scripts/Boss/BossModule/README_BossSystem.md`](Assets/02.%20Scripts/Boss/BossModule/README_BossSystem.md) | 보스 상호작용 규약 + **새 보스 제작 가이드** |
| [`Assets/02. Scripts/Optimization/README_Optimization.md`](Assets/02.%20Scripts/Optimization/README_Optimization.md) | 이펙트 풀·셰이더 예열 사용법 + 빌드 최적화 이력과 규칙 |
| [`Assets/02. Scripts/DB/DBability_README.md`](Assets/02.%20Scripts/DB/DBability_README.md) | 스킬 모듈 데이터 구조 설계 배경 |
| [`Assets/02. Scripts/UI/Option/Sound/SM_README.md`](Assets/02.%20Scripts/UI/Option/Sound/SM_README.md) | SoundManager 사용법 |
| [`docs/server_spec/`](docs/server_spec/) | 백엔드 구현 스펙 — 스킬 DB · 랭킹 · 크래시 · Admin · Sentry |
| [`docs/PROJECT_OVERVIEW_FOR_PPT.md`](docs/PROJECT_OVERVIEW_FOR_PPT.md) | 프로젝트 전체 요약 |
| [`docs/README.md`](docs/README.md) | 웹 발표 덱(`soul-rush-deck.html`) 편집 안내 |
| [`AGENTS.md`](AGENTS.md) | AI 코딩 도구 작업 규칙 |

---

## 팀

3인 팀 프로젝트입니다.

| 팀원 | 역할 | 담당 |
|---|---|---|
| **양평화** | 팀장 | 보스 시스템 · 서버/백엔드 연동 · 로그라이크 진행 · 최적화 · 사운드 |
| **임현빈** | | 플레이어 조작 · 전투 판정 · 스킬 모듈 · 카메라 · 컷신 |
| **이주영** | | HUD · 인벤토리 · 로비/타이틀 · 옵션 UI |

---

## 향후 계획

- 플레이어 직업(무기) 추가
- 인간형 보스 패턴 추가
- 튜토리얼
- 보스 전투방 추가 — SO 등록과 맵 배치만으로 늘어나므로 층수도 함께 확장됩니다

---

## 라이선스

학습·포트폴리오 목적의 팀 프로젝트입니다. `Assets/` 아래의 서드파티 에셋(에셋스토어 모델·이펙트·사운드 등)은 각 배포처의 라이선스를 따릅니다.
