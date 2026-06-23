using UnityEngine;

// [역할] 투사체를 앞으로 날아가게 하고, 일정 시간이 지나면 파괴합니다.
public class ProjectileMover : MonoBehaviour
{
    public float speed = 15f;
    public float lifeTime = 5f;

    private void Start()
    {
        // 생성된 지 lifeTime 초가 지나면 자기 자신을 파괴
        Destroy(gameObject, lifeTime);
    }

    private void Update()
    {
        // 매 프레임마다 바라보는 방향(forward)으로 날아감
        transform.position += transform.forward * speed * Time.deltaTime;
    }
}