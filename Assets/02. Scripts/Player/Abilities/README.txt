Player Ability System README

목적
- 스킬 제작부터 DB 업로드, Bake, 게임 초기화, 로그인 최신화, 보상 획득, 사용, 저장 복구까지 전체 진행 순서를 설명한다.
- 처음 보는 사람은 1번부터 순서대로 읽으면 된다.


1. 전체 진행 과정

[개발 단계]
PlayerAbilityModule 작성
-> DB Upload
-> 서버 DB에 밸런스 수치 저장
-> Bake
-> Resources/SkillModule의 .asset 갱신
-> 애니메이션/VFX/히트박스/사운드 참조 확인

[게임 시작]
GameManager
-> AbilityManager.Awake()
-> Resources/SkillModule 전체 로드
-> AbilityId/BitIndex 카탈로그 생성

[로그인 게임]
BackendManager.LoginUser()
-> 유저 스킬 비트마스크 수신
-> LoginSceneController
-> AbilityManager.FetchAbilities()
-> 서버 최신 수치를 메모리상의 SkillModule에 반영

[로컬 게임]
로그인과 FetchAbilities 없이
-> 시작 시 로드한 Bake 결과물을 그대로 사용

[게임 플레이]
보스 처치
-> RewardManager가 보상 후보 생성
-> 플레이어가 하나 선택
-> PlayerAbilityInventory에 장착
-> PlayerAbilityController가 입력 감지
-> 서버 권한에서 사용 검증
-> PlayerAbilityExecutor가 효과/히트박스 실행
-> 모든 클라이언트가 애니메이션/VFX 재생

[씬 이동 또는 재접속]
PlayerSessionStore/NetworkPlayerData에서 abilityId 복구
-> AbilityManager에서 최신 SkillModule 조회
-> PlayerAbilityInventory에 다시 장착


2. 스킬 원본 만들기: PlayerAbilityModule

파일:
- Assets/02. Scripts/Player/Abilities/PlayerAbilityModule.cs

에셋 위치:
- Assets/02. Scripts/Player/Abilities/Resources/SkillModule

역할:
- 스킬 하나의 서버 관리 수치와 Unity 전용 에셋 참조를 함께 보관하는 ScriptableObject다.

반드시 확인할 식별값:
- AbilityId: DB, Bake, 저장, RPC, 런타임 조회에서 사용하는 고유 문자열이다.
- BitIndex: 유저 해금 비트마스크에서 사용하는 위치다. 다른 스킬과 중복되면 안 된다.

주요 게임 데이터:
- DisplayName, Description, Icon
- AbilityType: Passive 또는 Active
- IncludeInRewardPool
- MinBossStage/MaxBossStage
- StaminaCost, CooldownSeconds
- 회복량과 패시브 스탯 보너스
- SpecialEffect

Unity 전용 데이터:
- AnimationClip, AnimationStateName, AnimationTrigger
- EffectPrefab과 위치/부모 설정
- HitboxPrefab과 데미지/위치/지연/수명
- SoundClip, SoundVolume, SoundDelay

주의:
- AbilityId를 비워두면 에셋 이름을 대신 사용하므로 에셋 이름 변경에 취약하다.
- Sound 데이터는 현재 모듈에 저장되지만 PlayerAbilityExecutor의 실제 재생 호출은 아직 연결되지 않았다.


3. Unity 데이터 DB로 올리기: Upload

메뉴:
- Soul Rush/스킬 DB로 업로드 (Upload)

관련 파일:
- Assets/02. Scripts/DB/Editor/AbilityUploadWindow.cs
- Assets/02. Scripts/DB/Editor/SoulRushApiSettings.cs

사용 순서:
1) Project 창에서 업로드할 PlayerAbilityModule 에셋을 선택한다.
2) Upload 창을 연다.
3) upload_ability.php 주소를 확인한다.
4) 선택한 스킬 데이터 DB 업로드 버튼을 누른다.

DB로 전송하는 값:
- BitIndex -> bit_index
- AbilityId -> ability_id
- DisplayName -> display_name
- Description -> description
- AbilityType -> ability_type
- StaminaCost -> stamina_cost
- CooldownSeconds -> cooldown_seconds
- HitboxDamage -> damage_multiplier
- HitboxLifetime -> duration
- SpecialEffect -> special_effect

DB로 전송하지 않는 값:
- Icon
- 애니메이션
- VFX와 프리팹
- 히트박스 프리팹
- 사운드
- 그 밖의 UnityEngine.Object 참조

