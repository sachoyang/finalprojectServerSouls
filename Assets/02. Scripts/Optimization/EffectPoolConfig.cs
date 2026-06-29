using System.Collections.Generic;
using UnityEngine;

// [역할] 오브젝트 풀 설정 데이터. 카테고리별로 풀링할 이펙트 프리팹과 한도를 정의한다.
//  - 카테고리 예: BossEffect(보스 이펙트), PlayerEffect(플레이어 이펙트), BloodEffect(피 이펙트) … 자유롭게 추가.
//  - ★ 새 이펙트 추가: 해당 카테고리의 prefabs 리스트에 프리팹을 넣고 prewarm/autoReturn 값만 정하면 끝.
//  - 생성: Project 창 우클릭 → Create → Optimization → Effect Pool Config
[CreateAssetMenu(fileName = "EffectPoolConfig", menuName = "Optimization/Effect Pool Config")]
public class EffectPoolConfig : ScriptableObject
{
    public List<Category> categories = new List<Category>();

    [System.Serializable]
    public class Category
    {
        [Tooltip("카테고리 이름. 예: BossEffect, PlayerEffect, BloodEffect (자유롭게 추가). 코드에서 이 문자열로 참조하지 않아도 됨 — 프리팹으로 꺼내 씀.")]
        public string name;

        [Tooltip("이 카테고리에서 '동시에 활성화'될 수 있는 최대 개수. 초과하면 가장 오래된 인스턴스를 회수해 재사용한다. 0 = 무제한.")]
        public int maxActive = 30;

        public List<Entry> prefabs = new List<Entry>();
    }

    [System.Serializable]
    public class Entry
    {
        public GameObject prefab;

        [Tooltip("로딩(프리워밍) 때 미리 만들어 둘 개수. 평소 동시 사용량 정도로 잡으면 게임 중 추가 생성이 거의 없다.")]
        [Min(0)] public int prewarmCount = 4;

        [Tooltip("Spawn() 으로 꺼냈을 때 자동 회수까지의 시간(초). 0이면 자동 회수 안 함(파티클이 끝나면 회수, 또는 수동 Return).")]
        [Min(0f)] public float autoReturnAfter = 0f;
    }
}
