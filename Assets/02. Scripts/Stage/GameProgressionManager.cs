using UnityEngine;
using System.Collections.Generic;
using Fusion;

public class GameProgressionManager : MonoBehaviour
{
    public static GameProgressionManager Instance { get; private set; }

    [Header("등장 가능한 보스 풀 (랜덤 추첨)")]
    public List<BossEncounterData> bossPool;

    // 동기화할 필요 없습니다! 방장(Host)이 여기서 계산하고 알아서 세팅합니다.
    public int CurrentLevel { get; private set; } = 1;
    public BossEncounterData CurrentBossData { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // 씬이 넘어가도 절대 파괴되지 않음
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // 로비에서 레디가 끝나면 호출됨
    public void StartFirstLevel(NetworkRunner runner)
    {
        CurrentLevel = 1;
        LoadNextRandomLevel(runner);
    }

    // 다음 층으로 넘어갈 때 호출됨
    public void LoadNextRandomLevel(NetworkRunner runner)
    {
        if (bossPool == null || bossPool.Count == 0) return;

        // 1. 랜덤 보스와 맵 뽑기
        int randomIndex = Random.Range(0, bossPool.Count);
        CurrentBossData = bossPool[randomIndex];

        Debug.Log($"=== [통제실] {CurrentLevel}층 진입! 출현 보스: {CurrentBossData.bossName}, 맵: {CurrentBossData.sceneName} ===");

        // 2. 뽑힌 보스의 전용 맵(씬)으로 플레이어 전원 강제 이동!
        runner.LoadScene(CurrentBossData.sceneName, UnityEngine.SceneManagement.LoadSceneMode.Single);
    }

    public void GoToNextLevel(NetworkRunner runner)
    {
        CurrentLevel++;
        LoadNextRandomLevel(runner);
    }

    // 클라이언트가 방장의 층수를 받아와서 강제로 동기화하는 함수
    public void SetLevelFromHost(int level)
    {
        CurrentLevel = level;
    }

}