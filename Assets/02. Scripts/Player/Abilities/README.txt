Player/Abilities README

목적:
- 플레이어 스킬, 패시브, 액티브 슬롯, 보상 획득, 스킬 히트박스 구조 설명.
- UI는 스킬 슬롯 내부 구조를 직접 조합하지 않고 GetSkillSlotUIData()가 만든 표시용 데이터를 사용한다.


1. 전체 흐름

보상 획득:
RewardManager
-> PlayerAbilityRewardController.OfferBossReward()
-> PlayerAbilityInventory.GenerateRewardOptions()
-> RewardSelectView에서 선택
-> PlayerAbilityRewardController.SelectPendingOption()
-> PlayerAbilityInventory.SelectRewardOption()
-> PlayerAbilityExecutor.EquipModule()
-> NetworkPlayerData.RecordAbility()

액티브 스킬 사용:
PlayerAbilityController.Update()
-> 키 입력 감지
-> Client면 RPC_RequestActivateAbility()
-> StateAuthority에서 TryActivateAbility()
-> PlayerAbilityExecutor.Activate()
-> slot.StartCooldown(currentTime)
-> RPC_PlayAbilityPresentation(abilityId, cooldownEndTime)
-> 모든 클라이언트에서 ApplyCooldownToLocalSlot()
-> HUD는 GetSkillSlotUIData(currentTime)로 남은 쿨타임 표시


2. PlayerAbilityModule.cs

역할:
- 스킬/보상 하나를 정의하는 ScriptableObject.

주요 필드:
- abilityId: DB/네트워크/RPC에서 쓰는 고유 문자열 키.
- displayName: UI 표시 이름.
- description: UI 설명.
- icon: UI 아이콘.
- abilityType: Passive 또는 Active.
- minBossStage/maxBossStage: 보상 후보 등장 층.
- includeInRewardPool: 보상 후보 포함 여부.
- staminaCost: 액티브 사용 스태미나.
- cooldownSeconds: 액티브 쿨타임.
- healthRestoreAmount/staminaRestoreAmount: 회복 효과.
- specialEffect: 특수 효과. 현재 UnlockBasicAttackCombo가 있다.
- maxHealthBonus/maxStaminaBonus/defenseRateBonus/attackDamageBonusPercent: 패시브 스탯 보너스.
- animationClip/animationStateName/animationTrigger: 표시 애니메이션.
- effectPrefab/effectLocalOffset/parentEffectToPlayer: VFX.
- hitboxPrefab/hitboxLocalOffset/hitboxDamage/hitboxRevivePower/hitboxDelay/hitboxLifetime: 공격 히트박스.

중요 규칙:
- DB 연결 예정이면 abilityId를 반드시 명시한다.
- abilityId를 비워두면 에셋 이름을 ID로 쓰므로 에셋 이름 변경에 취약하다.


3. PlayerAbilityInventory.cs

역할:
- 능력 풀, 획득한 모듈, 액티브 슬롯 관리.

주요 필드:
- abilityPool: 보상 후보 전체 목록.
- preventDuplicateModules: 중복 획득 방지.
- defaultActiveKeys: 액티브 슬롯 기본 키.
- equippedModules: 획득/장착된 모든 모듈.
- activeSlots: 액티브 스킬 슬롯 목록.

외부에서 호출할 함수:
- GenerateRewardOptions(int bossStage, int optionCount): 보상 후보 생성.
- SelectRewardOption(PlayerAbilityModule): 보상 선택/장착/저장.
- RestoreFromPlayerData(): NetworkPlayerData 기준 복구.
- RestoreFromSessionData(PlayerRef owner): PlayerSessionStore 기준 복구.
- TryChangeActiveKey(int activeSlotIndex, KeyCode newKey): 액티브 키 변경.
- GetActiveSlot(int activeSlotIndex): 슬롯 조회.
- GetSkillSlotUIData(float currentTime): HUD 표시용 스킬 슬롯 데이터 목록 반환.
- CreateContext(): Executor에 넘길 컨텍스트 생성.
- FindModuleById(string abilityId): abilityId로 모듈 찾기.

GetSkillSlotUIData()가 주는 값:
- IsEmpty
- AbilityId
- DisplayName
- Icon
- KeyCode
- CooldownRemaining
- CooldownDuration
- IsReady

UI 연동 규칙:
- SkillSlotHUDView는 SetData(SkillSlotUIData)를 사용한다.
- 기존 SetSlot(PlayerAbilityModule, KeyCode, float)은 제거됐다.
- UI는 ActiveSlots, NextReadyTime, Module.Icon 등을 직접 조합하지 않는다.

