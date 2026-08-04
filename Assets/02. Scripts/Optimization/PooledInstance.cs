using System.Collections.Generic;
using UnityEngine;

// [역할] 풀에서 꺼낸 인스턴스에 자동으로 붙는 표식. 출처(프리팹/카테고리)를 기억해 회수 때 사용한다.
//  직접 추가할 필요 없음 — EffectPoolManager가 생성 시 붙여준다.
public class PooledInstance : MonoBehaviour
{
    [System.NonSerialized] public GameObject sourcePrefab;     // 어떤 프리팹에서 나왔는지(풀 키)
    [System.NonSerialized] public string category;             // 소속 카테고리 이름
    [System.NonSerialized] public LinkedListNode<PooledInstance> activeNode; // 카테고리 활성목록에서의 위치(가장 오래된 것 회수용)
    [System.NonSerialized] public EffectPoolManager manager;

    // 편의: 자기 자신을 풀로 반환.
    public void ReturnToPool()
    {
        if (manager != null) manager.Return(gameObject);
        else gameObject.SetActive(false);
    }

    // 씬 오브젝트를 parent로 꺼낸 인스턴스는 씬 언로드 때 풀 몰래 파괴된다.
    // 그때 카테고리 활성목록에 노드가 남아 있으면 다음 회수(ReturnAllActive 등)에서
    // 파괴된 객체에 접근해 MissingReferenceException이 난다 → 파괴 시점에 스스로 빠진다.
    private void OnDestroy()
    {
        if (activeNode != null)
        {
            if (activeNode.List != null) activeNode.List.Remove(activeNode);
            activeNode = null;
        }
    }
}
