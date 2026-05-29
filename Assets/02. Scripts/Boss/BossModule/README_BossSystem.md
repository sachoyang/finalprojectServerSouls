# 🐉 ServerSouls Boss System Manual (Roguelike Update)

본 문서는 멀티플레이(Photon Fusion) 환경에서 보스와 상호작용하고, 새로운 보스를 양산하며, 로그라이크식 스테이지 이동을 관리하기 위한 공통 규약 및 가이드라인입니다.

---

## 👥 Part 1. Player & UI 프로그래머를 위한 상호작용 가이드

플레이어의 공격을 보스에게 전달하거나, 보스의 체력을 UI(보스 체력바)에 표기하기 위해 알아야 할 기능입니다. 모든 보스는 `NetworkBossCore`를 상속받으므로 아래 기능이 100% 보장됩니다.

### 1. 보스에게 데미지 주기 (Player -> Boss)
플레이어의 무기나 스킬이 보스(Hitbox)에 적중했을 때, 보스의 최상위 객체에 있는 `RPC_TakeDamage`를 호출해야 합니다.
* **함수:** `public void RPC_TakeDamage(float damage, NetworkObject attacker = null)`
* **특징:** 데미지와 함께 타격자(`attacker`)를 넘겨주면, 보스가 10초간 누적 딜량을 계산하여 딜 1위에게 어그로를 변경합니다. 체력이 50% 이하로 떨어지면 알아서 2페이즈(광폭화)로 전환됩니다.

### 2. UI 체력바 동기화 (Boss -> UI)
보스의 체력을 화면에 표시하려면 다음 두 변수를 읽어오면 됩니다. (난이도에 따른 뻥튀기가 이미 적용된 값입니다.)
* **`maxHP` (float):** 보스의 최대 체력
* **`CurrentHP` ([Networked] float):** 현재 체력

### 3. 보스 상태 확인 (상태창, 디버그용)
* **`CurrentState` ([Networked] BossState):** * `Sleep` (맵 중앙에서 대기/수면 중)
  * `WakeUp` (최초 감지 시 기상 및 포효 중)
  * `PhaseTransition` **[NEW]** (체력 50% 이하 도달 시 2페이즈 변신/무적 연출 중)
  * `Idle` / `Walk` (전투 중 대기 및 추적)
  * `ExecutingPattern` (패턴 공격 중)
  * `Die` (사망)

> **💡 팀원 핵심 요약**
> * 보스 스크립트 이름이 무엇이든 무조건 `NetworkBossCore` 타입으로 가져다 쓰세요. (다형성)
> * 보스 사망 여부는 `CurrentState == BossState.Die` 인지만 확인하면 됩니다!

---

## ⚙️ Part 2. 로그라이크 스테이지 매니저 시스템 (Flow)

이제 씬 로딩과 보스 난이도 조절은 3단계 매니저 시스템이 자동으로 처리합니다.

1. **데이터 (`BossEncounterData` SO):** 보스 프리팹 원본과 그 보스가 등장할 전용 '맵(Scene) 이름'을 들고 있는 순수 데이터 캡슐입니다.
2. **통제실 (`GameProgressionManager`):** 로비에 존재하며 절대 파괴되지 않는(DontDestroyOnLoad) 글로벌 매니저입니다. 층수(Level)를 관리하고, 다음 층으로 갈 때 보스 데이터를 랜덤으로 뽑아 해당 맵으로 플레이어들을 강제 이동시킵니다.
3. **현장 소장 (`BossArenaManager`):** 각 보스 맵에 배치되어 있습니다. 씬 로딩이 끝나면 통제실에게 현재 층수를 물어보고, 층수에 비례해 보스의 체력/데미지를 뻥튀기한 뒤 맵 중앙에 보스를 소환합니다. (5층 이상이면 2페이즈도 해금해 줍니다.)

---

## 🏗️ Part 3. 새로운 보스 제작 가이드 (Step-by-Step)

코더가 아니더라도 기획자와 애니메이터가 인스펙터 조작만으로 보스를 만들 수 있습니다. 

### Step 1. 시각화 스크립트 작성 (`IBossVisual` 구현)
보스의 모델링 프리팹에 붙을 비주얼 스크립트를 만듭니다.
* **반드시 `IBossVisual` 인터페이스를 상속**받아야 합니다. 서버의 명령(Hash)을 받아 `anim.CrossFade` 시켜주는 역할만 합니다.

### Step 2. 패턴 모듈 조립 (ScriptableObject) ⭐가장 중요⭐
`Create` -> `ServerSouls` -> `Boss Modules` -> `Boss Pattern`을 클릭하여 공격 패턴을 만듭니다.
* **`Min/Max Range`:** 발동 사거리 (Max가 0이면 절대 공격하지 않으니 주의!)
* **`Actions Sequence`:** * `animationStateName`: 애니메이터의 State 이름과 **완벽히 똑같이** 작성.
  * `duration`: 동작 유지 시간 (원본 클립과 다르면 자동 배속 처리).
  * `moveOffset` & `moveCurve`: 돌진 거리를 그래프로 깎습니다.

> **🚨 [필독] 애니메이터 세팅 주의사항 (블렌드 트리 금지)**
> 패턴 `Action` 안에는 **반드시 '단일 클립(Single Clip)' 상태**만 넣어야 합니다. `Locomotion` 같은 블렌드 트리를 넣으면 다리가 굳어버립니다. 패턴 중 대기나 걷기가 필요하면 `Pattern_Idle` 같은 단일 클립을 따로 만들어 연결하세요.

### Step 3. 메인 AI 조립 (`NetworkBossCore`)
최상위 오브젝트에 `NetworkObject`와 메인 스크립트(예: `DragonBoss.cs`)를 붙입니다.
* **`Phase 1 Patterns`:** 1페이즈용 패턴 SO들을 넣습니다.
* **`Phase 2 Patterns`:** 체력이 50% 이하가 되었을 때 사용할 광폭화 패턴 SO들을 넣습니다.
*(보스만의 특수 기믹이 필요하다면 `NetworkBossCore`를 상속받는 새 스크립트를 작성하여 `SelectPatternBasedOnRange`를 override 하세요.)*

### Step 4. 데이터 등록 및 씬 배치 (최종 마무리)
1. **데이터 생성:** `Create` -> `ServerSouls` -> `Boss Encounter Data`를 만들어 보스 프리팹과 등장할 씬 이름(예: `scVolcano`)을 적습니다.
2. **통제실 등록:** 로비 씬의 `GameProgressionManager`에 있는 **Boss Pool** 리스트에 방금 만든 데이터를 넣습니다.
3. **현장 소장 세팅:** 전투가 벌어질 맵 씬(예: `scVolcano`)을 열고, `BossArenaManager`의 **Boss Spawn Point**에 보스가 소환될 위치를 지정해 주면 완성입니다!