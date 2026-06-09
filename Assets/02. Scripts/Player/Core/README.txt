Player/Core README

목적:
- 플레이어의 네트워크 이동, 기본 전투, 체력/스태미나, 사망/부활, 상태이상, 저장 데이터를 담당하는 핵심 스크립트 설명.
- UI는 이 컴포넌트들의 값을 직접 수정하지 않고, 표시용 데이터 함수로 읽기만 한다.


1. 전체 흐름

NetworkPlayerController
-> 입력을 받아 이동, 구르기, 점프, 기본 공격, 패링, 락온 처리
-> 애니메이션 액션 상태를 Networked 값으로 기록
-> 기본 공격이 맞으면 PlayerStats.TakeDamage() 또는 RegisterReviveHit() 호출

PlayerStats
-> 체력, 스태미나, 사망, 부활, 패시브 스탯 보너스 처리
-> 피해를 받으면 방어율과 PlayerStatusController 배율 적용
-> 피격/사망 결과를 NetworkPlayerController.NotifyDamageReaction()에 알림
-> HUD용 체력/스태미나 값은 GetHUDData()로 제공

PlayerStatusController
-> 플레이어 버프/디버프를 NetworkArray로 관리
-> 받는 피해, 주는 피해, 이동속도 배율 제공
-> UI용 상태 목록은 GetActiveStatusesForUI()로 제공

NetworkPlayerData
-> 획득한 abilityId 목록과 보상 선택 상태를 네트워크로 동기화

PlayerSessionStore
-> 씬 이동 중 유지할 임시 런타임 저장소


2. NetworkPlayerController.cs

역할:
- 플레이어 조작의 중심.
- Fusion NetworkBehaviour.
- FixedUpdateNetwork()에서 입력 기반 게임플레이를 처리한다.

주요 SerializeField:
- animator: 플레이어 Animator.
- viewCamera: 이동 방향 계산용 카메라.
- walkSpeed/runSpeed/rollSpeed/crawlSpeed: 이동 속도.
- rotationSpeed/lockOnRotationSpeed: 일반/락온 회전 속도.
- rollDuration: 구르기 지속 시간.
- shiftHoldThreshold: Shift 짧게 누름/길게 누름 구분.
- movementAcceleration/movementBraking: 수평 이동 보간.
- jumpImpulse/jumpAnimationLockDuration: 점프 힘과 점프 액션 잠금 시간.
- comboGraceSeconds: 기본 공격 콤보 유예 시간.
- comboInputBufferSeconds: 다음 콤보 입력을 미리 받을 수 있는 시간.
- attackHitRadius/attackHitDistance/attackHitHeight/attackTargetLayers: 기본 공격 판정 범위.
- basicAttackRevivePower: 죽은 플레이어를 기본 공격으로 도울 때 줄어드는 부활 게이지.
- lockOnTargetSelector/lockOnSearchRadius: 락온 대상 탐색.

주요 Networked 값:
- IsMovingNetworked, IsRunningNetworked: 원격 이동 애니메이션용.
- IsLockOnNetworked, LockOnMoveNetworked, LockOnPointPosition: 락온 상태.
- RollTimer, RollDirection: 구르기 진행 상태.
- LastAction, LastActionId, LastConsumedActionId, ActionSequence: 액션 애니메이션과 입력 중복 방지.
- BasicAttackComboUnlocked, BasicAttackComboIndex, BasicAttackComboExpiresAt: 기본 공격 콤보.
- ActionAnimationLocked, ActionLockType, ComboInputWindowOpen: 애니메이션 중 입력/이동 잠금.

외부에서 호출할 함수:
- UnlockBasicAttackCombo(): 패시브 보상으로 기본 공격 콤보 해금.
- BeginActionAnimation(PlayerActionLockType): Animator State 진입 시 호출.
- OpenComboInputWindow(): Animator State 중 콤보 입력 가능 구간에서 호출.
- EndActionAnimation(PlayerActionLockType): Animator State 종료 시 호출.
- NotifyDamageReaction(bool becameDead): PlayerStats가 피해/사망 판정 후 호출.
- NotifyRevived(): PlayerStats가 부활 완료 후 호출.
- IsInvincible()/EndInvincible(): 애니메이션 이벤트에서 무적 시작/종료 연결.

중요 내부 함수:
- FixedUpdateNetwork(): 입력 처리 중심.
- TryConsumeInputAction(int actionId): 같은 입력이 재시뮬레이션에서 중복 실행되지 않게 막음.
- StartAction(byte actionType, int actionId): 액션 상태를 네트워크 값으로 기록.
- TriggerAction(byte actionType): Animator에 실제 트리거/CrossFade 적용.
- ApplyAttackDamage(): 기본 공격 히트 판정.
- GetBasicAttackDamage(): 콤보 단계와 상태이상 공격 배율을 반영한 데미지.


3. PlayerStats.cs

역할:
- 체력, 스태미나, 방어율, 공격력 보너스, 피격 무적, 사망, 부활 게이지 관리.

