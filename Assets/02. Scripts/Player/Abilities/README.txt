Player Ability System README

목적
- 스킬 ScriptableObject 생성, 로컬 SkillModule 관리, DB Bake/Upload 방향, 게임 시작 로드, 보상 획득, 전투 실행, 세션 복구 흐름을 설명한다.
- 현재 스킬 모듈은 Active / Passive / Utility 타입별 ScriptableObject로 분리되어 있다.


1. 전체 진행 과정

[개발 단계]
타입별 스킬 모듈 생성
-> 로컬 SkillModule 에셋에 Unity 전용 참조 연결
-> 필요 시 DB Upload
-> 서버 DB에 밸런스 수치 저장
-> Bake
-> Resources/SkillModule의 .asset 갱신
-> 애니메이션/VFX/공격판정/사운드 참조 확인

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
-> 시작 시 로드한 SkillModule 값을 그대로 사용

[게임 플레이]
보스 처치
-> RewardManager가 보상 후보 생성
-> 플레이어가 하나 선택
-> PlayerAbilityInventory에 등록 또는 레벨업
-> PlayerAbilityController가 입력 감지
-> 서버 권한에서 사용 검증
-> PlayerAbilityExecutor가 효과/공격판정 실행
-> 모든 클라이언트가 애니메이션/VFX/사운드 재생

[같은 방 씬 이동]
PlayerSessionStore/NetworkPlayerData에서 abilityId와 level 복구
-> AbilityManager에서 최신 SkillModule 조회
-> PlayerAbilityInventory에 다시 등록


2. 스킬 원본 만들기

공통 부모:
- Assets/02. Scripts/Player/Abilities/PlayerAbilityModule.cs

타입별 모듈:
- Assets/02. Scripts/Player/Abilities/ActiveAbilityModule.cs
- Assets/02. Scripts/Player/Abilities/PassiveAbilityModule.cs
- Assets/02. Scripts/Player/Abilities/UtilityAbilityModule.cs

에셋 위치:
- Assets/02. Scripts/Player/Abilities/Resources/SkillModule/ActiveSkill
- Assets/02. Scripts/Player/Abilities/Resources/SkillModule/PassiveSkill
- Assets/02. Scripts/Player/Abilities/Resources/SkillModule/UtilitySkill

폴더 구조:
```text
Resources/SkillModule
├─ ActiveSkill
├─ PassiveSkill
└─ UtilitySkill
```

Create 메뉴:
- Create > ServerSouls > Player Modules > Active Ability
- Create > ServerSouls > Player Modules > Passive Ability
- Create > ServerSouls > Player Modules > Utility Ability

주의:
- 처음 만들 때 타입을 선택한다.
- 인스펙터에서 AbilityType을 바꾸는 구조가 아니다.
- 세 타입 모두 PlayerAbilityModule을 상속하므로 런타임에서는 PlayerAbilityModule로 공통 처리된다.


3. 공통 필드

모든 모듈이 가지는 값:
- BitIndex: 해금 비트마스크 위치. 다른 스킬과 중복되면 안 된다.
- AbilityId: DB, 저장, RPC, 런타임 조회에서 사용하는 고유 문자열.
- DisplayName
- Description
- Icon
- AppearStage: 몇 스테이지부터 보상 후보에 등장할지.
- UnlockedSkill
- BasicSkill
- MaxLevel

AbilityId를 비워두면 에셋 이름을 대신 사용한다. 에셋 이름 변경에 취약하므로 직접 입력하는 것을 권장한다.

BitIndex 범위:
- Active: 1 ~ 19
- Passive: 20 ~ 39
- Utility: 40 ~ 60

새 SkillModule 에셋의 BitIndex가 0이면 에디터가 타입별 범위에서 다음 빈 번호를 자동 할당한다.
인스펙터의 `다음 빈 BitIndex 자동 할당` 버튼으로도 다시 배정할 수 있다.

주의:
- BitIndex는 유저 해금 비트마스크와 직접 연결된다.
- 기존 저장 데이터가 있는 상태에서 BitIndex를 바꾸면 유저가 가진 스킬 해금 상태가 달라질 수 있다.
- DB의 abilities.bit_index도 Unity 에셋과 같은 범위 규칙을 사용해야 한다.


4. ActiveAbilityModule

용도:
- 플레이어가 슬롯에 장착하고 직접 사용하는 공격/액션 스킬.

