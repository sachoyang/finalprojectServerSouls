Cutscene README

목적:
- 보스 등장, 보상 상자, 문 열기 컷신의 카메라 연출과 플레이어 조작 잠금 흐름을 설명한다.


1. CutsceneManager.cs

역할:
- CameraManager를 통해 컷신 카메라로 전환한다.
- CameraPointManager의 카메라 포인트를 기준으로 줌/복귀 연출을 실행한다.
- 보스 등장 컷신, 골드 상자 컷신, 문 발차기 컷신을 관리한다.
- 컷신 중 플레이어 조작은 NetworkPlayerController.SetControlLock()으로 잠근다.

주요 함수:
- PlayBossWakeUpCutscene(): 보스 등장 컷신 시작.
- PlayGoldChestCutscene(): 보상 상자 컷신 코루틴.
- RestoreGameplayCamera(): 게임플레이 카메라로 복귀.
- PlayGateKickCutscene(): 문 발차기 컷신 시작.
- PlayGateKickCutscene(NetworkRunner runner, PlayerRef kickPlayer): 특정 플레이어를 발차기 연출 대상으로 지정.

씬 전환:
- CutsceneManager는 씬을 전환하지 않는다.
- 문 발차기 컷신과 동시에 시작하는 Fusion 씬 전환은 NextLevelPortal이 담당한다.

조작 잠금:
- SetPlayerControlEnabled(false)는 내부적으로 PlayerControlLockFlags.All을 잠근다.
- SetPlayerControlEnabled(true)는 PlayerControlLockFlags.All을 해제한다.
- 컷신 매니저는 이동, 스킬, 기본 액션 컴포넌트를 직접 끄지 않는다.
- 새 컷신을 추가할 때도 각 기능 컴포넌트를 찾아서 끄지 말고 SetControlLock()만 호출한다.


2. CameraManager 연동

호출 흐름:
- BeginCutscene(): 카메라 컨트롤러를 컷신 모드로 전환.
- ZoomToPoint(): 지정된 컷신 포인트로 이동.
- RestoreGameplayCamera(): 플레이어 추적 카메라로 복귀.


3. 컴포넌트 분리 기준

- enum, 데이터 구조, 순수 helper 클래스를 별도 스크립트로 분리해도 프리팹에 컴포넌트를 추가할 필요가 없다.
- MonoBehaviour나 NetworkBehaviour로 새 책임을 분리할 때만 씬/프리팹에 컴포넌트를 추가해야 한다.
- 중앙 조작 잠금은 현재 NetworkPlayerController가 보유한다.
