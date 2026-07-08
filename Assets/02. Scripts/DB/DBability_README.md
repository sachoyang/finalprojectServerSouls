# Soul Rush 스킬 DB 연동 가이드

현재 Unity 스킬 모듈은 단일 `PlayerAbilityModule` 에셋이 아니라 타입별 ScriptableObject로 분리되어 있습니다.

- `ActiveAbilityModule`
- `PassiveAbilityModule`
- `UtilityAbilityModule`

세 타입 모두 공통 부모는 `PlayerAbilityModule`입니다. 그래서 런타임에서는 기존처럼 `PlayerAbilityModule`로 조회하고, 실제 저장되는 필드는 타입별 모듈에 필요한 것만 가집니다.

## Unity 에셋 생성 방식

Project 창에서 새 스킬을 만들 때 아래 메뉴 중 하나를 선택합니다.

- `Create > ServerSouls > Player Modules > Active Ability`
- `Create > ServerSouls > Player Modules > Passive Ability`
- `Create > ServerSouls > Player Modules > Utility Ability`

처음 만들 때 타입을 선택하는 구조입니다. 이후 인스펙터에서 `Ability Type`을 바꾸는 방식이 아닙니다.

## 타입별 Unity 데이터 범위

### 공통: `PlayerAbilityModule`

모든 스킬이 가지는 값입니다.

- `bitIndex`
- `abilityId`
- `displayName`
- `description`
- `icon`
- `appearStage`
- `unlockedSkill`
- `basicSkill`
- `maxLevel`

### Active: `ActiveAbilityModule`

공격/사용형 스킬입니다.

- `staminaCost`
- `cooldownSeconds`
- `levelSettings.damageMultiplier`
- 애니메이션
- VFX
- 사운드
- 공격 판정 `hitEvents`

Active의 레벨 배율은 최종 데미지를 직접 대체하는 값이 아니라, 각 Hit Event의 `damageRate`에 곱해지는 레벨별 배율입니다.

### Passive: `PassiveAbilityModule`

획득/레벨업 시 플레이어 스탯을 올리는 스킬입니다.

- `levelSettings.maxHealthBonus`
- `levelSettings.maxStaminaBonus`
- `levelSettings.defenseBonusPercent`
- `levelSettings.attackDamageBonusPercent`
- 애니메이션
- VFX
- 사운드

Passive도 획득 연출이 필요할 수 있으므로 애니메이션/VFX/사운드는 유지합니다. 대신 쿨타임, 스태미나 소모량, 공격 판정, 회복량은 가지지 않습니다.

공격력 증가와 방어력 증가는 퍼센트 입력 기준입니다.

- `10` 입력 = 10%
- `100` 입력 = 100%

### Utility: `UtilityAbilityModule`

회복, 기본 공격 해금, 기능성 효과를 담당합니다.

- `staminaCost`
- `cooldownSeconds`
- `specialEffect`
- `levelSettings.healthRestoreAmount`
- `levelSettings.staminaRestoreAmount`
- 애니메이션
- VFX
- 사운드

기본 공격 해금처럼 쿨타임/스태미나/레벨별 회복량이 필요 없는 Utility는 해당 값을 `0`으로 둡니다.

## 권장 DB 테이블 구조

Unity 모듈이 타입별로 분리되었으므로 DB도 타입별로 분리하는 구조를 권장합니다.

총 7개 테이블 구조입니다.

- `abilities`
- `active_abilities`
- `active_ability_levels`
- `passive_abilities`
- `passive_ability_levels`
- `utility_abilities`
- `utility_ability_levels`

## `abilities`

모든 스킬의 공통 정보만 저장합니다.

| 컬럼 | 설명 |
| --- | --- |
| `ability_id` | 스킬 고유 ID, PK |
| `ability_type` | `Active`, `Passive`, `Utility` |
| `bit_index` | 해금 비트마스크 인덱스 |
| `display_name` | 표시 이름 |
| `description_template` | 토큰 포함 설명문 |
| `appear_stage` | 몇 스테이지부터 등장할지 |
| `is_unlocked` | 기본 해금 여부 |
| `is_basic_skill` | 기본 스킬 여부 |
| `max_level` | 최대 레벨 |

