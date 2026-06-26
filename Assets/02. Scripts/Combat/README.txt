Combat README

목적:
- 플레이어, 보스, 몬스터, 죽은 플레이어가 함께 사용하는 전투 규칙을 Player 폴더에서 분리해 관리한다.
- Player는 입력과 상태를 들고, Combat은 타겟 선택/히트 판정/전투 공통 규칙을 담당하는 방향으로 정리한다.


1. LockOn/LockOnTargetSelector.cs

역할:
- Q 입력 시 카메라 시야 안의 락온 후보를 찾고, 화면 중심에 가장 가까운 대상부터 선택한다.
- Q를 다시 누르면 현재 대상 Root 안의 LockOnTargetPoint를 priority 순서대로 먼저 순회하고, 그 다음 대상 Root로 넘어간다.
- E 해제는 NetworkPlayerController가 처리하고, selector는 내부 순회 상태만 Clear()로 비운다.

후보 수집:
- LockOnTargetRoot가 붙은 대상만 락온 후보로 사용한다.
- 각 Root 아래의 LockOnTargetPoint만 실제 조준 위치로 사용한다.
- 태그 기반 LockOnHead/LockOnBody/DeadPlayer 탐색은 사용하지 않는다.
- PlayerStats가 붙은 대상은 IsDead가 true일 때만 락온 후보가 된다.


2. LockOn/LockOnTargetRoot.cs

역할:
- 락온 가능한 대상의 루트 표시자.
- 전투 시스템이 NetworkBossCore, PlayerStats 같은 구체 타입을 직접 몰라도 대상 단위를 구분할 수 있게 한다.

사용:
- 보스/일반 몬스터/죽은 플레이어 루트에 붙인다.
- targetable을 끄면 런타임에서 락온 후보에서 제외된다.
- 플레이어 루트에 붙인 경우, 플레이어가 살아있으면 selector에서 자동 제외된다.


3. LockOn/LockOnTargetPoint.cs

역할:
- 실제 카메라와 플레이어가 바라볼 락온 위치.
- priority가 낮을수록 같은 대상 안에서 우선 선택된다.

권장:
- 머리 포인트 priority 0.
- 몸통 포인트 priority 10.
- 포인트가 없는 대상은 락온 후보가 되지 않으므로, 대상마다 최소 하나의 Point를 둔다.