정리:
- Upload는 Unity 에셋의 밸런스 수치를 DB로 보내는 방향이다.
- Unity 전용 에셋 참조는 로컬 SkillModule에만 남는다.


4. DB 데이터를 에셋으로 받기: Bake

메뉴:
- Soul Rush/스킬 DB 동기화 (Bake)

관련 파일:
- Assets/02. Scripts/DB/Editor/AbilityBakeWindow.cs

저장 위치:
- Assets/02. Scripts/Player/Abilities/Resources/SkillModule

진행 과정:
1) SoulRushApiSettings.bakeUrl의 get_abilities.php를 호출한다.
2) 서버의 AbilityDBResponse를 받는다.
3) ability_id와 같은 이름의 SkillModule 에셋을 찾는다.
4) 기존 에셋이면 InitializeFromDB()로 서버 수치를 덮어쓴다.
5) 에셋이 없으면 새 PlayerAbilityModule을 생성한다.
6) AssetDatabase.SaveAssets()로 파일에 저장한다.

InitializeFromDB()가 갱신하는 값:
- 에셋 이름
- BitIndex
- AbilityId
- DisplayName
- Description
- AbilityType
- StaminaCost
- CooldownSeconds
- HitboxDamage
- HitboxLifetime
- SpecialEffect

Bake 후에도 유지되는 기존 값:
- Icon
- 애니메이션과 Trigger
- VFX 프리팹과 위치
- 히트박스 프리팹과 세부 설정
- 사운드
- 서버 응답에 없는 Unity 전용 데이터

중요:
- 기존 에셋을 Bake하면 에셋 참조는 유지되고 서버 수치만 갱신된다.
- 서버에만 존재하던 스킬은 새 에셋으로 생성되므로 Unity 전용 참조를 직접 연결해야 한다.
- Bake 결과물은 로컬 플레이의 기본 데이터이므로 Git에 포함한다.


5. 게임 시작: AbilityManager 로컬 초기화

관련 파일:
- Assets/02. Scripts/DB/AbilityManager.cs
- Assets/02. Scripts/System/GameManager.cs

진행 과정:
1) GameManager가 AbilityManager를 준비한다.
2) AbilityManager.Awake()가 LoadLocalAbilityCatalog()를 호출한다.
3) Resources.LoadAll<PlayerAbilityModule>("SkillModule")로 Bake된 에셋을 읽는다.
4) AbilityId와 BitIndex 기준 딕셔너리를 만든다.
5) 하나 이상의 모듈이 등록되면 IsLoaded가 true가 된다.

이 초기화는 로그인보다 먼저 실행된다.
따라서 서버를 거치지 않는 로컬 플레이도 SkillModule을 사용할 수 있다.

AbilityManager의 역할:
- 전체 스킬 원본 카탈로그 관리
- AbilityId/BitIndex 조회
- 서버 밸런스 수치 최신화
- 유저 해금 비트마스크 해석

AbilityManager가 관리하지 않는 것:
- 플레이어별 획득 목록
- 액티브 슬롯과 키
- 플레이어별 쿨다운
- 실제 스킬 입력과 실행

위 상태는 각 플레이어의 PlayerAbilityInventory와 PlayerAbilityController가 관리한다.


6. 로그인: 서버 최신 데이터 적용

관련 파일:
- Assets/02. Scripts/Login/LoginSceneController.cs
- Assets/02. Scripts/DB/BackendManager.cs
- Assets/02. Scripts/DB/AbilityManager.cs

진행 과정:
1) BackendManager.LoginUser()가 로그인한다.
2) 유저 정보와 CurrentSkillsBitmask를 저장한다.
3) LoginSceneController가 AbilityManager.FetchAbilities()를 호출한다.
4) AbilityManager가 get_abilities.php에서 전체 스킬 수치를 받는다.
5) ability_id로 로컬 SkillModule을 찾는다.
6) InitializeFromDB()로 메모리상의 서버 관리 수치를 갱신한다.
7) 변경된 BitIndex에 맞춰 카탈로그 인덱스를 다시 만든다.
8) 유저 비트마스크를 해석해 보상 가능 스킬을 갱신한다.
9) 완료 후 다음 씬으로 이동한다.

Bake와 FetchAbilities의 차이:
- Bake는 .asset 파일을 실제로 저장한다.
- FetchAbilities는 현재 실행 중인 메모리 데이터만 변경한다.
- FetchAbilities 결과는 Unity 에셋 파일에 영구 저장되지 않는다.

