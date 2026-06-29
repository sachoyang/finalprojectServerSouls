using UnityEngine;

// [역할] 풀에서 Spawn()한 이펙트를 '파티클이 끝나면' 또는 '지정 시간 뒤' 자동으로 풀에 반환한다.
//  EffectPoolManager.Spawn()이 자동으로 붙이고 Begin()을 호출한다. 직접 붙일 필요 없음.
[RequireComponent(typeof(PooledInstance))]
public class AutoReturnToPool : MonoBehaviour
{
    private float _maxLifetime;  // >0이면 이 시간 뒤 무조건 반환
    private float _elapsed;
    private bool _running;
    private bool _particlesStarted;
    private ParticleSystem[] _particles;

    public void Begin(float maxLifetime)
    {
        _maxLifetime = maxLifetime;
        _elapsed = 0f;
        _particlesStarted = false;
        _running = true;
        _particles = GetComponentsInChildren<ParticleSystem>(true);
    }

    private void OnDisable()
    {
        _running = false; // 풀로 반환되며 비활성화될 때 정지
    }

    private void Update()
    {
        if (!_running) return;
        _elapsed += Time.deltaTime;

        // 1) 지정 수명 우선
        if (_maxLifetime > 0f && _elapsed >= _maxLifetime) { ReturnNow(); return; }

        // 2) 파티클 기반: 한 번 살아난 뒤 모두 죽으면 반환
        if (_particles != null && _particles.Length > 0)
        {
            bool anyAlive = false;
            for (int i = 0; i < _particles.Length; i++)
                if (_particles[i] != null && _particles[i].IsAlive(true)) { anyAlive = true; break; }

            if (anyAlive) _particlesStarted = true;
            else if (_particlesStarted) { ReturnNow(); return; }
        }
        else if (_maxLifetime <= 0f)
        {
            // 파티클도 없고 수명 지정도 없으면 기준이 없으니 안전장치로 5초 후 반환
            if (_elapsed >= 5f) ReturnNow();
        }
    }

    private void ReturnNow()
    {
        _running = false;
        var pi = GetComponent<PooledInstance>();
        if (pi != null) pi.ReturnToPool();
        else gameObject.SetActive(false);
    }
}
