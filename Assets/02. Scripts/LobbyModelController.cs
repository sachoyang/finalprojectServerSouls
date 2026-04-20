using UnityEngine;

public class LobbyModelController : MonoBehaviour
{
    [Header("설정")]
    public float rotateSpeed = 0.5f;     // 드래그 회전 속도
    public float autoRotateSpeed = 20f; // 자동 회전 속도
    
    [Header("상태")]
    [SerializeField] private bool isAutoRotate = false;   // 체크박스로 제어할 변수

    void Update()
    {
        // 자동 회전 체크 시 Y축 방향으로 서서히 회전
        if (isAutoRotate)
        {
            transform.Rotate(Vector3.up, autoRotateSpeed * Time.deltaTime);
        }
    }

    public void SetAutoRotate(bool value)
    {
        isAutoRotate = value;
    }

    public void OnDragModel(float deltaX)
    {
        transform.Rotate(Vector3.up, -deltaX * rotateSpeed);
    }
}