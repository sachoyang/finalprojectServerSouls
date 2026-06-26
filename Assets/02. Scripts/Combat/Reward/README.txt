Skill Reward System README

목적
- 보스 처치 후 스킬 보상 후보를 만들고, UI에서 선택한 결과를 플레이어에게 장착하는 순서를 설명한다.
- DB Upload부터 실제 스킬 사용까지의 전체 구조는 Player/Abilities/README.txt를 먼저 참고한다.


1. 보상 시스템이 사용하는 스킬 데이터

RewardManager는 DB를 직접 호출하지 않는다.

데이터 준비 순서:
DB Upload
-> Bake
-> Resources/SkillModule
-> AbilityManager 로컬 카탈로그
-> 로그인 시 서버 수치 최신화
-> PlayerAbilityInventory가 보상 후보 조회

즉, 보상 시스템은 AbilityManager에 이미 준비된 PlayerAbilityModule을 사용한다.


2. 보스 처치부터 보상 UI까지

진행 순서:
1) RewardManager가 NetworkBossCore의 사망 상태를 감지한다.
2) 보상 BGM을 재생한다.
3) 골드 상자를 생성한다.
4) CutsceneManager로 상자 컷씬을 재생한다.
5) RewardDistortionTrigger를 활성화한다.
6) 플레이어가 트리거에 들어오면 Triggered 이벤트가 발생한다.
7) RewardManager.OfferRewardToLocalPlayer()가 실행된다.
8) 로컬 PlayerAbilityInventory.GenerateRewardOptions()를 호출한다.
9) RewardSelectCanvas를 활성화한다.
10) BossRewardOffered 이벤트로 후보를 UI에 전달한다.


3. 보상 후보 생성

호출 흐름:
RewardManager
-> PlayerAbilityInventory.GenerateRewardOptions(bossStage, 3)
-> AbilityManager.GetUnlockedAbilitiesList(userBitmask)

후보 검사:
- 모듈이 null이 아닌가?
- IncludeInRewardPool이 true인가?
- 현재 보스 단계가 MinBossStage~MaxBossStage 범위인가?
- preventDuplicateModules가 켜졌다면 이미 획득한 스킬이 아닌가?

후보 풀 기준:
- 로그인 상태: BackendManager/NetworkPlayerData의 유저 해금 비트마스크
- 로컬 상태: SkillModule에 저장된 IncludeInRewardPool

검사가 끝난 후보를 섞고 최대 3개를 반환한다.


4. 보상 UI 표시

관련 파일:
- RewardSelectView.cs
- RewardCardView.cs
- RewardSelectCanvas.prefab

연결 순서:
1) RewardSelectView.OnEnable()이 현재 RewardManager를 찾는다.
2) BossRewardOffered 이벤트를 구독한다.
3) 이벤트를 받으면 RewardCardView를 후보 수만큼 생성한다.
4) 각 카드에 PlayerAbilityModule 데이터를 표시한다.

표시 데이터:
- Icon
- DisplayName
- Description
- Passive/Active 구분

RewardSelectView 주요 설정:
- rootObject
- cardContentParent
- rewardCardPrefab
- confirmButton
- messageText
- autoBindRewardManager


5. 카드 선택과 확정

진행 순서:
1) RewardCardView 클릭
2) RewardSelectView.OnCardSelected()
3) 선택 효과 표시
4) Confirm 버튼 클릭
5) RewardSelectView.ConfirmRewardSelection()
6) 선택한 모듈의 PendingOptions 인덱스 확인
7) RewardManager.SelectPendingOption(optionIndex) 호출

선택 실패:
- 선택된 카드가 없음
- RewardManager를 찾지 못함
- PendingOptions에 없는 모듈
- Inventory 장착 검사 실패

실패하면 UI 메시지를 표시하고 선택창을 유지한다.


6. 플레이어에게 스킬 장착