가지는 값:
- CooldownSeconds
- StaminaCost
- LevelSettings.damageMultiplier
- AnimationClip / AnimationStateName / AnimationTrigger / AnimationSpeed
- RootMotionMode
- StaminaRecoveryDelayMode
- OpensComboInput / ComboInputOpenNormalizedTime
- EffectPrefab / EffectLocalOffset / ParentEffectToPlayer
- SoundClip / SoundVolume / SoundDelay
- HitboxPrefab / HitboxLocalOffset / HitboxDelay / HitboxLifetime
- HitEvents

데미지 기준:
- 실제 공격 판정은 HitEvents를 사용한다.
- 각 HitEvent의 DamageRate가 기본 스킬 타격 배율이다.
- 레벨별 DamageMultiplier는 HitEvent의 DamageRate에 추가로 곱해지는 레벨 배율이다.

예:
```text
기본 공격력 x HitEvent.damageRate x ActiveLevel.damageMultiplier x 버프/디버프/방어 보정
```


5. PassiveAbilityModule

용도:
- 획득 즉시 또는 레벨업 시 플레이어 스탯을 올리는 스킬.

가지는 값:
- LevelSettings.maxHealthBonus
- LevelSettings.maxStaminaBonus
- LevelSettings.defenseBonusPercent
- LevelSettings.attackDamageBonusPercent
- AnimationClip / AnimationStateName / AnimationTrigger / AnimationSpeed
- EffectPrefab / EffectLocalOffset / ParentEffectToPlayer
- SoundClip / SoundVolume / SoundDelay

가지지 않는 값:
- CooldownSeconds
- StaminaCost
- HitEvents
- Hitbox
- HealthRestoreAmount
- StaminaRestoreAmount

Passive도 획득 연출이 필요할 수 있으므로 애니메이션/VFX/사운드는 유지한다.

레벨 값 기준:
- 레벨별 값은 “해당 레벨의 최종 증가값”이다.
- 런타임에서는 이전 레벨값과 새 레벨값의 차이만 계산해 적용한다.
- 공격력 증가와 방어력 증가는 퍼센트 입력 기준이다.

예:
```text
10 입력 = 10%
100 입력 = 100%
```


6. UtilityAbilityModule

용도:
- 회복, 기본 공격 해금, 특수 기능 같은 기능성 스킬.

가지는 값:
- CooldownSeconds
- StaminaCost
- SpecialEffect
- LevelSettings.healthRestoreAmount
- LevelSettings.staminaRestoreAmount
- AnimationClip / AnimationStateName / AnimationTrigger / AnimationSpeed
- EffectPrefab / EffectLocalOffset / ParentEffectToPlayer
- SoundClip / SoundVolume / SoundDelay

가지지 않는 값:
- HitEvents
- Hitbox
- Passive 스탯 증가값
- Active 스킬 데미지 배율

기본 공격 해금처럼 회복량이 필요 없는 Utility는 회복 수치를 0으로 둔다.


7. DB Upload 방향

관련 파일:
- Assets/02. Scripts/DB/Editor/AbilityUploadWindow.cs

현재 주의:
- 기존 UploadWindow에는 예전 단일 PlayerAbilityModule 업로드 형식이 일부 남아 있다.
- DB 업로드 기능을 다시 사용할 때는 타입별 모듈 구조에 맞게 수정해야 한다.

권장 업로드 구조:

공통으로 항상 업로드:
- abilities

Active 업로드:
- abilities
- active_abilities
- active_ability_levels

Passive 업로드:
- abilities
- passive_abilities
- passive_ability_levels

Utility 업로드:
- abilities
- utility_abilities
- utility_ability_levels

코드 기준:
```csharp
if (module is ActiveAbilityModule active)
{
    // active 전용 데이터 업로드
}
else if (module is PassiveAbilityModule passive)
{
    // passive 전용 데이터 업로드
}
else if (module is UtilityAbilityModule utility)
{
    // utility 전용 데이터 업로드
}
```

Unity 전용 참조는 DB로 보내지 않는다.
- Icon
- AnimationClip
- VFX Prefab
- Hitbox Prefab
- SoundClip
- 그 밖의 UnityEngine.Object 참조


8. DB Bake 방향

관련 파일:
- Assets/02. Scripts/DB/Editor/AbilityBakeWindow.cs
- Assets/02. Scripts/DB/AbilityManager.cs

Bake 저장 위치:
- Active: Assets/02. Scripts/Player/Abilities/Resources/SkillModule/ActiveSkill
- Passive: Assets/02. Scripts/Player/Abilities/Resources/SkillModule/PassiveSkill
- Utility: Assets/02. Scripts/Player/Abilities/Resources/SkillModule/UtilitySkill

