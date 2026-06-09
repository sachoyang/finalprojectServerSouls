Hud README

목적:
- HUD 표시 구조와 UIData 연결 방식을 설명한다.
- UI는 서버/네트워크 컴포넌트가 확정한 상태를 읽어서 표시만 한다.


1. 기본 원칙

UI가 하는 일:
- 체력바 표시
- 스태미나바 표시
- 스킬 아이콘/키/쿨타임 표시
- 파티원 HP/SP 표시
- 보스 HP와 상태이상 표시
- 플레이어/보스 버프, 디버프 아이콘 표시

UI가 하지 않는 일:
- 체력 수정
- 스태미나 수정
- 스킬 쿨타임 직접 시작
- 상태이상 직접 추가/삭제
- 보유 스킬 직접 변경

게임 상태 변경은 PlayerStats, PlayerAbilityController, PlayerStatusController, NetworkPlayerData 같은 권한 컴포넌트가 담당한다.


2. UIData 구조

PlayerHUDData.cs
- PlayerStats.GetHUDData()가 반환한다.
- 로컬 플레이어 HUD에 필요한 체력, 스태미나, 사망 여부를 담는다.

SkillSlotUIData.cs
- PlayerAbilityInventory.GetSkillSlotUIData(currentTime)가 반환한다.
- 스킬 슬롯 UI에 필요한 abilityId, 표시 이름, 아이콘, 키, 남은 쿨타임, 전체 쿨타임을 담는다.
- 빈 슬롯은 SkillSlotUIData.Empty로 표현한다.

PartyMemberUIData.cs
- 파티 HUD와 네임플레이트에서 공통으로 쓸 수 있는 표시용 데이터다.
- 현재 HUDManager는 원격 플레이어의 PlayerStats.GetHUDData()를 읽어 PartyMemberUIData를 만든다.


3. HUDManager.cs

역할:
- 런타임에서 로컬 플레이어, 보스, 파티원을 찾는다.
- 각 View에 표시용 데이터만 전달한다.

주요 흐름:
- UpdatePlayerHUD()
  - playerStats.GetHUDData()
  - playerHUDView.SetHp()
  - playerHUDView.SetSp()
  - playerStatusController.GetActiveStatusesForUI()
  - playerHUDView.SetStatuses()

- UpdateBossHUD()
  - NetworkBossCore.CurrentHP/maxHP 읽기
  - boss.GetActiveStatusesForUI()
  - bossHUDView.SetStatuses()

- UpdateSkillHUD()
  - abilityInventory.GetSkillSlotUIData(currentTime)
  - skillSlotViews[i].SetData()

- RefreshPartyHUD()
  - FindPartyMemberUIData()
  - partyMemberHUDViews[i].SetData()

주의:
- HUDManager는 아직 파티원 닉네임을 실제 DB/네트워크 닉네임으로 받지 않는다.
- 현재 파티 표시 이름은 임시로 PlayerRef 기반 "Player {id}" 형식이다.


4. SkillSlotHUDView.cs

역할:
- 스킬 슬롯 하나의 아이콘, 키, 쿨타임 오버레이/텍스트를 표시한다.

외부에서 호출할 함수:
- SetData(SkillSlotUIData data)
- Clear()

중요:
- 기존 SetSlot(PlayerAbilityModule, KeyCode, float)은 제거됐다.
- 이 뷰는 PlayerAbilityModule이나 PlayerAbilitySlot을 직접 받지 않는다.


5. PartyMemberHUDView.cs

역할:
- 파티원 하나의 HP/SP 표시.

외부에서 호출할 함수:
- SetData(PartyMemberUIData data)
- SetVisible(bool)
- SetStats(float currentHp, float maxHp, float currentSp, float maxSp)

현재 상태:
- HP/SP fill만 연결되어 있다.
- PartyMemberUIData에는 이름, 생존, 다운, 로컬 여부가 들어 있지만 View prefab 필드가 아직 없어서 표시하지 않는다.

확장 후보:
- nicknameText
- statusText
- localPlayerMarker
- downedIcon
- deadIcon
- 상태이상 아이콘 바


6. PlayerHUDView.cs

역할:
- 로컬 플레이어 HP/SP와 상태이상 아이콘 표시.

외부에서 호출할 함수:
- SetHp(float currentHp, float maxHp)
- SetSp(float currentSp, float maxSp)
- SetStatuses(IReadOnlyList<ActiveStatusUIInfo> statuses)
- ClearStatuses()


7. BossHUDView.cs

역할:
- 보스 이름, HP, 상태이상 아이콘 표시.

외부에서 호출할 함수:
- SetBossName(string)
- SetHp(float currentHp, float maxHp)
- SetStatuses(IReadOnlyList<ActiveStatusUIInfo> statuses)
- ClearStatuses()
- SetVisible(bool)
- Clear()


8. StatusIconBarView.cs

위치:
- 현재 Compile 경로는 Assets/StatusIconBarView.cs다.

역할:
- ActiveStatusUIInfo 목록을 받아 상태이상 아이콘 UI를 표시한다.
- PlayerHUDView와 BossHUDView에서 사용한다.


9. 파티원 스킬 인벤토리 표시

단순히 현재 클라이언트에 복구된 파티원 PlayerAbilityInventory를 표시하려면:
- partyPlayer.GetComponent<PlayerAbilityInventory>().GetSkillSlotUIData(currentTime)

서버 기준 보유 목록을 표시하려면:
- NetworkPlayerData.SavedAbilityIds를 기준으로 abilityId 목록을 읽는다.
- 아이콘/이름은 PlayerAbilityInventory.FindModuleById(abilityId)로 PlayerAbilityModule을 찾아 표시한다.

쿨타임까지 표시하려면:
- 현재 구조에서는 ActiveSlots의 NextReadyTime이 필요하다.
- 단순 보유 스킬 목록만 표시한다면 SavedAbilityIds만으로 충분하다.
