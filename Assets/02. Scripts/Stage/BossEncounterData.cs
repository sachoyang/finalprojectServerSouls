using UnityEngine;

[CreateAssetMenu(menuName = "ServerSouls/Boss Encounter Data")]
public class BossEncounterData : ScriptableObject
{
    [Tooltip("기획자 확인용 보스 이름 (예: 화염 드래곤)")]
    public string bossName = "Unknown Boss";

    [Tooltip("스폰할 보스 프리팹")]
    public GameObject bossPrefab;

    [Tooltip("이 보스가 등장할 씬(맵)의 정확한 이름 (예: scVolcano)")]
    public string sceneName = "scLevel"; 
}