외부에서 호출할 함수:
- HasStamina(float amount): 스태미나 충분 여부 확인.
- TryUseStamina(float amount): 스태미나 사용. 부족하면 false.
- TakeDamage(float damage): 일반 피해 요청.
- ApplyBossDamage(float damage): 보스 피해 요청.
- SetAnimationInvincible(bool): 애니메이션 기반 무적 설정.
- Heal(float amount): 살아있는 상태에서 회복.
- ForceHeal(float amount): 강제 회복.
- RestoreStamina(float amount): 스태미나 회복.
- RegisterReviveHit(NetworkObject helper, float revivePower): 죽은 플레이어 부활 게이지 감소.
- ApplyPassiveStatBonus(PlayerAbilityModule): 패시브 보너스 적용.
- RemovePassiveStatBonus(PlayerAbilityModule): 패시브 보너스 제거.
- GetHUDData(): HUD 표시용 PlayerHUDData 반환.

GetHUDData()가 주는 값:
- CurrentHealth
- MaxHealth
- CurrentStamina
- MaxStamina
- IsDead

중요 내부 함수:
- ApplyDamage(float damage): 방어율, 상태이상 배율, 피격 무적, 사망 판정.
- BeginReviveState(): 사망 시 부활 상태 진입.
- ApplyReviveHit(): revivePower만큼 ReviveProgress 감소.
- ReviveFully(): 부활 완료 후 NotifyRevived() 호출.
- UpdateReviveDecay(): 도움을 받지 않으면 ReviveProgress를 다시 채움.

UI 연동 규칙:
- UI는 CurrentHealth 같은 값을 직접 조합해도 읽기만 가능하지만, 앞으로는 GetHUDData()를 우선 사용한다.
- UI는 PlayerStats 상태를 직접 수정하지 않는다.


4. PlayerStatusController.cs

역할:
- 플레이어 상태이상 관리.
- StatusEffectData를 사용한다.
- ActiveStatuses를 NetworkArray로 동기화한다.

Inspector:
- statusDatabase에 사용할 StatusEffectData 에셋을 넣어야 ApplyStatus(statusId)가 동작한다.

외부에서 호출할 함수:
- ApplyStatus(int statusId): 상태 적용.
- RemoveStatus(int statusId): 상태 제거.
- GetIncomingDamageMultiplier(): 받는 피해 배율.
- GetOutgoingDamageMultiplier(): 주는 피해 배율.
- GetMoveSpeedMultiplier(): 이동속도 배율.
- GetActiveStatusesForUI(): UI 표시용 목록 반환.

주의:
- statusId 0은 상태 없음으로 쓰므로 사용하지 않는다.
- statusDatabase가 비어 있으면 상태를 찾지 못한다.


5. NetworkPlayerData.cs

역할:
- 획득한 스킬 abilityId와 보상 선택 상태를 네트워크로 저장한다.

외부에서 호출할 함수:
- RecordAbility(PlayerAbilityModule): 모듈의 AbilityId 저장.
- RecordAbilityId(string abilityId): 문자열 ID 직접 저장.
- GetAbilityId(int index): 저장된 abilityId 조회.
- HasAbilityId(string abilityId): 이미 보유했는지 확인.
- MarkRewardSelected(int bossStage): 해당 보스 층 보상 선택 완료 기록.

서버 기준 보유 스킬:
- SavedAbilityIds가 서버/호스트 기준 보유 목록이다.
- UI 표시용 아이콘/이름은 abilityId를 PlayerAbilityInventory.FindModuleById()로 다시 모듈에 매칭해서 읽는다.

DB 연결 시:
- SavedAbilityIds는 현재 string 기반이다.
- 숫자 ID로 바꾸려면 이 파일과 PlayerAbilityInventory/PlayerAbilityController도 함께 바꿔야 한다.


6. PlayerSessionStore.cs

역할:
- 씬 이동 중 유지되는 임시 메모리 저장소.
- 영구 DB가 아니다.

저장 항목:
- PlayerRef별 abilityId 목록.
- PlayerRef별 마지막 보상 선택 층.
- PlayerRef별 PlayerStats.SessionSnapshot.

DB 연동 후보:
- SaveAbility/GetAbilityIds
- MarkRewardSelected/HasSelectedReward
- SaveStats/TryGetStats


7. PlayerActionStateBehaviour.cs

역할:
- Animator State에 붙어서 액션 잠금과 콤보 입력 창을 연동한다.

호출 흐름:
- OnStateEnter -> NetworkPlayerController.BeginActionAnimation()
- OnStateUpdate -> NetworkPlayerController.OpenComboInputWindow()
- OnStateExit -> NetworkPlayerController.EndActionAnimation()


8. PlayerAttackRangeGizmo.cs

역할:
- Scene 뷰에서 기본 공격 범위를 보여주는 디버그용 Gizmo.
- 실제 게임 판정에는 영향이 없다.
