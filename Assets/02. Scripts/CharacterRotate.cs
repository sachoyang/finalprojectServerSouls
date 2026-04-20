using UnityEngine;

public class CharacterRotate : MonoBehaviour
{
    public float rotateSpeed = 5f;
    private bool isDragging = false;

    void OnMouseDrag()
    {
        // 마우스 이동량에 따라 캐릭터 회전 (Y축 기준)
        float rotX = Input.GetAxis("Mouse X") * rotateSpeed * Mathf.Deg2Rad;
        transform.Rotate(Vector3.up, -rotX * 100f);
    }
}