진행 순서:
1) RewardManager가 로컬 PlayerAbilityInventory를 찾는다.
2) PlayerAbilityInventory.SelectRewardOption()을 호출한다.
3) 중복 획득과 장착 가능 여부를 검사한다.
4) PlayerAbilityExecutor.EquipModule()을 호출한다.
5) NetworkPlayerData.RecordAbility()로 abilityId를 기록한다.
6) RewardManager가 PendingOptions를 비운다.
7) BossRewardSelected 이벤트를 보낸다.
8) RewardSelectView가 선택 UI를 닫는다.

패시브:
- 즉시 스탯과 특수 효과 적용

액티브:
- PlayerAbilitySlot 생성
- 기본 키 배정
- HUD 슬롯에 표시


7. 멀티플레이어 선택 완료와 씬 이동

RewardManager는 PlayerSessionStore.HasSelectedReward(player, bossStage)로 각 플레이어의 선택 완료 여부를 확인한다.

진행 순서:
1) 선택 성공 시 NetworkPlayerData.MarkRewardSelected() 호출
2) PlayerSessionStore에 해당 플레이어의 선택 완료 상태 기록
3) RewardManager가 모든 ActivePlayers를 검사
4) 전원이 선택했거나 제한 시간이 끝나면 왜곡 효과 정리
5) PlayerSessionStore.SaveActivePlayerStats() 호출
6) 서버가 nextSceneName으로 씬 이동


8. 저장과 다음 씬 복구

보상 선택 저장:
PlayerAbilityInventory.SelectRewardOption()
-> NetworkPlayerData.RecordAbility(module)
-> PlayerSessionStore.SaveAbility(player, abilityId)

다음 씬 복구:
NetworkPlayerData/PlayerAbilityInventory
-> PlayerSessionStore.GetAbilityIds(owner)
-> AbilityManager.FindByAbilityId(abilityId)
-> 최신 PlayerAbilityModule 장착

저장에는 Unity 에셋 자체가 아니라 AbilityId를 사용한다.


9. 주요 클래스 역할

RewardManager:
- 보스 사망 감지
- 상자/컷씬/왜곡 진행
- 후보와 PendingOptions 관리
- 선택 결과를 로컬 Inventory에 전달
- 모든 플레이어 선택 완료 확인
- 다음 씬 이동

RewardDistortionTrigger:
- 플레이어 진입 시 보상 시작 이벤트 발생
- 한 번만 실행

RewardSelectView:
- 전체 선택 UI 관리
- RewardManager 이벤트 구독
- 카드 생성과 Confirm 처리

RewardCardView:
- 카드 하나의 데이터 표시
- 클릭과 선택 효과 처리

PlayerAbilityInventory:
- AbilityManager에서 후보 조회
- 선택된 스킬을 플레이어에게 장착
- abilityId 저장


10. 점검 목록

- 현재 스테이지에 RewardManager가 존재하는가?
- RewardManager에 boss와 상자 설정이 연결됐는가?
- RewardDistortionTrigger가 준비됐는가?
- ScenePrefabManager에 RewardSelectCanvas가 등록됐는가?
- RewardSelectCanvas의 autoBindRewardManager가 켜져 있는가?
- AbilityManager.IsLoaded가 true인가?
- 후보 SkillModule의 IncludeInRewardPool과 보스 단계 범위가 맞는가?
- 중복 방지 때문에 후보가 0개가 된 것은 아닌가?
- 선택 후 NetworkPlayerData에 abilityId가 기록되는가?
- 모든 플레이어 선택 후 다음 씬으로 이동하는가?


11. 관련 문서

전체 스킬 시스템:
- Assets/02. Scripts/Player/Abilities/README.txt

전체 문서에서 확인할 내용:
- PlayerAbilityModule 제작
- DB Upload
- Bake
- AbilityManager 로컬 초기화
- 로그인 서버 최신화
- 플레이어 스킬 복구
- 액티브 스킬 사용과 네트워크 권한
