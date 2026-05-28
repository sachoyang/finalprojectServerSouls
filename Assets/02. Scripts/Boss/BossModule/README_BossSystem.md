---

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

* **`CurrentState` ([Networked] BossState):** 보스의 현재 상태 (Sleep, Idle, Walk, ExecutingPattern, Die)
* **`AggroTarget` ([Networked] NetworkObject):** 보스가 현재 쫓아가거나 공격 대상으로 삼고 있는 플레이어 객체

"보스 스크립트 이름이 뭐든(DragonBoss든 KnightBoss든) 무조건 NetworkBossCore 타입으로 변수를 선언해서 가져다 쓰세요!"

"체력은 CurrentHP 변수를 읽고, 죽었는지 살았는지는 CurrentState == BossState.Die 인지만 확인하세요!" 이렇게 두 가지만 전달해 주시면, 다른 팀원들이 보스 내부의 복잡한 딜미터기나 패턴 룰렛 로직을 전혀 몰라도 UI와 시스템을 척척 붙일 수 있게 됩니다!

---

## ⚙️ Part 2. 보스 공통 변수 (NetworkBossCore)

새로운 보스를 기획하고 세팅할 때, 유니티 인스펙터에서 조작하게 될 공통 변수들입니다.

### [기본 설정]

* `moveSpeed` / `rotationSpeed`: 걷기 속도 및 회전(추적) 속도
* `wakeUpRange`: 플레이어가 이 반경 안에 들어오면 보스가 잠(Sleep)에서 깨어납니다.
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

보스의 모델링 프리팹에 붙을 비주얼 스크립트를 만듭니다. 이 스크립트는 **반드시 `IBossVisual` 인터페이스를 상속**받아야 합니다.

* 서버에서 넘겨주는 애니메이션 State 이름(string)을 받아 `anim.CrossFade()` 시켜주는 역할만 합니다.
* *참고 스크립트:* `DragonVisual.cs`

### Step 2. 패턴 모듈 조립 (ScriptableObject)

코딩 없이 유니티 Project 창에서 공격 패턴을 만듭니다.

1. 우클릭 -> `Create` -> `ServerSouls` -> `Boss Modules` -> `Boss Pattern` 클릭
2. 생성된 에셋의 이름을 정합니다. (예: `Pattern_Knight_3Combo`)
3. 인스펙터에서 **조건(Min/Max Range, Weight)** 을 설정합니다.
4. **Actions Sequence** 리스트에 `+` 버튼을 눌러 동작들을 추가합니다.
* `animationStateName`: 애니메이터에 있는 State 이름 (예: "Slash")
* `duration`: 이 동작이 유지될 시간 (엇박자 조절용)
* `moveOffset` & `moveCurve`: 이 동작을 하는 동안 앞으로 얼마나 튀어나갈지 꺾은선 그래프로 그립니다. (강제 돌진 연출용)



### Step 3. 보스 메인 AI 스크립트 작성 (`NetworkBossCore` 상속)

보스만의 특수한 기믹(예: 체력이 절반 이하일 때 광폭화, 등 뒤에 있으면 무조건 꼬리치기)이 필요하다면 스크립트를 하나 만듭니다.

* **반드시 `NetworkBossCore`를 상속**받습니다.
* 아무 기믹이 없는 단순한 몬스터라면 그냥 빈 스크립트여도 알아서 잘 싸웁니다.
* 특수 조건에서 특정 패턴을 강제로 발동시키고 싶다면 `SelectPatternBasedOnRange(float distance)` 함수를 `override` 하여 가로채면 됩니다.
* *참고 스크립트:* `DragonBoss.cs`

### Step 4. 프리팹 완성 및 연결

1. 보스 프리팹을 유니티 씬에 올립니다.
2. 최상위 오브젝트에 `NetworkObject`와 Step 3에서 만든 `(이름)Boss.cs`를 붙입니다.
3. 모델링(자식) 오브젝트에 `Animator`와 Step 1에서 만든 `(이름)Visual.cs`를 붙입니다.
4. 최상위 보스 스크립트의 **Available Patterns** 리스트에 Step 2에서 만든 패턴 SO 파일들을 드래그 앤 드롭으로 모두 넣어줍니다.
5. 저장 후 실행하면 보스가 완성됩니다!