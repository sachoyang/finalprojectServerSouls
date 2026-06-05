###  [공지] 보스전 UI 연동 가이드 및 API 명세서

#### 1. 보스 UI 스크립트 기본 세팅 (싱글톤)

보스가 맵에 스폰되면 알아서 UI를 찾아가 정보를 넘겨주도록 설계했습니다.
보스 체력바 UI 스크립트(`BossHealthBarUI` 등)를 **싱글톤**으로 만드시고, 아래처럼 `RegisterBoss` 함수를 하나 뚫어주세요.

```csharp
public class BossHealthBarUI : MonoBehaviour
{
    public static BossHealthBarUI Instance { get; private set; }
    private NetworkBossCore targetBoss;

    private void Awake() { Instance = this; }

    // 보스가 스폰될 때 알아서 이 함수를 호출하며 자신의 데이터를 넘겨줍니다!
    public void RegisterBoss(NetworkBossCore boss)
    {
        targetBoss = boss;
        // 여기서 보스 이름 세팅 및 UI 패널 활성화 하시면 됩니다.
        // 예: nameText.text = targetBoss.bossName;
    }
}

```

#### 2. 보스 기본 정보 (매 프레임 Update에서 읽어갈 변수들)

`targetBoss`가 연결된 이후부터는 `Update()` 문에서 아래 변수들을 마음껏 가져다 쓰시면 됩니다. (전부 실시간 동기화됩니다.)

* **이름:** `targetBoss.bossName` (string)
* **체력:** `targetBoss.CurrentHP` / `targetBoss.maxHP` (float)
* **그로기 게이지:** `targetBoss.CurrentGroggy` / `targetBoss.maxGroggy` (float)
* **사망 여부 확인:** `if (targetBoss.CurrentState == BossState.Die)`

#### 3. 버프 / 디버프 (상태이상) UI 그리기

보스에게 현재 걸려있는 버프와 디버프 목록을 가져오는 전용 헬퍼 함수를 만들어 두었습니다.

* **호출 함수:** `targetBoss.GetActiveStatusesForUI()`
* **반환값:** `List<ActiveStatusUIInfo>` (현재 활성화된 상태이상 리스트)

리스트를 받아와서 `foreach`를 돌리며 아래 데이터들을 UI 프리팹에 꽂아 넣어주시면 됩니다.

**[ActiveStatusUIInfo 구조체 내부 데이터]**

* `Data.icon` (Sprite): 기획자가 SO에 등록한 도트 아이콘 이미지
* `Data.statusName` (string): 상태이상 이름 (예: "갑옷 파괴")
* `Data.description` (string): 툴팁용 설명 텍스트
* `Data.isDebuff` (bool): 디버프면 true, 버프면 false (빨간/파란 테두리 구분용)
* `RemainingTime` (float): **남은 시간 (초 단위).** 0이 되면 알아서 리스트에서 사라집니다.
* **🚨 [중요] 무한 지속 버프 처리:**
* `Data.isInfinite` (bool) 값을 꼭 먼저 확인해 주세요!
* 이 값이 `true`라면 기믹 파훼 전까지 영구 지속되는 버프입니다. 서버 시스템상 `RemainingTime`이 아주 큰 숫자(999999)로 넘어오므로, **`isInfinite == true`일 때는 타이머 텍스트를 아예 숨기거나 "∞" 기호 등으로 대체**해 주셔야 합니다.

---

나머지 1, 2, 4, 5번 항목은 지금 작성하신 내용 그대로 아주 깔끔하고 완벽합니다. 바로 공유해 주시면 되겠습니다!
#### 4. 툴팁에 표시할 '배율 수치(%)' 변환 공식

상태이상 구조체 안에는 실제 데미지 연산에 쓰이는 `Power` (float) 값이 들어있습니다.
(예: 1.2면 1.2배, 0.5면 반토막)

UI 툴팁 등에 퍼센트(%) 수치로 예쁘게 띄워주실 때는 아래 변환 공식을 사용해 주세요.

> `int displayPercent = Mathf.RoundToInt((Power - 1.0f) * 100f);`

* 적용 예시 1: `Power`가 1.5일 때 ➔ **+50** (%)
* 적용 예시 2: `Power`가 0.8일 때 ➔ **-20** (%)

#### 5. SO(Scriptable Object) 에셋 생성

* Project 창에서 우클릭 ➔ `Create` ➔ `ServerSouls` ➔ `Status Effect Data`를 눌러서 새로운 상태이상 도감을 직접 찍어내실 수 있습니다.(현재 Boss/Buff/SO에있음)
* 협의하여 UI에 필요한 아이콘과 텍스트를 마음껏 채워 넣어주시면 됩니다. (네트워크/서버 쪽에 따로 요청하실 필요 없이 독립적으로 작업 가능합니다!)