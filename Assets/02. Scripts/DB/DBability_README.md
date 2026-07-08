📢 [필독] Soul Rush 스킬 시스템 사용 가이드 (Data-Driven)
이제 우리 게임의 모든 스킬(이름, 데미지, 쿨타임, 이펙트)은 관리자 웹사이트(Admin Hub)에서 중앙 통제됩니다. 유니티에서 수동으로 스킬 프리팹을 만들거나 수치를 고칠 필요가 없습니다!

## 현재 스킬 시스템 변경 방향

현재 Unity 쪽 스킬 구조는 다음 기준으로 정리합니다.

- 스킬은 Active / Passive / Utility 3종류로 분류합니다.
- Effect 분류명은 Utility로 변경합니다.
- Active 스킬의 레벨 배율은 스킬 전체 데미지에 직접 곱하는 값이 아니라, 각 Hit Event의 Damage Multiplier에 곱해지는 레벨별 배율로 사용합니다.
- Passive는 레벨별 최종 증가값을 사용합니다. 레벨업할 때 차감분을 누적 적용하는 방식이 아니라, 현재 레벨의 값을 기준으로 다시 적용하는 구조가 안전합니다.
- Utility는 회복, 기본공격 해금, 특수 기능처럼 전투 데미지와 직접 연결되지 않는 기능성 스킬을 담당합니다.
- 쿨타임과 스태미나 소모량은 레벨에 따라 변하지 않는 기본값으로 둡니다.
- 스킬 설명은 DB에 완성 문장으로 저장하기보다, Unity 표시 단계에서 토큰을 해석해 현재 레벨 수치가 반영되도록 처리합니다.
- DB Bake / Upload 쪽은 별도 담당 영역이므로, Unity 로컬 테스트 중에는 실제 적용 로직과 에디터 표시만 우선 정리합니다.

## DB 쪽에서 수정해야 할 권장 테이블 구조

레벨별 수치를 관리자 사이트에서 자주 조정할 예정이라면, 레벨 데이터는 별도 테이블로 분리하는 편이 좋습니다. 테이블 수는 늘어나지만, 특정 레벨만 수정하거나 검증하기 쉬워지고 Unity로 내려줄 JSON 응답도 만들기 쉬워집니다.

권장 구조는 총 7개입니다.

- abilities
- active_abilities
- active_ability_levels
- passive_abilities
- passive_ability_levels
- utility_abilities
- utility_ability_levels

### abilities

모든 스킬이 공통으로 가지는 기본 정보만 둡니다.

- ability_id
- ability_type
- display_name
- description_template
- icon_key
- bit_index
- appear_stage
- is_basic_skill
- is_unlocked
- max_level

여기에는 cooldown_seconds, stamina_cost, special_effect 같은 타입별 값은 넣지 않습니다.

### active_abilities

Active 스킬의 레벨과 무관한 기본 사용값만 둡니다.

- ability_id
- cooldown_seconds
- stamina_cost

### active_ability_levels

Active 스킬의 레벨별 배율만 둡니다.

- ability_id
- level
- skill_multiplier

이 값은 Hit Event의 Damage Multiplier에 곱해지는 레벨 배율입니다.

### passive_abilities

현재 Passive는 레벨과 무관한 기본값이 따로 없으면 ability_id만 두거나, 추후 확장용으로 비워둘 수 있습니다.

- ability_id

### passive_ability_levels

Passive 스킬의 레벨별 최종 증가값을 둡니다.

- ability_id
- level
- health
- stamina
- defense
- attack

공격력 증가, 방어력 증가 같은 값은 10을 입력하면 10% 증가로 해석하는 기준을 권장합니다.

### utility_abilities

Utility 스킬의 레벨과 무관한 기본값을 둡니다.

- ability_id
- cooldown_seconds
- stamina_cost
- special_effect

쿨타임이나 스태미나가 필요 없는 Utility라면 0 또는 null 허용 기준을 DB 쪽에서 정하면 됩니다.

### utility_ability_levels

Utility 스킬의 레벨별 수치만 둡니다.

- ability_id
- level
- health_restore
- stamina_restore

기본공격 해금처럼 레벨별 회복 수치가 필요 없는 Utility라면 0 또는 null 허용 기준을 사용합니다.

### DB에서 검증하면 좋은 규칙

- ability_id + level 조합은 중복되지 않아야 합니다.
- level은 1 이상 max_level 이하만 허용합니다.
- max_level에 도달한 스킬은 보상 후보에서 제외할 수 있어야 합니다.
- bit_index는 중복되지 않아야 합니다.
- ability_type에 맞는 타입별 테이블과 레벨 테이블이 존재해야 합니다.
- appear_stage는 “몇 스테이지부터 등장” 기준으로 사용합니다.

### Unity로 내려주는 데이터

DB 내부 저장은 위처럼 정규화된 테이블로 관리하고, Unity 응답은 JSON으로 묶어서 내려주는 방식을 권장합니다.

이 방식의 장점은 다음과 같습니다.

- 사이트에서 레벨별 행 수정이 쉽습니다.
- 특정 레벨만 업데이트할 수 있습니다.
- 잘못된 레벨이나 중복 레벨 검증이 쉽습니다.
- Unity 응답 JSON 생성이 단순합니다.
- 나중에 스킬마다 최대 레벨이 달라져도 컬럼 추가가 필요 없습니다.

👩‍💻 기획자 & 아티스트 (작업 흐름)
1. 스킬 만들기 & 수치 밸런싱

admin_hub.php에 접속해서 [⚔️ 스킬 모듈 관리]로 들어갑니다.