여기에는 쿨타임, 스태미나, 특수효과, 레벨별 수치를 넣지 않습니다.

### bit_index 범위 규칙

스킬 타입별로 bit_index 범위를 분리합니다.

| 타입 | bit_index 범위 |
| --- | --- |
| Active | 1 ~ 19 |
| Passive | 20 ~ 39 |
| Utility | 40 ~ 60 |

Unity 에디터에서는 새 SkillModule 에셋의 bitIndex가 0이면 타입별 범위에서 다음 빈 번호를 자동 할당합니다.

DB의 `abilities.bit_index`도 이 범위 규칙과 동일하게 관리해야 합니다. 이미 운영 중인 계정의 해금 비트마스크가 있다면 bit_index 재배치는 기존 저장값과 호환이 깨질 수 있으므로, DB 마이그레이션 또는 초기화 기준을 먼저 정해야 합니다.

## `active_abilities`

Active 스킬의 레벨과 무관한 사용값만 저장합니다.

| 컬럼 | 설명 |
| --- | --- |
| `ability_id` | `abilities.ability_id` FK |
| `cooldown_seconds` | 쿨타임 |
| `stamina_cost` | 스태미나 소모량 |

## `active_ability_levels`

Active 스킬의 레벨별 배율을 저장합니다.

| 컬럼 | 설명 |
| --- | --- |
| `ability_id` | `abilities.ability_id` FK |
| `level` | 레벨 |
| `skill_multiplier` | Hit Event damageRate에 곱할 배율 |

권장 고유키:

```text
ability_id + level
```

## `passive_abilities`

현재 Passive는 레벨과 무관한 기본값이 없습니다. 그래도 타입별 테이블을 유지하면 구조가 일관됩니다.

| 컬럼 | 설명 |
| --- | --- |
| `ability_id` | `abilities.ability_id` FK |

## `passive_ability_levels`

Passive 스킬의 레벨별 최종 증가값을 저장합니다.

| 컬럼 | 설명 |
| --- | --- |
| `ability_id` | `abilities.ability_id` FK |
| `level` | 레벨 |
| `max_health_bonus` | 최대 체력 증가 |
| `max_stamina_bonus` | 최대 스태미나 증가 |
| `defense_bonus_percent` | 방어력 증가율 |
| `attack_damage_bonus_percent` | 공격력 증가율 |

레벨업 시 “이번 레벨에서 더해질 차이값”이 아니라 “현재 레벨의 최종값”을 저장합니다. Unity 런타임에서 이전 레벨값과 새 레벨값의 차이를 계산해 적용합니다.

## `utility_abilities`

Utility 스킬의 레벨과 무관한 기본값을 저장합니다.

| 컬럼 | 설명 |
| --- | --- |
| `ability_id` | `abilities.ability_id` FK |
| `cooldown_seconds` | 쿨타임 |
| `stamina_cost` | 스태미나 소모량 |
| `special_effect` | 특수 효과 enum 이름 |

## `utility_ability_levels`

Utility 스킬의 레벨별 회복 수치를 저장합니다.

| 컬럼 | 설명 |
| --- | --- |
| `ability_id` | `abilities.ability_id` FK |
| `level` | 레벨 |
| `health_restore_amount` | 체력 회복량 |
| `stamina_restore_amount` | 스태미나 회복량 |

기본 공격 해금처럼 회복 수치가 없는 Utility는 0으로 저장하면 됩니다.

## Unity로 내려줄 JSON 권장 구조

DB는 테이블을 나눠 관리하더라도 Unity 응답은 JSON으로 묶어서 내려주는 편이 좋습니다.

예시:

```json
{
  "status": "success",
  "data": [
    {
      "ability_id": "jump_attack",
      "ability_type": "Active",
      "bit_index": 6,
      "display_name": "리프 어택",
      "description": "전방으로 도약하여 {hit1}배의 데미지를 준다",
      "appear_stage": 1,
      "basic_skill": 1,
      "unlocked_skill": 1,
      "max_level": 4,
      "active": {
        "cooldown_seconds": 8,
        "stamina_cost": 400
      },
      "levels": [
        { "level": 1, "skill_multiplier": 1.0 },
        { "level": 2, "skill_multiplier": 1.2 },
        { "level": 3, "skill_multiplier": 1.4 },
        { "level": 4, "skill_multiplier": 1.6 }
      ]
    }
  ]
}
```

