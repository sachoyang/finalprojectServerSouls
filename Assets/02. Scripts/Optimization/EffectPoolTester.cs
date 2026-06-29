using UnityEngine;

// [검증용 임시 스크립트] 에디터에서 풀 동작을 눈으로 확인하는 용도. 검증 끝나면 삭제해도 됨.
//  사용: 아무 씬(예: scServer_stage)의 빈 오브젝트에 붙이고 testPrefab에 풀에 등록한 이펙트를 넣는다.
//  좌상단 GUI 버튼으로 Spawn 테스트.
public class EffectPoolTester : MonoBehaviour
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    [Tooltip("EffectPoolConfig에 등록해 둔 이펙트 프리팹")]
    public GameObject testPrefab;

    [Tooltip("Spawn 위치(비우면 이 오브젝트 위치)")]
    public Transform spawnPoint;

    private int _spawnCount;

    private void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 320, 260, 160), GUI.skin.box);
        GUILayout.Label("[Pool Tester]");

        bool ready = EffectPoolManager.Instance != null;
        GUILayout.Label($"PoolManager: {(ready ? "OK" : "없음")}");
        GUILayout.Label($"누적 Spawn: {_spawnCount}");

        GUI.enabled = ready && testPrefab != null;

        if (GUILayout.Button("Spawn 1회", GUILayout.Height(28)))
            DoSpawn(1);

        if (GUILayout.Button("Spawn 20회 (카테고리 한도 테스트)", GUILayout.Height(28)))
            DoSpawn(20);

        GUI.enabled = true;
        GUILayout.EndArea();
    }

    private void DoSpawn(int n)
    {
        Vector3 basePos = spawnPoint != null ? spawnPoint.position : transform.position;
        for (int i = 0; i < n; i++)
        {
            Vector3 p = basePos + new Vector3(Random.Range(-3f, 3f), 0f, Random.Range(-3f, 3f));
            EffectPoolManager.Instance.Spawn(testPrefab, p, Quaternion.identity);
            _spawnCount++;
        }
    }
#endif
}
