
```markdown
# 🐉 ServerSouls Boss System Manual

본 문서는 멀티플레이(Photon Fusion) 환경에서 보스와 상호작용하고, 새로운 보스를 양산하기 위한 공통 규약 및 제작 가이드라인입니다.

---

## 👥 Part 1. Player & UI 프로그래머를 위한 상호작용 가이드

플레이어의 공격을 보스에게 전달하거나, 보스의 체력을 UI(보스 체력바)에 표기하기 위해 알아야 할 공통 변수와 함수들입니다. 모든 보스는 `NetworkBossCore`를 상속받으므로 아래의 기능이 100% 보장됩니다.

### 1. 보스에게 데미지 주기 (Player -> Boss)
플레이어의 무기나 스킬이 보스(Hitbox)에 적중했을 때, 보스의 최상위 객체에 있는 `RPC_TakeDamage`를 호출해야 합니다.
* **함수:** `public void RPC_TakeDamage(float damage, NetworkObject attacker = null)`
* **특징:** 데미지와 함께 타격자(`attacker`)를 넘겨주면, 보스가 내부적으로 10초간의 누적 딜량을 계산하여 딜 1위에게 어그로(타겟팅)를 변경합니다.
* **사용 예시:**
  ```csharp
  NetworkBossCore boss = hitCollider.GetComponentInParent<NetworkBossCore>();
  if (boss != null) {
      boss.RPC_TakeDamage(50f, Runner.LocalPlayer.GetComponent<NetworkObject>());
  }

