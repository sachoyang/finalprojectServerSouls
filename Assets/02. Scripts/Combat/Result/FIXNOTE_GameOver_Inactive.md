# 🐛 [수정 필요 메모] 죽음 → 로비 게임오버 화면이 안 뜸

작성: 2026-07-08 / 상태: **✅ 방법 B로 수정 완료 (GameOverView.cs만 변경, 프리팹/남 코드 미변경)**

## 적용된 수정 (방법 B)
`GameOverView.cs`에서:
- `Awake()`의 `gameObject.SetActive(false)` 제거 → 대신 `HidePanel()`(CanvasGroup alpha 0·blocksRaycasts false)로 숨김. 더 이상 스스로를 끄지 않으므로 Play의 SetActive가 유지됨.
- `Play()`는 `gameObject.SetActive(true)` 유지 + `ShowPanel()`(alpha 1) 추가.
- 헬퍼 `ShowPanel()`/`HidePanel()` 추가.
→ 프리팹의 GameOver 초기 활성 상태(0/1)와 무관하게 항상 정상 동작.

---
(아래는 원인 분석 기록)

## 증상
- 전원 사망 → `DEFEATED` 게임오버 화면이 **아예 안 뜸** (솔로/디버그에서도 재현. 예전엔 됐음)
- 콘솔 에러:
  ```
  Coroutine couldn't be started because the game object 'GameOver' is inactive!
  GameOverView:Play (string)      at GameOverView.cs:99
  GameOverView:PlayDefeat ()      at GameOverView.cs:74
  CombatResultManager:CompleteCombat  at CombatResultManager.cs:115
  CombatResultManager:CheckDefeat     at CombatResultManager.cs:95
  ```

## 근본 원인 (git diff로 확정)
항복 투표 커밋 **`90a4c7d` "항복 투표 추가. 충돌나는 사항 제거"** 가
`06. Prefabs/UI/InGameHUD1.prefab` 의 **`GameOver` 오브젝트 기본 활성 상태를
`m_IsActive: 1` → `0`** 으로 바꿈. 이게 `GameOverView`의 자기-비활성화 패턴과 충돌한다.

- `GameOverView.Awake()` (line 60): `gameObject.SetActive(false)`
  → "처음엔 켜진 채 시작해서 Awake에서 스스로 끈다"를 전제로 한 코드.
- **예전(m_IsActive:1):** 씬 로드 때 Awake가 즉시 실행돼 스스로 꺼짐.
  이후 `Play()`의 `SetActive(true)`가 정상 → `StartCoroutine` OK.
- **지금(m_IsActive:0):** Awake가 아직 안 돎. `Play()`가 line 87에서 `SetActive(true)`
  하는 **그 순간 Awake가 처음 실행**되고 line 60이 **다시 SetActive(false)** 로 꺼버림
  → line 99 `StartCoroutine` 시 오브젝트가 비활성 → 에러.

## 수정 방법 (택1)

### 방법 A — 프리팹 원복 (변경 최소)
`InGameHUD1.prefab` 의 `GameOver` 오브젝트 `m_IsActive` 를 `0` → `1` 로 복구.
(씬 `01. Scenes/hud 1.unity` 의 InGameHUD1 인스턴스에 active override가 있으면 함께 확인)
- 장점: 한 줄. 항복 커밋의 의도치 않은 부작용 원복.
- 단점: "Awake가 스스로 끈다" 프래질한 패턴에 계속 의존. 누군가 또 프리팹에서 끄면 재발.

### 방법 B — 코드 견고화 (권장, GameOverView.cs = peace 본인 파일)
`Awake()`의 `gameObject.SetActive(false)`(line 60) 의존을 없애고,
오브젝트는 항상 켜둔 채 **CanvasGroup.alpha=0 + blocksRaycasts=false** 로만 숨긴다.
`Play()`에서 alpha를 올려 표시. 그러면 프리팹 초기 활성 상태와 무관하게
`StartCoroutine`이 항상 성공.
- 장점: 남 코드/프리팹 안 건드림. 항복 UI 작업과 재충돌 없음. 근본 해결.
- 관련 수정 지점: Awake(line 40~61), Play(line 82~100), SetAlpha/SetInputEnabled.

## 참고: 문제 아님 (확인 완료)
- `CombatResultManager` 죽음 감지 로직 정상, 씬 배선(gameOverView 연결) 정상.
- 세션 유지 방식 로비 복귀(host `runner.LoadScene`)도 코드 정상.
  단 멀티에서는 **호스트가 키/버튼을 눌러야** 전원 이동 (게스트는 "Waiting for Host").
