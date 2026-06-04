using UnityEngine;

// 상태이상이 데미지 계산 시 어디에 영향을 줄지 결정하는 타입
public enum StatusEffectTarget
{
    None,            // 단순 표기용 (UI 타이머 등)
    IncomingDamage,  // 방깎, 뎀감 (받는 데미지 배율)
    OutgoingDamage,  // 공격력 증가, 감소 (주는 데미지 배율)
    MoveSpeed        // 이동 속도 (추후 확장용)
}

[CreateAssetMenu(menuName = "ServerSouls/Status Effect Data")]
public class StatusEffectData : ScriptableObject
{
    [Header("기본 정보")]
    [Tooltip("고유 ID (1 이상의 숫자)")]
    public int statusId; 
    public string statusName = "상태이상 이름";
    public float Power;
    
    // 🔥 [새로 추가] 이 상태이상이 데미지 계산 중 어디에 관여하는지 기획자가 직접 선택
    [Tooltip("어떤 스탯에 영향을 줄지 선택하세요.")]
    public StatusEffectTarget effectTarget;

    [TextArea]
    public string description = "상태이상 설명 (UI 툴팁용)";

    [Header("UI 시각 데이터")]
    public Sprite icon; 
    public bool isDebuff = true; 
}