```

### 2. UI 체력바 동기화 (Boss -> UI)

보스의 체력을 화면에 표시하려면 다음 두 변수를 읽어오면 됩니다.

* **`maxHP` (float):** 보스의 최대 체력 (인스펙터 세팅 값)
* **`CurrentHP` ([Networked] float):** 현재 체력. 값이 변할 때마다 UI를 갱신해 주면 됩니다.

### 3. 보스 상태 확인 (상태창, 디버그용)

보스가 현재 무엇을 하고 있는지, 누구를 보고 있는지 알아야 할 때 사용합니다.

* **`CurrentState` ([Networked] BossState):** 보스의 현재 상태
* `Sleep` (대기 중)
* `WakeUp` (최초 감지 시 기상 및 포효 중)
* `Idle` / `Walk` (전투 중 대기 및 추적)
* `ExecutingPattern` (패턴 공격 중)
* `Die` (사망)


* **`AggroTarget` ([Networked] NetworkObject):** 보스가 쫓아가거나 공격 대상으로 삼고 있는 플레이어 객체

> **💡 팀원 핵심 요약**
> * 보스 스크립트 이름이 무엇이든 무조건 `NetworkBossCore` 타입으로 가져다 쓰세요. (다형성)
> * 보스 사망 여부는 `CurrentState == BossState.Die` 인지만 확인하면 됩니다!
> 
> 

---

## ⚙️ Part 2. 보스 공통 변수 (NetworkBossCore)

새로운 보스를 기획하고 세팅할 때, 유니티 인스펙터에서 조작하게 될 공통 변수들입니다.

### [기본 및 기상 설정]

* `moveSpeed` / `rotationSpeed`: 걷기 속도 및 회전(추적) 속도
* `wakeUpRange`: 플레이어가 이 반경 안에 들어오면 보스가 잠(Sleep)에서 깨어납니다.
* `wakeUpAnimName`: 잠에서 깰 때 틀어줄 애니메이션 State 이름 (예: "Scream")
* `wakeUpDuration`: 포효 애니메이션 시간. 이 시간이 지나야本格적인 전투를 시작합니다.
* `aggroRefreshTime`: 딜미터기를 정산하여 어그로 대상을 바꾸는 주기 (기본 10초)
* `patternCooldown`: 하나의 공격 패턴이 끝난 후, 다음 공격을 시작하기 전까지의 대기 시간(쉬는 시간)

### [물리 및 충돌 (벽 미끄러짐)]

* `wallLayerMask`: 맵의 지형/벽 레이어 (보스가 벽을 뚫지 않게 함)
* `bodyRadius` / `castHeightOffset`: 보스의 물리 판정 크기 및 높이 (SphereCast용)

### [패턴 조립]

* `availablePatterns` (List): 보스가 사용할 수 있는 **패턴 SO(ScriptableObject)** 들을 꽂아 넣는 슬롯입니다. 사거리에 따라 알아서 가중치(Weight) 룰렛을 돌려 패턴을 시전합니다.

---

## 🏗️ Part 3. 새로운 보스 제작 가이드 (Step-by-Step)

코더가 아니더라도 기획자와 애니메이터가 인스펙터 조작만으로 보스를 만들 수 있도록 모듈화되어 있습니다. 아래 4단계를 따라 새로운 보스를 만드세요.

### Step 1. 시각화 스크립트 작성 (`IBossVisual` 구현)

보스의 모델링 프리팹에 붙을 비주얼 스크립트를 만듭니다.

* **반드시 `IBossVisual` 인터페이스를 상속**받아야 합니다.
* 서버에서 넘겨주는 해시(Hash)값을 받아 `anim.CrossFade()` 시켜주는 역할만 합니다.

### Step 2. 패턴 모듈 조립 (ScriptableObject) ⭐가장 중요⭐

유니티 Project 창 우클릭 -> `Create` -> `ServerSouls` -> `Boss Modules` -> `Boss Pattern`을 클릭하여 공격 패턴을 만듭니다.

1. **[주의] AI Conditions 설정:** `Min Range`와 `Max Range`를 반드시 설정하세요. (Max Range가 0이면 보스가 허공만 쳐다보고 절대 공격하지 않습니다!)
2. **Actions Sequence 설정:**
* `animationStateName`: 애니메이터에 있는 회색 네모 상자(State) 이름과 **대소문자/띄어쓰기까지 완벽하게 똑같이** 적어야 합니다.
* `duration`: 이 동작이 유지될 시간. (원본 클립 시간과 다르면 알아서 배속/슬로우 처리됩니다.)
* `moveOffset` & `moveCurve`: 공격 중 앞으로 튀어나갈 강제 돌진 거리를 꺾은선 그래프로 그립니다.



> **🚨 [필독] 애니메이터 세팅 주의사항 (블렌드 트리 금지)**
> 패턴 `Action` 안에 들어갈 애니메이션은 **반드시 '단일 클립(Single Clip)'으로 구성된 상태**여야 합니다.
> * 연속 공격: `[물기] -> [물기]` 처럼 동일한 액션을 연속으로 넣어도 멈추지 않고 0초부터 정상적으로 연타가 나갑니다.
> * 패턴 중 대기/걷기: `Locomotion` 같은 블렌드 트리를 패턴 리스트 안에 넣으면 다리가 굳어버립니다. 패턴 도중 1초간 쉬거나 슬금슬금 걷게 만들고 싶다면, 애니메이터에 순수한 `Pattern_Idle`, `Pattern_Walk` 단일 상태를 따로 만들어서 사용하세요.
> 
> 

### Step 3. 보스 메인 AI 스크립트 작성 (`NetworkBossCore` 상속)

보스만의 특수한 기믹(예: 체력이 절반 이하일 때 광폭화, 등 뒤에 있으면 즉시 꼬리치기)이 필요하다면 스크립트를 하나 만듭니다.

* **반드시 `NetworkBossCore`를 상속**받습니다. (참고: `DragonBoss.cs`)
* 아무 기믹이 없는 단순한 몬스터라면 기믹 코드를 비워두어도 부모 로직에 의해 완벽하게 전투를 수행합니다.
* 특수 조건에서 특정 패턴을 강제로 발동시키고 싶다면 `SelectPatternBasedOnRange(float distance)` 함수를 `override` 하여 가로채면 됩니다.

### Step 4. 프리팹 완성 및 연결

1. 최상위 오브젝트에 `NetworkObject`와 Step 3에서 만든 메인 보스 스크립트를 붙입니다.
2. 모델링(자식) 오브젝트에 `Animator`와 Step 1에서 만든 비주얼 스크립트를 붙입니다.
3. 메인 보스 스크립트의 **Available Patterns** 리스트에 Step 2에서 만든 패턴 SO 파일들을 드래그 앤 드롭으로 모두 넣어줍니다. 끝!

```

```