진행 과정:
1) 서버의 get_abilities.php 또는 새 JSON API를 호출한다.
2) ability_id와 같은 이름의 SkillModule 에셋을 찾는다.
3) 기존 에셋이면 InitializeFromDB()로 서버 수치를 갱신한다.
4) 에셋이 없으면 ability_type에 따라 새 타입을 생성하고 타입별 폴더에 저장한다.
5) AssetDatabase.SaveAssets()로 저장한다.

생성 타입:
- Active -> ActiveAbilityModule
- Passive -> PassiveAbilityModule
- Utility -> UtilityAbilityModule

Bake가 갱신하는 값:
- 에셋 이름
- BitIndex
- AbilityId
- DisplayName
- Description
- BasicSkill
- Active: StaminaCost, CooldownSeconds, HitboxLifetime
- Utility: StaminaCost, CooldownSeconds, SpecialEffect

Bake 후에도 유지되는 값:
- Icon
- AnimationClip / Trigger / StateName
- VFX Prefab과 위치
- Hitbox Prefab과 세부 HitEvents
- SoundClip
- 서버 응답에 없는 Unity 전용 데이터


9. 게임 시작: AbilityManager 로컬 초기화

관련 파일:
- Assets/02. Scripts/DB/AbilityManager.cs

진행 과정:
1) AbilityManager.Awake()가 LoadLocalAbilityCatalog()를 호출한다.
2) Resources/SkillModule 아래 ActiveSkill, PassiveSkill, UtilitySkill 폴더의 스킬 에셋을 읽는다.
3) AbilityId와 BitIndex 기준 딕셔너리를 만든다.
4) 하나 이상의 모듈이 등록되면 IsLoaded가 true가 된다.

새 타입들도 PlayerAbilityModule을 상속하므로 Resources.LoadAll<PlayerAbilityModule>()에 함께 잡힌다.

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


10. 로그인: 서버 최신 데이터 적용

관련 파일:
- Assets/02. Scripts/Login/LoginSceneController.cs
- Assets/02. Scripts/DB/BackendManager.cs
- Assets/02. Scripts/DB/AbilityManager.cs

진행 과정:
1) BackendManager.LoginUser()가 로그인한다.
2) 유저 정보와 CurrentSkillsBitmask를 저장한다.
3) LoginSceneController가 AbilityManager.FetchAbilities()를 호출한다.
4) AbilityManager가 서버에서 전체 스킬 수치를 받는다.
5) ability_id로 로컬 SkillModule을 찾는다.
6) InitializeFromDB()로 메모리상의 서버 관리 수치를 갱신한다.
7) 변경된 BitIndex에 맞춰 카탈로그 인덱스를 다시 만든다.
8) 유저 비트마스크를 해석해 보상 가능 스킬을 갱신한다.
9) 완료 후 다음 씬으로 이동한다.

Bake와 FetchAbilities의 차이:
- Bake는 .asset 파일을 실제로 저장한다.
- FetchAbilities는 실행 중 메모리상의 모듈만 갱신한다.


11. 보상 후보 생성과 레벨업

관련 파일:
- Assets/02. Scripts/Player/Abilities/PlayerAbilityInventory.cs
- Assets/02. Scripts/Player/Core/NetworkPlayerData.cs
- Assets/02. Scripts/Player/Core/PlayerSessionStore.cs

규칙:
- 처음 획득: Lv.1
- 동일 스킬 재획득: Lv.2 -> Lv.3 -> ... -> MaxLevel
- MaxLevel에 도달한 스킬은 보상 후보에서 제외한다.
- 같은 방에서 씬 이동 시 abilityId + level을 유지한다.
- 새 매칭 시작 전 PlayerSessionStore.ClearAll()로 초기화한다.

PlayerAbilityInventory는 PlayerAbilityModule 공통 타입으로 저장한다.
실제 동작 차이는 Module의 런타임 API와 타입 검사로 처리한다.


12. 스킬 사용 흐름

관련 파일:
- Assets/02. Scripts/Player/Abilities/PlayerAbilityController.cs
- Assets/02. Scripts/Player/Abilities/PlayerAbilityExecutor.cs
- Assets/02. Scripts/Player/Abilities/PlayerAbilitySlot.cs

흐름:
1) PlayerAbilityController가 입력을 받는다.
2) PlayerAbilitySlot에서 모듈과 쿨타임을 확인한다.
3) module.UsesActiveSlot이 false면 사용하지 않는다.
4) 스태미나를 검사한다.
5) 서버 권한에서 사용을 확정한다.
6) PlayerAbilityExecutor가 모듈 타입에 따라 실행한다.

타입별 실행:
- Active: HitEvents 기반 공격 판정 실행
- Passive: 획득/레벨업 시 스탯 증가 적용
- Utility + SpecialEffect 없음: 회복형 액티브 슬롯 스킬처럼 사용 가능
- Utility + SpecialEffect 있음: 획득 시 특수 효과 적용


