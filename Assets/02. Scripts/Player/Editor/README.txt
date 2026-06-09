Player/Editor README

목적:
- Player 관련 에디터 도구 설명.
- 런타임 게임 로직에는 직접 포함되지 않는다.


1. PlayerAbilityModuleEditor.cs

역할:
- PlayerAbilityModule 전용 Inspector.
- 보상, 액티브 설정, 효과, 패시브 스탯, 애니메이션, VFX, 히트박스를 섹션별로 편집한다.
- 미리보기 뷰에서 애니메이션/VFX/히트박스 타이밍을 확인할 수 있다.

주요 기능:
- abilityId, displayName, description, icon 편집.
- Passive/Active 설정 편집.
- 스태미나 비용, 쿨타임, 회복량 편집.
- 패시브 보너스 편집.
- 애니메이션 클립, 상태명, 트리거 편집.
- VFX prefab과 offset 편집.
- hitbox prefab, damage, revivePower, delay, lifetime 편집.

주의:
- Editor 전용 스크립트다.
- 빌드된 게임의 실행 흐름에는 직접 영향이 없다.


2. PlayerAbilityPoolSetupTool.cs

역할:
- PlayerAbilityModule 에셋을 찾아 PlayerAbilityInventory.abilityPool에 등록하는 도구.

사용 의도:
- SkillModule 폴더에 스킬 SO를 만든 뒤 Player prefab의 abilityPool에 일일이 넣는 작업을 줄인다.

주의:
- DB 연결 전에는 abilityId가 빈 모듈이 abilityPool에 들어가지 않도록 검증하는 기능을 추가하는 것이 좋다.


3. PlayerAnimatorSetupTool.cs

역할:
- Player Animator에 필요한 상태/트리거/StateMachineBehaviour 설정을 돕는 도구.

사용 의도:
- PlayerAbilityModule의 animationClip, animationStateName, animationTrigger 설정을 Animator와 맞추는 작업을 자동화한다.

주의:
- Animator 구조를 자동으로 바꾸는 도구는 변경 범위가 크므로 실행 전 prefab/animator diff를 확인하는 것이 좋다.
