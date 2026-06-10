📢 [필독] Soul Rush 스킬 시스템 사용 가이드 (Data-Driven)
이제 우리 게임의 모든 스킬(이름, 데미지, 쿨타임, 이펙트)은 관리자 웹사이트(Admin Hub)에서 중앙 통제됩니다. 유니티에서 수동으로 스킬 프리팹을 만들거나 수치를 고칠 필요가 없습니다!

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