Revive README

목적:
- 플레이어 사망 후 부활 UI 표시 구조 설명.


1. 부활 게임플레이 흐름

1) PlayerStats.ApplyDamage()에서 체력이 0 이하가 된다.
2) PlayerStats.BeginReviveState()가 실행된다.
3) IsDead가 true가 되고 ReviveProgress, ReviveSegmentCount, ReviveGaugePerSegment가 설정된다.
4) ReviveUIManager가 죽은 PlayerStats를 찾는다.
5) ReviveScreenIndicator가 화면 위치와 부활 게이지를 표시한다.
6) 다른 플레이어가 죽은 플레이어를 공격하면 PlayerStats.RegisterReviveHit()가 호출된다.
7) ReviveProgress가 0 이하가 되면 PlayerStats.ReviveFully()로 부활한다.


2. ReviveUIManager.cs

역할:
- 죽은 플레이어를 스캔하고 ReviveScreenIndicator를 생성/삭제한다.

주요 필드:
- refreshInterval: PlayerStats 스캔 주기.
- headOffset: 플레이어 머리 위 표시 위치.
- screenOffset: 화면 좌표 오프셋.
- indicatorSize: UI 크기.
- hideLocalPlayerReviveUI: 로컬 플레이어가 죽었을 때 자기 화면에 표시할지 여부.
- backgroundSprite: 배경 이미지.
- fillSprites: 게이지 이미지.

내부 함수:
- RefreshIndicators(): 죽은 PlayerStats를 찾고 indicator 생성/삭제.
- ShouldHide(PlayerStats): 로컬 플레이어 표시 숨김 여부 판단.
- CreateIndicator(PlayerStats): ReviveScreenIndicator 생성 후 Bind().
- EnsureCanvasRoot(): ScreenSpaceOverlay Canvas 자동 생성.
- RemoveIndicator(PlayerStats): 특정 대상 indicator 제거.
- ClearIndicators(): 전체 제거.

외부에서 직접 호출할 일:
- 일반적으로 없다. 씬에 ReviveUIManager가 켜져 있으면 자동으로 동작한다.


3. ReviveScreenIndicator.cs

역할:
- 죽은 플레이어 한 명의 부활 게이지 UI.

외부에서 호출하는 함수:
- Bind(PlayerStats targetStats, RectTransform canvasRoot, Vector3 headOffset, Vector2 screenOffset, Vector2 indicatorSize, Sprite backgroundSprite, Sprite[] fillSprites)
- Tick()

동작:
- Bind()에서 PlayerStats.ReviveStateChanged 이벤트를 구독한다.
- Tick()에서 월드 위치를 화면 좌표로 바꾸고 게이지를 갱신한다.
- targetStats.IsDead가 false가 되면 숨기거나 제거 대상이 된다.

읽는 PlayerStats 값:
- ReviveProgress
- ReviveRequiredGauge
- ReviveSegmentCount
- IsDead


4. 부활 게이지를 줄이는 호출

기본 공격:
- NetworkPlayerController.ApplyAttackDamage()
- 죽은 PlayerStats를 맞추면 RegisterReviveHit(Object, basicAttackRevivePower)

스킬 히트박스:
- PlayerSkillHitbox.OnTriggerEnter()
- 죽은 PlayerStats를 맞추면 RegisterReviveHit(attacker, revivePower)

새 구조를 추가할 때:
- 부활 도움 판정을 만들려면 최종적으로 PlayerStats.RegisterReviveHit()를 호출하면 된다.