서버 요청 실패:
- AbilityManager는 이미 로컬 SkillModule을 가지고 있다.
- 로컬 카탈로그가 준비되어 있으면 Bake된 기본 수치로 계속 진행할 수 있다.


7. 플레이어 생성과 저장 스킬 복구

관련 파일:
- PlayerAbilityInventory.cs
- NetworkPlayerData.cs
- PlayerSessionStore.cs

진행 과정:
1) 플레이어 프리팹의 PlayerAbilityInventory가 초기화된다.
2) PlayerAbilityExecutor가 없으면 자동으로 추가한다.
3) 저장된 abilityId 목록을 NetworkPlayerData 또는 PlayerSessionStore에서 읽는다.
4) PlayerAbilityInventory.FindModuleById()를 호출한다.
5) AbilityManager.FindByAbilityId()에서 현재 최신 모듈을 찾는다.
6) EquipModule()로 다시 장착한다.

패시브 복구:
- 스탯 보너스와 특수 효과를 즉시 적용한다.

액티브 복구:
- PlayerAbilitySlot을 생성한다.
- 저장된 키가 있으면 PlayerPrefs의 키 설정을 사용한다.


8. 보스 처치와 스킬 보상 생성

관련 파일:
- Assets/02. Scripts/RewardManager.cs
- PlayerAbilityInventory.cs
- Assets/02. Scripts/Reward/RewardSelectView.cs

진행 과정:
1) RewardManager가 보스 사망을 감지한다.
2) 상자/컷씬/왜곡 트리거 과정을 실행한다.
3) 로컬 PlayerAbilityInventory.GenerateRewardOptions()를 호출한다.
4) Inventory가 AbilityManager.GetUnlockedAbilitiesList()에서 후보를 받는다.
5) 보스 단계, 보상 풀 포함 여부, 중복 획득 여부를 검사한다.
6) 후보를 섞고 최대 3개를 반환한다.
7) RewardManager가 BossRewardOffered 이벤트를 보낸다.
8) RewardSelectView가 카드를 표시한다.

후보 풀 기준:
- 로그인 상태: 서버에서 받은 유저 스킬 비트마스크
- 로그인하지 않은 로컬 상태: SkillModule.IncludeInRewardPool


9. 보상 선택과 플레이어 장착

진행 과정:
1) 플레이어가 RewardCardView에서 카드를 선택한다.
2) RewardSelectView가 RewardManager.SelectPendingOption()을 호출한다.
3) RewardManager가 선택된 모듈을 로컬 PlayerAbilityInventory에 전달한다.
4) PlayerAbilityInventory.SelectRewardOption()이 중복과 장착 가능 여부를 검사한다.
5) PlayerAbilityExecutor.EquipModule()을 호출한다.
6) NetworkPlayerData.RecordAbility()로 abilityId를 기록한다.
7) PlayerSessionStore에 씬 이동용 데이터가 보관된다.

패시브 선택:
- 스탯 보너스
- 즉시 효과
- SpecialEffect

액티브 선택:
- PlayerAbilitySlot 생성
- 기본 키 할당
- HUD 표시 대상에 포함


10. 액티브 스킬 입력과 서버 검증

관련 파일:
- PlayerAbilityController.cs
- PlayerAbilityInventory.cs
- PlayerAbilitySlot.cs

진행 과정:
1) InputAuthority를 가진 PlayerAbilityController만 키 입력을 읽는다.
2) PlayerAbilityInventory.ActiveSlots에서 입력된 슬롯을 찾는다.
3) 호스트면 TryActivateAbility()를 직접 호출한다.
4) 클라이언트면 RPC_RequestActivateAbility()로 StateAuthority에 요청한다.
5) StateAuthority가 최종 사용 가능 여부를 검사한다.

검사 항목:
- 슬롯과 모듈 존재 여부
- Active 타입 여부
- 쿨다운
- 스태미나
- 사망 상태
- 액션 애니메이션 잠금
- PlayerControlLockFlags.Skill

조작 잠금 규칙:
- 컷씬이나 UI를 PlayerAbilityController에서 직접 검사하지 않는다.
- 외부 시스템은 NetworkPlayerController.SetControlLock()으로 Skill 잠금을 건다.
- 새로운 스킬 입력 경로도 같은 잠금 검사를 사용해야 한다.


11. 스킬 효과, 히트박스, 표현 실행

관련 파일:
- PlayerAbilityExecutor.cs
- PlayerAbilityContext.cs
- PlayerSkillHitbox.cs

