using UnityEngine;

public class WarningIndicator : MonoBehaviour
{
    [Header("장판 설정")]
    [Tooltip("장판이 꽉 차는 데 걸리는 시간 (점프 시전 시간과 맞추세요)")]
    public float duration = 1.2f;
    
    [Tooltip("서서히 커질 안쪽 원형 오브젝트 (없으면 빈칸)")]
    public Transform innerCircle;

    private float _timer = 0f;
    private Vector3 _initialScale;

    private void Start()
    {
        // 안쪽 원이 지정되어 있다면 크기를 0으로 초기화
        if (innerCircle != null)
        {
            _initialScale = innerCircle.localScale;
            innerCircle.localScale = Vector3.zero;
        }

        // 설정된 시간이 지나면 장판 스스로 파괴 (넉넉하게 0.2초 추가)
        Destroy(gameObject, duration + 0.2f);
    }

    private void Update()
    {
        _timer += Time.deltaTime;
        float progress = Mathf.Clamp01(_timer / duration);

        // 안쪽 원이 서서히 바깥쪽으로 차오르는 연출
        if (innerCircle != null)
        {
            innerCircle.localScale = Vector3.Lerp(Vector3.zero, _initialScale, progress);
        }
    }
}