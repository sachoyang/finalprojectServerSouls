using UnityEngine;
using UnityEngine.EventSystems;

public class UI_ModelDragHandler : MonoBehaviour, IDragHandler
{
    public LobbyModelController modelController;

    public void OnDrag(PointerEventData eventData)
    {
        if (modelController != null)
        {
            // 마우스 이동량(delta.x)을 모델 컨트롤러에 전달
            modelController.OnDragModel(eventData.delta.x);
        }
    }
}