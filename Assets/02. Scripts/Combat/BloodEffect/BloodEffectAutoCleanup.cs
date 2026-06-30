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
    private BloodEffectSpawner _owner;
    private float _maxLifetime;
    private float _elapsed;
    private bool _running;

    private void Awake()
    {
        _renderers = GetComponentsInChildren<Renderer>(true);
        _decalProjectors = GetComponentsInChildren<DecalProjector>(true);
    }

    public void Begin(BloodEffectSpawner owner, float maxLifetime)
    {
        _owner = owner;
        _maxLifetime = maxLifetime;
        _elapsed = 0f;
        _observedActiveVisual = false;
        _running = true;
    }

    private void LateUpdate()
    {
        if (!_running)
        {
            return;
        }

        _elapsed += Time.deltaTime;
        bool hasActiveVisual = HasActiveVisual();
        if (hasActiveVisual)
        {
            _observedActiveVisual = true;
        }

        // 자연 종료 또는 안전 최대 수명에 도달하면 파괴 대신 풀로 반환한다.
        if ((_observedActiveVisual && !hasActiveVisual) || _elapsed >= _maxLifetime)
        {
            _running = false;
            if (_owner != null)
            {
                _owner.ReturnBloodEffect(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }

    private void OnDisable()
    {
        _running = false;
        if (_owner != null)
        {
            _owner.NotifyBloodEffectDisabled(gameObject);
        }

        _owner = null;
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