새로운 스킬의 스탯(데미지, 쿨타임, 아이콘 키값 등)을 입력하고 저장합니다.

(중요) 수치를 바꾸고 싶을 때도 여기서 수정만 하면 끝입니다.

2. 유니티에 스킬 가져오기 (Bake)

유니티 에디터를 열고 상단 메뉴에서 Soul Rush -> ⚔️ 스킬 DB 동기화 (Bake)를 누릅니다.

[🚀 스킬 동기화 실행] 버튼을 누르면, 웹에서 만든/수정한 스킬들이 Assets/Resources/GeneratedAbilities 폴더에 .asset 파일로 짠! 하고 생겨납니다.

3. 스킬 사용하기

이제 생성된 .asset 파일을 보스 몬스터의 드랍 테이블이나, 인벤토리 초기 지급 리스트에 마우스로 드래그 앤 드롭해서 마음껏 쓰시면 됩니다.

👨‍💻 프로그래머 (코드 활용법)
프로그래머분들은 이제 하드코딩된 모듈 대신 AbilityManager가 관리하는 살아있는 데이터를 가져다 써야 합니다.

1. "내가 가진 스킬 목록" 가져오기 (UI 갱신할 때)
유저가 로그인하면 서버에서 알아서 최신 스킬 패치 내역을 받아옵니다. 여러분은 현재 유저의 '비트마스크'를 던져주고, 해금된 스킬(SO) 리스트만 받아오면 됩니다.

C#
// 현재 로그인한 유저의 비트마스크(예: 5) 가져오기
long myBitmask = BackendManager.Instance.CurrentSkillsBitmask;

// 비트마스크를 해독해서, 내가 진짜 가진 스킬 파일(.asset)들만 List로 뽑아줌!
List<PlayerAbilityModule> myUnlockedSkills = AbilityManager.Instance.GetUnlockedAbilitiesList(myBitmask);

// UI에 그리기
foreach (PlayerAbilityModule skill in myUnlockedSkills)
{
    Debug.Log($"스킬 이름: {skill.DisplayName}, 데미지: {skill.HitboxDamage}");
    // slot.SetIcon(skill.Icon);
}
2. 특정 스킬 하나만 콕 집어서 가져오기
"0번 비트에 있는 파이어볼 스킬 데이터 좀 줘!" 할 때는 딕셔너리에서 바로 꺼내 씁니다.

C#
int bitIndex = 0; // 파이어볼의 고유 인덱스

if (AbilityManager.Instance.AllAbilitiesDict.TryGetValue(bitIndex, out PlayerAbilityModule fireSkill))
{
    // 여기서 fireSkill.CooldownSeconds 등을 읽어서 사용!
}
💡 핵심 주의사항
🚨 절대 유니티 인스펙터에서 스킬 .asset 파일의 수치(데미지, 쿨타임 등)를 수동으로 수정하지 마세요.

어차피 게임을 실행(Play)하는 순간, 서버(DB)에 적힌 최신 수치로 알아서 덮어씌워집니다. (라이브 패치 적용)

무언가 수정하고 싶다면 반드시 관리자 웹사이트(Admin Hub)에서 고친 뒤, 유니티에서 Bake 버튼을 한 번 눌러주세요.

📢 [플레이어 스킬 연동 가이드]

플레이어 전투 로직 짜실 때 스킬 데이터는 직접 하드코딩하지 마시고 아래 방법대로 연동해 주세요! 서버 데이터 기반으로 라이브 연동 다 끝내놨습니다.

1. 테스트할 때 스킬 세팅법
유니티 상단 메뉴에 [Soul Rush] -> [스킬 DB 동기화] 툴 만들어 놨습니다. 버튼 한 번 누르면 서버에 있는 스킬 수치들이 Assets/Resources/GeneratedAbilities 폴더 안에 .asset 파일로 쫙 구워집니다.
플레이어 프리팹이나 스킬 슬롯 인스펙터에 이 파일들을 드래그 앤 드롭해서 꽂아놓고 테스트하시면 됩니다!

2. 실제 코드에서 스킬 수치 꺼내 쓰는 법
PlayerAbilityModule에 들어있는 변수들을 그대로 가져다 쓰시면 됩니다.

C#
// [플레이어 전투 스크립트 예시]
public class PlayerCombat : MonoBehaviour
{
    // 인스펙터에 아까 구운 스킬 파일(.asset)을 드래그해서 넣거나, 
    // 장착 UI에서 넘겨받은 모듈을 여기에 할당합니다.
    public PlayerAbilityModule equippedSkill; 

    public void UseSkill()
    {
        // 1. 스태미나가 충분한지 검사
        if (PlayerStats.CurrentStamina < equippedSkill.StaminaCost) return;

        // 2. 공격 애니메이션 실행
        animator.Play(equippedSkill.AnimationStateName);

        // 3. 데미지 계산 및 히트박스 생성 (서버 수치 그대로 적용됨!)
        float finalDamage = CalculateDamage(equippedSkill.HitboxDamage);
        
        // (이펙트, 사운드 등도 equippedSkill 안에 다 들어있습니다)
    }
}
※ 주의사항: > 테스트하다가 데미지나 쿨타임을 바꾸고 싶으면 절대 유니티 인스펙터에서 직접 숫자를 고치지 마세요. 어차피 게임 실행하면 서버 최신 수치로 알아서 덮어씌워집니다.
수정이 필요하면 저한테 말씀해 주시면 DB 웹에서 바로 고쳐드리겠습니다! (고치고 Bake 툴 한 번만 다시 돌리면 끝납니다.)