13. 전투 데미지 연결

Active 스킬은 HitEvents를 기준으로 공격 판정을 만든다.

최종 데미지 기본 흐름:
```text
기본 공격력
x HitEvent.damageRate
x ActiveAbilityLevelData.damageMultiplier
x 패시브 공격력 증가
x 버프/디버프
x 보스 방어 보정
```

`damageMultiplier`는 스킬 전체에 남아 있는 낡은 HitboxDamage가 아니다.
현재는 레벨별 스킬 배율이며, HitEvent의 damageRate와 함께 사용된다.


14. 설명 토큰

설명은 고정 숫자를 직접 적기보다 토큰을 사용한다.

예:
```text
전방으로 도약하여 {hit1}배의 데미지를 준다
스태미나 {staminaCost} 소모, 쿨타임 {cooldown}초
```

표시 단계에서 현재 레벨의 값으로 치환한다.
레벨 수치가 바뀌어도 설명 문장을 매번 수정하지 않아도 된다.


15. 에디터 도구

관련 파일:
- Assets/02. Scripts/Player/Editor/PlayerAbilityModuleEditor.cs
- Assets/02. Scripts/Player/Editor/PlayerAbilityAssetSearch.cs
- Assets/02. Scripts/Player/Editor/PlayerAbilityPoolSetupTool.cs
- Assets/02. Scripts/Player/Editor/PlayerAnimatorSetupTool.cs

주의:
- 에디터 자동화 도구는 이제 `t:PlayerAbilityModule` 단일 검색이 아니라 Active/Passive/Utility 세 타입을 모두 검색해야 한다.
- 이를 위해 PlayerAbilityAssetSearch가 세 타입 검색을 묶어서 제공한다.


16. 체크리스트

새 Active 스킬:
1) Active Ability 생성
2) AbilityId / BitIndex 설정
3) Cooldown / Stamina 설정
4) LevelSettings.damageMultiplier 설정
5) Animation / VFX / Sound 연결
6) HitEvents 설정
7) 보상 후보에서 등장할 AppearStage 확인
8) 필요 시 DB Upload/Bake
9) SkillModule 변경 Git 포함

새 Passive 스킬:
1) Passive Ability 생성
2) AbilityId / BitIndex 설정
3) LevelSettings 스탯 증가값 설정
4) 획득 연출용 Animation / VFX / Sound 연결
5) AppearStage 확인
6) 필요 시 DB Upload/Bake
7) SkillModule 변경 Git 포함

새 Utility 스킬:
1) Utility Ability 생성
2) AbilityId / BitIndex 설정
3) SpecialEffect 또는 회복 수치 설정
4) 필요하면 Cooldown / Stamina 설정
5) Animation / VFX / Sound 연결
6) AppearStage 확인
7) 필요 시 DB Upload/Bake
8) SkillModule 변경 Git 포함


17. 문제 확인 포인트

스킬이 보상 후보에 안 뜰 때:
- SkillModule이 Resources/SkillModule의 타입별 폴더 아래에 있는가?
- AbilityId가 비어 있거나 중복되지 않았는가?
- BitIndex가 중복되지 않았는가?
- BitIndex가 타입별 범위에 들어가는가? Active 1~19, Passive 20~39, Utility 40~60.
- UnlockedSkill 또는 유저 비트마스크 조건을 만족하는가?
- AppearStage 조건을 만족하는가?
- 이미 MaxLevel에 도달한 스킬은 아닌가?

스킬 사용이 안 될 때:
- module.UsesActiveSlot이 true인가?
- Active 또는 사용형 Utility인가?
- PlayerAbilitySlot에 들어갔는가?
- 쿨타임 중은 아닌가?
- 스태미나가 충분한가?
- 서버 권한에서 사용 요청이 거절되지 않았는가?

공격 데미지가 이상할 때:
- ActiveAbilityModule인지 확인한다.
- HitEvents의 damageRate를 확인한다.
- LevelSettings.damageMultiplier를 확인한다.
- 패시브 공격력 증가가 중복 적용되지 않았는지 확인한다.

인스펙터에 이상한 필드가 보일 때:
- 에셋의 실제 타입이 ActiveAbilityModule / PassiveAbilityModule / UtilityAbilityModule 중 맞는지 확인한다.
- 예전 PlayerAbilityModule 단일 에셋 GUID를 물고 있는지 확인한다.
- Unity 리프레시 후에도 이상하면 해당 .asset의 m_Script GUID를 확인한다.
