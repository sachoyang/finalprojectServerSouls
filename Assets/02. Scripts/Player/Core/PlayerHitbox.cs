using UnityEngine;

/// <summary>
/// 플레이어의 개별 피격 Collider를 표시한다.
/// 피격에 사용할 각 Sphere/Capsule Collider 오브젝트에 하나씩 부착한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerHitbox : MonoBehaviour
{
    [SerializeField] private Collider hitCollider;

    public Collider HitCollider
    {
        get
        {
            if (hitCollider == null)
            {
                hitCollider = GetComponent<Collider>();
            }

            return hitCollider;
        }
    }

    public bool Matches(Collider candidate)
    {
        return candidate != null && candidate.gameObject == gameObject;
    }

    private void Reset()
    {
        hitCollider = GetComponent<Collider>();
    }

    private void Awake()
    {
        PlayerStats playerStats = GetComponentInParent<PlayerStats>();
        if (playerStats != null)
        {
            gameObject.layer = playerStats.gameObject.layer;
        }
    }
}