서버 내부 테이블이 7개여도 Unity는 위처럼 한 스킬 단위로 묶인 JSON을 받으면 됩니다.

## Upload 방향

Unity에서 DB로 업로드할 때도 이제 타입별로 나눠 보내야 합니다.

```csharp
if (module is ActiveAbilityModule active)
{
    // abilities + active_abilities + active_ability_levels
}
else if (module is PassiveAbilityModule passive)
{
    // abilities + passive_abilities + passive_ability_levels
}
else if (module is UtilityAbilityModule utility)
{
    // abilities + utility_abilities + utility_ability_levels
}
```

현재 `AbilityUploadWindow`는 예전 단일 모듈 업로드 형식이 남아 있습니다. DB 업로드 기능을 다시 사용할 때는 위 타입별 구조에 맞게 수정해야 합니다.

## Bake 방향

Bake는 서버 JSON을 받아 `Assets/02. Scripts/Player/Abilities/Resources/SkillModule` 아래 타입별 폴더의 에셋을 갱신합니다.

저장 폴더:

```text
Assets/02. Scripts/Player/Abilities/Resources/SkillModule
├─ ActiveSkill
├─ PassiveSkill
└─ UtilitySkill
```

현재 구조에서는 `ability_type`에 따라 생성 타입이 달라져야 합니다.

- `Active` → `ActiveAbilityModule`
- `Passive` → `PassiveAbilityModule`
- `Utility` → `UtilityAbilityModule`

새 에셋을 만들 때는 `ability_type`에 따라 위 타입별 폴더에 저장합니다.

기존 에셋이 있으면 같은 `ability_id`의 에셋을 찾아 수치만 갱신합니다. 애니메이션, VFX, 사운드, 프리팹 참조 같은 Unity 전용 데이터는 DB에서 내려오지 않으므로 로컬 에셋에 유지합니다.

## 설명 토큰

설명은 DB에 완성된 숫자 문장으로 박아두기보다 토큰 문장으로 저장하는 방식을 권장합니다.

예시:

```text
전방으로 도약하여 {hit1}배의 데미지를 준다
스태미나 {staminaCost} 소모, 쿨타임 {cooldown}초
```

Unity 표시 단계에서 현재 레벨과 모듈 값을 기준으로 토큰을 치환합니다. 그러면 레벨별 수치가 바뀌어도 설명 문구를 매번 다시 쓰지 않아도 됩니다.

## 검증 규칙

DB 또는 Admin 사이트에서 아래 규칙을 검증하는 것이 좋습니다.

- `ability_id`는 중복되면 안 됩니다.
- `bit_index`는 중복되면 안 됩니다.
- `bit_index`는 타입별 범위 규칙을 지켜야 합니다.
- `ability_id + level` 조합은 중복되면 안 됩니다.
- `level`은 1 이상 `max_level` 이하만 허용합니다.
- `ability_type`에 맞는 타입별 기본 테이블과 레벨 테이블 행이 있어야 합니다.
- `special_effect`는 Utility에서만 사용합니다.
- `appear_stage`는 “몇 스테이지부터 등장” 기준입니다.

## 런타임 연결

런타임에서는 여전히 `PlayerAbilityModule` 공통 타입으로 다룹니다.

- `AbilityManager`는 `Resources/SkillModule/ActiveSkill`, `PassiveSkill`, `UtilitySkill` 세 폴더에서 모듈을 로드합니다.
- `PlayerAbilityInventory`는 `PlayerAbilityModule` 리스트로 장착/레벨을 관리합니다.
- `PlayerAbilityExecutor`는 `module.IsActive`, `module.IsPassive`, `module.IsUtility`, `module.UsesActiveSlot` 기준으로 실행 방식을 나눕니다.
- `PlayerStats`는 Passive 모듈의 레벨별 최종 증가값을 읽어 이전 레벨과 새 레벨의 차이만 적용합니다.

즉, 에셋 저장 구조는 타입별로 분리되었지만 전투/인벤토리 연결은 공통 부모 타입으로 유지됩니다.
