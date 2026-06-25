using UnityEngine;

// 벽 미끄러짐 + 지형 Y축 밀착 물리 연산 책임 분리
// Transform만 조작하는 순수 로직이라 네트워크 코어에서 분리 가능
public class BossMovementController
{
    private readonly LayerMask _wallLayerMask;
    private readonly float _bodyRadius;
    private readonly float _castHeightOffset;
    private readonly LayerMask _groundLayerMask;
    private readonly float _stepHeight;
    private readonly float _gravitySpeed;

    public BossMovementController(LayerMask wallLayerMask, float bodyRadius, float castHeightOffset,
                                  LayerMask groundLayerMask, float stepHeight, float gravitySpeed)
    {
        _wallLayerMask = wallLayerMask;
        _bodyRadius = bodyRadius;
        _castHeightOffset = castHeightOffset;
        _groundLayerMask = groundLayerMask;
        _stepHeight = stepHeight;
        _gravitySpeed = gravitySpeed;
    }

    // 🔥 [버그 픽스] 비행(공중부양) 중에는 중력/바닥밀착을 통째로 끄기 위한 스위치.
    //    NetworkBossCore가 매 틱 IsFlightActive 상태를 여기에 주입해 준다.
    public bool BypassGround { get; set; }

    // 목표 이동량을 벽에 막히면 미끄러뜨려 안전하게 이동 후 지형에 밀착
    public void MoveWithWallSlide(Transform transform, Vector3 targetDisplacement, float deltaTime)
    {
        if (targetDisplacement.sqrMagnitude < 0.000001f) return;

        Vector3 sphereCenter = transform.position + Vector3.up * _castHeightOffset;

        if (Physics.SphereCast(sphereCenter, _bodyRadius, targetDisplacement.normalized, out RaycastHit hit, targetDisplacement.magnitude, _wallLayerMask))
        {
            float safeDistance = Mathf.Max(0f, hit.distance - 0.01f);
            Vector3 safeMove = targetDisplacement.normalized * safeDistance;
            transform.position += safeMove;

            Vector3 remainingDisplacement = targetDisplacement - safeMove;
            Vector3 slideDisplacement = Vector3.ProjectOnPlane(remainingDisplacement, hit.normal);
            slideDisplacement.y = 0;

            if (slideDisplacement.sqrMagnitude > 0.000001f)
            {
                Vector3 newSphereCenter = transform.position + Vector3.up * _castHeightOffset;
                if (Physics.SphereCast(newSphereCenter, _bodyRadius, slideDisplacement.normalized, out RaycastHit hit2, slideDisplacement.magnitude, _wallLayerMask))
                {
                    float safeDistance2 = Mathf.Max(0f, hit2.distance - 0.01f);
                    transform.position += slideDisplacement.normalized * safeDistance2;
                }
                else
                {
                    transform.position += slideDisplacement;
                }
            }
        }
        else
        {
            transform.position += targetDisplacement;
        }

        StickToGround(transform, deltaTime);
    }

    // Y축을 바닥에 밀착, 바닥이 없으면 가짜 중력으로 낙하
    private void StickToGround(Transform transform, float deltaTime)
    {
        // 🔥 [버그 픽스] 레이 시작점을 발밑(stepHeight)이 아니라 몸통 위(castHeightOffset)에서 쏜다.
        //    기존엔 보스가 한 번 지면 아래로 파고들면 rayStart 가 바닥 콜라이더 '안/아래'에 생겨
        //    레이가 지면을 못 잡고 → 가짜 중력이 계속 적용 → 점점 더 박히는 런어웨이가 발생했다.
        //    위에서 내려쏘면 파묻힌 상태에서도 지면을 다시 찾아 정상 높이로 복구된다.
        float rayUp = Mathf.Max(_castHeightOffset, _stepHeight);
        Vector3 rayStart = transform.position + (Vector3.up * rayUp);
        float rayLength = rayUp + (_stepHeight * 2f); // 발밑 stepHeight*2 까지는 계단/경사로 따라 내려감
        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, rayLength, _groundLayerMask))
        {
            // 🔥 [높낮이 맵 버그 픽스] 비행 중이든 지상이든, 부모 루트는 '항상 현재 지형 표면'에 밀착시킨다.
            //    비행 고도(flightHeight)는 Visual이 자식 메쉬에 별도로 얹으므로, 부모가 지형을 따라가야
            //    '지면 기준 일정 고도'로 날고, 죽었을 때도 시체가 현재 지면에 정확히 안착한다.
            //    (예전엔 BypassGround로 이걸 통째로 스킵해서 부모 Y가 이륙 지점에 얼어붙어 시체가 공중에 떴음)
            Vector3 newPosition = transform.position;
            newPosition.y = hit.point.y;
            transform.position = newPosition;
        }
        else if (!BypassGround)
        {
            // 지면을 못 찾았을 때의 '가짜 중력 추락'은 비행 중이 아닐 때만 적용 (비행 중 추락 방지 = 요구사항3).
            transform.position += Vector3.down * _gravitySpeed * deltaTime;
        }
    }
}
