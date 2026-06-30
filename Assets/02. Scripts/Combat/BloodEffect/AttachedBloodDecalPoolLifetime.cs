using UnityEngine;

/// <summary>
/// 부착 혈흔의 수명을 관리하고 만료 시 EffectPoolManager로 반환한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class AttachedBloodDecalPoolLifetime : MonoBehaviour
{
    private BloodEffectSpawner _owner;
    private float _remainingLifetime;
    private bool _running;

    public void Begin(BloodEffectSpawner owner, float lifetime)
    {
        _owner = owner;
        _remainingLifetime = lifetime;
        _running = true;
    }

    private void Update()
    {
        if (!_running)
        {
            return;
        }

        _remainingLifetime -= Time.deltaTime;
        if (_remainingLifetime > 0f)
        {
            return;
        }

        _running = false;
        if (_owner != null)
        {
            _owner.ReturnAttachedDecal(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnDisable()
    {
        _running = false;
        if (_owner != null)
        {
            _owner.NotifyAttachedDecalDisabled(gameObject);
        }

        _owner = null;
    }
}
