# 📢 [플레이어팀 TODO] 게임오버 → 로비 복귀 시스템 — 플레이어 쪽 남은 작업

게임오버(전멸) 시 **세션을 유지한 채(같은 인원 그대로)** 로비로 돌아가 처음부터 재시작하는 흐름이 구현되어 있습니다.
플레이어 쪽 코드는 약속대로 건드리지 않았고, 아래 작업들이 플레이어팀 몫으로 남아 있습니다.

## 현재 구조 (1분 요약)

- 전멸 → `GameOverView`(호스트만) → `runner.LoadScene("scLobbyMain")` → 게스트는 Fusion 씬 동기화로 자동 복귀
- 로비 씬이 로드되는 순간 **`GameProgressionManager.ResetRun()`이 모든 클라이언트에서 실행**됨 — "이번 런에서만 유효한 것"을 버리는 단일 청소 지점
- 플레이어 오브젝트는 전투씬 언로드 때 자동 디스폰됨 (로비에서는 재스폰 안 함, 정상)

---

## ✅ 작업 1. `PlayerSessionStore.ClearAll()` 추가 (필수, 가장 중요)

**안 하면: 로비에서 재시작할 때 이전 런의 체력/스태미나/어빌리티/보상 기록이 그대로 복원됩니다.**
static 저장소라 씬 전환으로 안 지워집니다.

`Player/Core/PlayerSessionStore.cs`에 추가:

```csharp
/// 게임오버 → 로비 복귀 시 런(Run) 단위 저장소 전체 초기화.
/// GameProgressionManager.ResetRun()이 호출한다.
public static void ClearAll()
{
    AbilityIdsByPlayer.Clear();
    SelectedRewardStageByPlayer.Clear();
    StatsByPlayer.Clear();
}
```

그다음 `Stage/GameProgressionManager.cs`의 `ResetRun()` 안에 있는
**`[플레이어팀 작업 필요]` 주석 블록에서 `// PlayerSessionStore.ClearAll();` 주석만 해제**하면 끝입니다.

---

## ✅ 작업 2. `PlayerStats.Despawned()`에서 `IsSpawnedReady` 내리기 (권장)

씬 전환 프레임에 이런 예외가 실제로 발생했습니다 (지금은 UI 쪽 호출부에 가드를 넣어 막아둔 상태):

```
InvalidOperationException: Error when accessing PlayerStats.IsDead.
Networked properties can only be accessed when Spawned() has been called.
```

원인: 디스폰된 뒤에도 파괴 전까지 한두 프레임 동안 다른 스크립트가 `[Networked]` 변수를 읽을 수 있는데,
`IsSpawnedReady`가 Spawned에서 true가 된 뒤 **내려가는 곳이 없어서** 가드 역할을 못 합니다.

`PlayerStats.cs`에 추가 (보스 쪽 `NetworkBossCore`에는 이미 동일 패턴 적용됨):

```csharp
public override void Despawned(NetworkRunner runner, bool hasState)
{
    // 씬 전환으로 디스폰된 뒤 UI/보스 AI가 [Networked] 변수에 접근하다 예외 던지는 것 방지
    IsSpawnedReady = false;
    // (기존에 Despawned가 이미 있다면 이 줄만 추가)
}
```

추가로 `PlayerRegistry.IsAlivePlayer()`는 `stats.IsDead`를 **아무 가드 없이** 읽습니다.
보스 AI(`NetworkBossCore`)가 매 틱 호출하는 함수라, 위 플래그와 함께 이렇게 보강해 주세요:

```csharp
public static bool IsAlivePlayer(NetworkObject networkObject)
{
    return TryGetStats(networkObject, out PlayerStats stats)
        && stats.IsSpawnedReady                          // ← 추가
        && stats.Object != null && stats.Object.IsValid  // ← 추가 (디스폰 후 접근 차단)
        && !stats.IsDead;
}
```

---

## ✅ 작업 3. 게스트 닉네임 null 확인 (로그인/DB 담당과 협의)

`BackendManager.CurrentNickname`은 **정식 로그인 응답에서만** 세팅됩니다.
디버그/간이 경로로 들어온 게스트는 null이라서, 로비 복귀 시 닉네임 등록 RPC가 null 직렬화로 터지며
**슬롯 UI 전체가 깨지는 버그**가 있었습니다. 지금은 "닉네임이 있을 때만 RPC 전송"으로 막아뒀지만,
닉네임 없는 유저는 슬롯에 **"Loading..."** 으로 표시됩니다.

확인/결정할 것:
- 정식 로그인 게스트도 복귀 후 null이 되는 케이스가 있는지 (있다면 `BackendManager` 쪽 세션 수명 문제)
- 디버그 유저는 `PlayerPrefs["CurrentNickname"]`(DebugQuickEntry가 채움)를 폴백으로 쓸지 여부

---

## 참고: 이미 처리되어 있어 작업 불필요한 것들

| 항목 | 처리 방식 |
|---|---|
| 마우스 커서 복구 | 플레이어팀이 만들어둔 `ThirdPersonCameraController.ForceCursorVisible` 훅 사용. 게임오버 화면에서 켜고, 로비 복귀(`ResetRun`) 때 내려서 다음 런에서 커서 잠금이 정상 동작 |
| 플레이어 스탯 UI 예외 | `ReviveScreenIndicator`, `HUDManager`에 접근 가드 추가됨 (PlayerStats 수정 없이 공개 프로퍼티만 읽음) |
| 플레이어 오브젝트 정리 | 씬 언로드 시 Fusion이 자동 디스폰, `PlayerRegistry`도 Despawned에서 정리됨 |
| 이펙트/BGM/레벨 초기화 | `ResetRun()` + `SceneBGMPlayer`가 처리 |

## 테스트 체크리스트 (작업 1·2 완료 후)

1. 호스트+게스트 접속 → 전투 → 어빌리티 획득 → 전멸 → 로비 복귀
2. 재시작 후 **체력/스태미나가 초기값**이고 **이전 어빌리티가 없는지** 확인 ← 작업 1 검증
3. 복귀 시점 콘솔에 `InvalidOperationException` 0건 확인 ← 작업 2 검증
4. 보상 화면이 새 런의 1층 보스 처치 후 정상적으로 다시 나오는지 (보상 선택 기록 초기화 검증)