사용 성공 후:
1) PlayerAbilityExecutor.Activate()가 게임 결과를 실행한다.
2) 회복 효과를 적용한다.
3) HitboxPrefab을 생성한다.
4) PlayerSkillHitbox에 공격자, 데미지, 부활 수치, 지연, 수명을 전달한다.
5) 슬롯 쿨다운을 시작한다.
6) RPC_PlayAbilityPresentation()을 모든 클라이언트에 전송한다.
7) 각 클라이언트가 애니메이션과 VFX를 재생한다.
8) 서버가 확정한 쿨다운 종료 시간을 각 로컬 슬롯에 적용한다.

권한 규칙:
- Activate()는 게임 결과를 만들므로 StateAuthority에서 실행한다.
- PlayPresentation()은 애니메이션/VFX 표현만 담당한다.
- 표현 함수에 데미지 판정을 넣으면 클라이언트별 결과가 달라질 수 있다.

히트박스:
- HitboxDelay 후 Collider 활성화
- HitboxLifetime 후 Despawn
- 살아있는 대상은 TakeDamage()
- 죽은 대상은 RegisterReviveHit()


12. HUD와 키 변경

HUD 흐름:
PlayerAbilityInventory.GetSkillSlotUIData(currentTime)
-> SkillSlotUIData 목록 생성
-> HUDManager
-> SkillSlotHUDView.SetData()

표시 데이터:
- AbilityId
- DisplayName
- Icon
- KeyCode
- 남은 쿨다운
- 전체 쿨다운
- 사용 가능 상태

UI 규칙:
- UI는 PlayerAbilitySlot 내부 값을 직접 조합하지 않는다.
- GetSkillSlotUIData()가 만든 읽기 전용 데이터만 사용한다.

키 변경:
- PlayerAbilityInventory.TryChangeActiveKey()
- PlayerPrefs에 슬롯별 키 저장
- ActiveKeyChanged 이벤트로 UI 갱신


13. 주요 클래스 역할 요약

PlayerAbilityModule:
- 스킬 원본 데이터와 Unity 에셋 참조

AbilityManager:
- 전체 스킬 카탈로그와 서버 수치 최신화

PlayerAbilityInventory:
- 플레이어별 획득 스킬, 액티브 슬롯, 키, 보상 후보

RewardManager:
- 보상 진행, 후보 대기 상태, 선택 결과 전달

PlayerAbilityController:
- 입력 감지, RPC 요청, 서버 사용 검증

PlayerAbilityExecutor:
- 패시브/액티브 효과, 히트박스, 애니메이션/VFX 실행

PlayerAbilitySlot:
- 액티브 모듈, 키, 쿨다운 상태

PlayerAbilityContext:
- 실행 대상, Stats, Runner 묶음

PlayerSkillHitbox:
- 네트워크 스킬 충돌과 데미지/부활 판정


14. 작업 상황별 권장 순서

기존 스킬 밸런스 변경:
1) SkillModule 수정
2) DB Upload
3) 서버 DB 확인
4) Bake
5) Unity 에셋 참조 유지 확인
6) 로컬/로그인 테스트
7) SkillModule 변경 Git 포함

새 스킬 추가:
1) PlayerAbilityModule 생성
2) 고유 AbilityId와 BitIndex 지정
3) 타입과 밸런스 수치 설정
4) 애니메이션/VFX/히트박스/사운드 연결
5) DB Upload
6) Bake
7) 에셋 참조 재확인
8) 필요하면 PlayerAnimatorSetupTool의 스킬 동기화 실행
9) 로컬/호스트/클라이언트 테스트
10) 생성된 SkillModule Git 포함

서버에서 먼저 스킬을 추가한 경우:
1) Bake
2) 새로 생성된 SkillModule 확인
3) Unity 전용 참조 연결
4) 로컬 테스트
5) 로그인 최신화 테스트


15. 필수 점검 목록

- AbilityId가 비어 있거나 중복되지 않았는가?
- BitIndex가 0~62 범위이며 중복되지 않았는가?
- SkillModule이 Resources/SkillModule 아래에 있는가?
- 새 Bake 에셋에 Icon/애니메이션/VFX/히트박스가 연결됐는가?
- 액티브 스킬에 AnimationTrigger가 Animator와 일치하는가?
- HitboxPrefab이 NetworkObject를 사용한다면 Fusion Prefab 등록이 되어 있는가?
- 로컬 모드에서 로그인 없이 스킬 카탈로그가 로드되는가?
- 로그인 후 서버 수치가 반영되는가?
- 호스트와 클라이언트에서 스킬이 한 번만 실행되는가?
- 씬 이동 후 abilityId 기준으로 스킬이 복구되는가?
