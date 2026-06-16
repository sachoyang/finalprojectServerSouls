아하, 스테이지 별 해금이나 획득 조건은 SO 데이터를 바탕으로 플레이어 파트 담당자분이 유연하게 관리하는 구조군요! 역할 분담이 확실해서 충돌 없이 작업하기 아주 좋은 방향입니다.

플레이어 담당자분이 필요할 때 언제든 꺼내 쓸 수 있도록, '영구 해금 트리거 사용법'을 마크다운(.md) 문서 형식으로 깔끔하게 정리해 드립니다. 이 내용을 그대로 복사해서 전달해 주시면 됩니다!

---

# 🔓 Soul Rush - 영구 스킬 해금(Unlock) 연동 가이드

본 문서에서는 게임 진행 중 특정 조건(상점 구매, 보스 클리어, 필드 습득 등)을 만족했을 때, 스킬을 계정에 영구적으로 해금하고 서버에 저장하는 방법을 안내합니다.

## 1. 핵심 호출 함수

어느 스크립트에서든 아래 함수 단 한 줄만 호출하면 로컬 갱신과 서버 저장이 동시에 완료됩니다.

```csharp
// module: 해금하고자 하는 스킬의 PlayerAbilityModule (SO) 데이터
AbilityManager.Instance.UnlockAbilityAndSync(PlayerAbilityModule module);

```

## 2. 작동 원리 (내부 처리)

이 함수가 호출되면 자동으로 다음 3단계가 백그라운드에서 즉시 처리됩니다.

1. **비트 갱신:** 유저의 현재 64비트 마스크 값에 해당 스킬의 `bitIndex`를 추가(OR 연산)하여 1로 켭니다.
2. **SO 업데이트:** 해당 스킬 모듈(SO)의 `includeInRewardPool` 값을 `true`로 덮어씌워, 그 즉시 인게임 보상 풀 룰렛에 등장할 수 있도록 만듭니다.
3. **서버 동기화:** `BackendManager`를 통해 변경된 최종 비트마스크를 서버 DB에 실시간으로 영구 저장합니다.

---

## 3. 상황별 적용 예시 (개발 참고용)

### 상황 A. 로비 상점이나 스킬 트리 UI에서 해금할 때

```csharp
public void OnClickPurchaseSkill(PlayerAbilityModule skillToBuy)
{
    // 1. 재화 확인 및 차감 로직 (예시)
    if (PlayerManager.Gold >= 1000)
    {
        PlayerManager.Gold -= 1000;

        // 2. 스킬 영구 해금 및 서버 동기화!
        AbilityManager.Instance.UnlockAbilityAndSync(skillToBuy);
        
        Debug.Log($"{skillToBuy.DisplayName} 스킬이 계정에 영구 해금되었습니다!");
    }
}

```

### 상황 B. 보스를 처치하거나 특정 스테이지를 클리어했을 때

```csharp
public void OnBossDefeated(int bossStage)
{
    // 예: 1스테이지 보스 클리어 시 "대시 베기" 스킬 영구 해금
    if (bossStage == 1)
    {
        // AbilityManager에서 ID로 모듈을 바로 찾아올 수도 있습니다.
        PlayerAbilityModule rewardSkill = AbilityManager.Instance.FindByAbilityId("Skill_DashSlash");
        
        if (rewardSkill != null)
        {
            AbilityManager.Instance.UnlockAbilityAndSync(rewardSkill);
        }
    }
}

```

### 상황 C. 필드에서 해금용 보물상자와 상호작용했을 때

```csharp
// 필드에 배치된 보물상자 스크립트
public PlayerAbilityModule containedSkill;

public void Interact()
{
    // 상자를 열면 즉시 영구 해금
    AbilityManager.Instance.UnlockAbilityAndSync(containedSkill);
    PlayOpenAnimation();
}

```

---

## 4. ⚠️ 주의 사항 및 팁

* **중복 호출 방지:** 내부적으로 비트 연산(`OR`)을 사용하므로, 이미 해금된 스킬에 이 함수를 실수로 여러 번 호출해도 에러가 터지거나 비트가 망가지지는 않습니다.
* **서버 트래픽 최적화:** 에러는 안 나지만 불필요한 서버 통신(POST 요청)이 발생할 수 있습니다. 상점 UI 등에서는 유저가 이미 보유한 스킬인지 사전에 검사하여 버튼을 비활성화해 두는 것을 권장합니다.

```csharp
// [안전장치] 이미 해금된 스킬인지 사전 검사하는 방법
if (!BackendManager.Instance.IsSkillUnlocked(skillToBuy.BitIndex))
{
    AbilityManager.Instance.UnlockAbilityAndSync(skillToBuy);
}
else
{
    Debug.Log("이미 보유한 스킬입니다!");
}

```