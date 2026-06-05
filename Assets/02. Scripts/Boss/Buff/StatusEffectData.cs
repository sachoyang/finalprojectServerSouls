using UnityEngine;

public enum StatusEffectTarget
{
    None,            
    IncomingDamage,  
    OutgoingDamage,  
    MoveSpeed        
}

[CreateAssetMenu(menuName = "ServerSouls/Status Effect Data")]
public class StatusEffectData : ScriptableObject
{
    [Header("기본 정보")]
    [Tooltip("고유 ID (반드시 1 이상의 숫자로 설정하세요!)")]
    public int statusId; 
    public string statusName = "상태이상 이름";
    public float Power;
    
    public StatusEffectTarget effectTarget;

    [Header("지속 시간 설정")]
    [Tooltip("체크 시 시간 제한 없이 무한 유지되며, 특정 기믹/트리거로만 꺼집니다.")]
    public bool isInfinite = false;
    
    [Tooltip("기본 지속 시간 (초) - isInfinite가 false일 때만 적용됨")]
    public float defaultDuration = 10f;

    [TextArea]
    public string description = "상태이상 설명 (UI 툴팁용)";

    [Header("UI 시각 데이터")]
    public Sprite icon; 
    public bool isDebuff = true; 
}