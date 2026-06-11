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
        Vector3 rayStart = transform.position + (Vector3.up * _stepHeight);
        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, _stepHeight * 2f, _groundLayerMask))
        {
            Vector3 newPosition = transform.position;
            newPosition.y = hit.point.y;
            transform.position = newPosition;
        }
        else
        {
            transform.position += Vector3.down * _gravitySpeed * deltaTime;
        }
    }
}
