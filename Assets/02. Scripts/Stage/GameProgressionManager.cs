using UnityEngine;
using System.Collections.Generic;
using Fusion;

public class GameProgressionManager : MonoSingleton<GameProgressionManager> // 제네릭 모노싱글톤 상속
{
    [Header("등장 가능한 보스 풀 (랜덤 추첨)")]
    public List<BossEncounterData> bossPool;

    // 동기화할 필요 없습니다! 방장(Host)이 여기서 계산하고 알아서 세팅합니다.
    public int CurrentLevel { get; private set; } = 1;
    public BossEncounterData CurrentBossData { get; private set; }

    // 구형 싱글톤 보일러플레이트 제거 (Instance/Awake는 베이스가 처리)

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

    // ▼▼▼ [디버그 전용] 씬 이동 없이 층/보스 데이터만 세팅 (DebugQuickEntry 에서 사용) ▼▼▼
    // 현재 열려 있는 보스 씬을 그대로 테스트할 때, runner.LoadScene 을 거치지 않고
    // CurrentBossData 를 직접 채워주기 위한 메서드입니다. 정상 플레이 흐름에는 영향 없음.
    public void SetupForDebug(int level, BossEncounterData bossData)
    {
        CurrentLevel = level;
        CurrentBossData = bossData;
        Debug.Log($"[GameProgressionManager] (디버그) Level={level}, Boss={(bossData != null ? bossData.bossName : "없음")} 세팅");
    }
    // ▲▲▲ 디버그 전용 끝 ▲▲▲
}