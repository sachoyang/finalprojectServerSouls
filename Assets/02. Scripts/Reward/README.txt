Reward README

목적:
- 보스 처치 후 보상 선택 UI와 보상 카드 흐름 설명.


1. 전체 보상 흐름

RewardManager
-> 보스 사망 감지
-> 상자 생성/컷씬/왜곡 트리거 활성화
-> RewardDistortionTrigger.Triggered
-> RewardManager.OfferRewardToLocalPlayer()
-> PlayerAbilityRewardController.OfferBossReward()
-> RewardSelectView.OnBossRewardOffered()
-> RewardCardView 표시
-> 선택 확정
-> PlayerAbilityRewardController.SelectPendingOption()
-> PlayerAbilityInventory.SelectRewardOption()
-> NetworkPlayerData/PlayerSessionStore 저장


2. RewardSelectView.cs

역할:
- 보상 선택 UI 전체 관리.

주요 필드:
- rootObject: UI 루트.
- cardContentParent: 카드가 생성될 부모.
- rewardCardPrefab: 카드 prefab.
- confirmButton: 선택 확정 버튼.
- messageText/messageDuration: 경고 메시지 표시.
- confirmDisabledColor/confirmEnabledColor: 확정 버튼 색.
- autoBindLocalRewardController: 로컬 보상 컨트롤러 자동 연결 여부.

외부에서 호출할 함수:
- Show(IReadOnlyList<PlayerAbilityModule> modules, Func<PlayerAbilityModule, int> getLevelFunc, Func<PlayerAbilityModule, bool> confirmCallback)
- Hide()

이벤트 바인딩:
- OnEnable()에서 로컬 PlayerAbilityRewardController를 찾는다.
- BossRewardOffered 이벤트를 받으면 Show()를 호출한다.
- BossRewardSelected 이벤트를 받으면 InventoryPanel 상태를 닫는다.

선택 흐름:
- RewardCardView 클릭 -> OnCardSelected()
- Confirm 버튼 -> OnClickConfirm()
- ConfirmRewardSelection() -> PlayerAbilityRewardController.SelectPendingOption()

주의:
- 일부 메시지 문자열이 인코딩 깨짐 상태다. UI 문구 정리가 필요하다.


3. RewardCardView.cs

역할:
- 보상 카드 하나의 표시와 클릭 처리.

주요 필드:
- iconImage
- skillNameText
- skillDescriptionText
- skillLevelText
- skillDivisionText
- selectButton
- selectedEffectObject

외부에서 호출할 함수:
- Setup(PlayerAbilityModule rewardModule, int currentLevel, Action<RewardCardView, PlayerAbilityModule> selectCallback)
- SetSelected(bool isSelected)

표시 데이터:
- PlayerAbilityModule.Icon
- PlayerAbilityModule.DisplayName
- PlayerAbilityModule.Description
- AbilityType에 따른 Passive/Active 구분


4. RewardDistortionTrigger.cs

역할:
- 플레이어가 왜곡 트리거에 들어왔을 때 보상 선택을 시작시키는 이벤트 발생기.

외부에서 호출할 함수:
- TriggerReward(): 수동으로 트리거 발생.

이벤트:
- Triggered

흐름:
- OnTriggerEnter(Collider other)에서 Player 태그 또는 플레이어 컴포넌트를 감지한다.
- 한 번만 Triggered 이벤트를 발생시킨다.


5. RewardManager.cs 위치

RewardManager.cs는 현재 Assets/02. Scripts/RewardManager.cs에 있다.
보상 UI 폴더 밖에 있지만 Reward 시스템의 시작점이다.

역할:
- 보스 사망 감지.
- 상자 생성과 컷씬.
- 왜곡 트리거 활성화.
- 로컬 플레이어 보상 UI 오픈.
- 모든 플레이어 선택 완료 확인.
- 다음 씬 로드.

주요 외부 연동:
- NetworkBossCore.CurrentState/CurrentHP 확인.
- CutsceneManager.PlayGoldChestCutscene()/RestoreGameplayCamera() 호출.
- ScenePrefabManager.ShowPrefab("RewardSelectCanvas") 호출.
- PlayerAbilityRewardController.OfferBossReward(bossStage) 호출.
- PlayerSessionStore.HasSelectedReward(player, bossStage) 확인.
- NetworkRunner.LoadScene(nextSceneName) 호출.


6. DB 연결 시 보상 저장

현재 저장:
- PlayerAbilityInventory.SelectRewardOption()
-> NetworkPlayerData.RecordAbility(module)
-> PlayerSessionStore.SaveAbility(player, abilityId)

DB 연결 후보:
- PlayerSessionStore.SaveAbility()
- PlayerSessionStore.GetAbilityIds()
- NetworkPlayerData.RecordAbilityId()

현재 abilityId는 string이다.
숫자 ID로 바꾸려면 Abilities README의 DB 주의점을 같이 확인해야 한다.