이벤트:
- RewardOptionsGenerated
- AbilityEquipped
- ActiveKeyChanged


4. PlayerAbilityController.cs

역할:
- 액티브 스킬 입력 감지와 네트워크 사용 요청.

외부에서 호출할 함수:
- TryActivateAbility(int activeSlotIndex): 특정 슬롯 스킬 사용 시도.

권한 흐름:
- InputAuthority만 Update()에서 키 입력을 읽는다.
- Client는 RPC_RequestActivateAbility()로 StateAuthority에 요청한다.
- StateAuthority가 쿨타임, 스태미나, 액션 잠금, 사용 가능 여부를 최종 판정한다.
- 성공 후 RPC_PlayAbilityPresentation()으로 모든 클라이언트에 표시와 쿨타임 종료 시간을 보낸다.


5. PlayerAbilityExecutor.cs

역할:
- 모듈 효과 실행 담당.

외부에서 호출할 함수:
- CanEquip(PlayerAbilityModule, PlayerAbilityContext)
- CanActivate(PlayerAbilityModule, PlayerAbilityContext)
- EquipModule(PlayerAbilityModule, PlayerAbilityContext)
- EquipPassive(PlayerAbilityModule, PlayerAbilityContext)
- Activate(PlayerAbilityModule, PlayerAbilityContext)
- PlayPresentation(PlayerAbilityModule, PlayerAbilityContext)

중요:
- Activate()는 게임 결과를 만드는 함수다. StateAuthority에서 호출되어야 한다.
- PlayPresentation()은 애니메이션/VFX 표시용이다. 데미지 판정을 넣으면 클라이언트별 불일치가 생길 수 있다.


6. PlayerSkillHitbox.cs

역할:
- 플레이어 스킬용 네트워크 히트박스.

외부에서 호출할 함수:
- Initialize(GameObject owner, NetworkObject attacker, float damage, float revivePower, float delay, float lifetime)

흐름:
- Spawned() 후 delay가 지나면 Collider 활성화.
- lifetime이 지나면 Despawn.
- 살아있는 PlayerStats에는 TakeDamage().
- 죽은 PlayerStats에는 RegisterReviveHit().


7. PlayerAbilityRewardController.cs

역할:
- 보스 처치 보상 후보 생성과 선택 처리.

외부에서 호출할 함수:
- OfferBossReward(int bossStage): 보상 후보 3개 생성.
- SelectPendingOption(int optionIndex): 선택한 보상 적용.

이벤트:
- BossRewardOffered(int bossStage, IReadOnlyList<PlayerAbilityModule> modules)
- BossRewardSelected(PlayerAbilityModule module)


8. PlayerAbilitySlot.cs

역할:
- 액티브 슬롯 하나의 모듈, 키, 쿨타임 상태.

외부에서 호출할 함수:
- SetKey(KeyCode)
- IsReady(float currentTime)
- StartCooldown(float currentTime)
- SetCooldownEndTime(float readyTime)

주의:
- UI는 PlayerAbilitySlot을 직접 표시하지 않고 PlayerAbilityInventory.GetSkillSlotUIData()를 통해 SkillSlotUIData로 읽는다.


9. PlayerAbilityContext.cs

역할:
- Executor에 넘기는 실행 컨텍스트.
- Owner, Stats, Runner 같은 실행 대상 정보를 묶어 전달한다.


10. PlayerAbilityRewardDebugTester.cs

역할:
- 에디터/개발 빌드에서 보상 흐름을 키 입력으로 테스트하는 디버그 스크립트.
최신 변경: 중앙 조작 잠금과 스킬 입력

PlayerAbilityController는 컷신 매니저나 UI 상태를 직접 알지 않는다.
스킬 입력/사용 가능 여부는 NetworkPlayerController.HasControlLock(PlayerControlLockFlags.Skill)로 확인한다.

스킬 사용 차단 흐름:
- CutsceneManager 또는 다른 외부 시스템이 SetControlLock(PlayerControlLockFlags.Skill, true)를 호출한다.
- PlayerAbilityController.Update()는 Skill 잠금이면 키 입력을 읽어도 스킬 사용 요청을 보내지 않는다.
- TryActivateAbility()도 Skill 잠금이면 false를 반환한다.

새 액티브 스킬 입력 경로를 추가할 때의 규칙:
- 컷신 여부를 직접 검사하지 않는다.
- 스킬을 실행하기 전에 HasControlLock(PlayerControlLockFlags.Skill)을 확인한다.
- 서버 권한에서 최종 판정하는 쿨타임, 스태미나, 사용 가능 조건은 기존 TryActivateAbility() 흐름을 유지한다.
