using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// KriptoFX가 자체 애니메이션을 끝내 모든 메시와 데칼을 끈 뒤 루트 오브젝트를 정리한다.
/// 별도 타이머나 코루틴 없이 캐시된 컴포넌트만 확인하므로 프레임당 가비지를 만들지 않는다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BloodEffectAutoCleanup : MonoBehaviour
{
    private Renderer[] _renderers;
    private DecalProjector[] _decalProjectors;
    private bool _observedActiveVisual;

    private void Awake()
    {
        _renderers = GetComponentsInChildren<Renderer>(true);
        _decalProjectors = GetComponentsInChildren<DecalProjector>(true);
    }

    private void LateUpdate()
    {
        bool hasActiveVisual = HasActiveVisual();
        if (hasActiveVisual)
        {
            _observedActiveVisual = true;
            return;
        }

        // 생성 직후 BFX의 OnEnable 처리보다 먼저 검사된 경우에는 파괴하지 않는다.
        if (_observedActiveVisual)
        {
            Destroy(gameObject);
        }
    }

    private bool HasActiveVisual()
    {
        for (int i = 0; i < _renderers.Length; i++)
        {
            Renderer renderer = _renderers[i];
            if (renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy)
            {
                return true;
            }
        }

        for (int i = 0; i < _decalProjectors.Length; i++)
        {
            DecalProjector projector = _decalProjectors[i];
            if (projector != null && projector.enabled && projector.gameObject.activeInHierarchy)
            {
                return true;
            }
        }

        return false;
    }
}
