Player/Camera README

목적:
- 3인칭 카메라, 락온 상태의 카메라 반응, 컷씬 카메라, 카메라 포인트 구조 설명.
- 락온 대상 탐색/순회 규칙은 Combat/LockOn으로 분리되어 있고, 카메라는 선택된 대상만 전달받아 바라본다.


1. ThirdPersonCameraController.cs

역할:
- 일반 플레이어 추적 카메라와 락온 카메라 위치/회전을 처리한다.

주요 필드:
- target: 따라갈 플레이어 Transform.
- targetOffset: 플레이어 기준 시점 높이.
- distance/minDistance/maxDistance/zoomSpeed: 카메라 거리와 마우스 휠 줌.
- mouseSensitivity/minPitch/maxPitch: 마우스 회전.
- positionSmoothTime: 위치 보간.
- lockCursorOnStart: 시작 시 커서 잠금.
- lockOnTargetOffset/lockOnHeight/lockOnTargetClearance: 락온 상태 카메라 위치 조정.
- ForceCursorVisible: 보상 UI 등에서 커서를 강제로 보이게 하는 static 값.

외부에서 호출할 함수:
- SetTarget(Transform newTarget): 따라갈 대상 지정.
- SetLockOnTarget(Transform newTarget): 락온 대상 지정.
- ClearLockOnTarget(): 락온 해제.

흐름:
- LateUpdate()에서 target이 있으면 카메라 갱신.
- 락온 대상이 없으면 마우스로 yaw/pitch 회전.
- 락온 대상이 있으면 대상 방향으로 자동 회전.


2. CameraManager.cs

역할:
- Gameplay, LockOn, Cutscene 카메라 모드를 중앙에서 관리한다.
- 컷씬 중에는 카메라 컨트롤러를 끄고 직접 위치/회전/FOV를 적용한다.

외부에서 호출할 함수:
- CameraManager.GetOrCreate(): 매니저 획득 또는 생성.
- RegisterGameplayCamera(Camera camera, Transform target): 플레이어 카메라 등록.
- SetLockOnTarget(Transform target): 락온 카메라로 전환.
- ClearLockOnTarget(): 락온 해제.
- BeginCutscene(): 컷씬 시작.
- BeginCutscene(bool restoreControllersOnEnd): 컷씬 종료 시 컨트롤러 복구 여부 지정.
- ZoomToTarget(Transform target, Vector3 localOffset, float lookHeight, float duration, float fieldOfView): 특정 대상 줌.
- ZoomToPoint(Transform point, float duration, float fieldOfView): 지정 포인트로 카메라 이동.
- RestoreGameplayCamera(): 게임플레이 카메라 위치로 부드럽게 복귀.
- EndCutscene(): 컷씬 종료.

연동:
- CutsceneManager가 상자/보스 등장/문 열기 연출에서 호출한다.
- NetworkPlayerController 락온 흐름에서 SetLockOnTarget/ClearLockOnTarget을 호출한다.


3. CameraPointManager.cs

역할:
- 컷씬 카메라 포인트를 전역으로 제공한다.

Inspector 연결:
- goldChestCameraPoint
- bossWakeUpCameraPoint
- gateKickCameraPoint

사용:
- 컷씬에서 CameraPointManager.Instance의 포인트를 가져와 CameraManager.ZoomToPoint()에 넘기는 식으로 사용한다.
