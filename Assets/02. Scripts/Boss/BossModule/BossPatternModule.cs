using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class BossActionModule
{
    [Header("Animation Setup")]
    [Tooltip("에디터에서 어떤 동작인지 직관적으로 확인하기 위한 애니메이션 클립 원본")]
    public AnimationClip animationClip;

    [Tooltip("Animator에서 실제로 재생할 State 이름 (예: Jump Attack)")]
    public string animationStateName;

    // 컴퓨터가 실제로 읽을 고유 번호 (인스펙터에는 안 보임)
    [HideInInspector] 
    public int animationHash;

    [Tooltip("이 동작을 유지할 시간 (초). \n애니메이션 원본 길이와 다르게 설정하여 엇박자나 후딜레이를 마음대로 조절할 수 있습니다.")]
    public float duration = 1.0f;

    [Header("Movement Mode")]
    [Tooltip("ON  : 이 클립의 실제 루트모션(animator.deltaPosition)으로 이동. moveOffset/moveCurve 는 무시.\n" +
             "       → 해당 클립은 Root Transform Position(XZ) 의 Bake Into Pose 를 OFF 로 임포트해야 함(루트모션 추출).\n" +
             "OFF : 아래 moveOffset + moveCurve 로 코드가 이동을 만든다.\n" +
             "       → 해당 클립은 Bake Into Pose 를 ON(제자리) 으로 임포트.")]
    public bool useRootMotion = false;

    [Header("Curve-Driven Movement")]
    [Tooltip("이 동작 동안 보스가 자신의 로컬 앞/옆으로 얼마나 이동할 것인가? (예: Z축 5 = 앞으로 5m 돌진)")]
    public Vector3 moveOffset;
    
    [Tooltip("시간 흐름(X: 0~1)에 따른 이동 완료 비율(Y: 0~1). \n급발진 돌진이나 멈칫 후 이동을 그래프로 정밀하게 깎습니다.")]
    public AnimationCurve moveCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Hitbox / Mechanics")]
    [Tooltip("이 동작에서 사용할 히트박스 인덱스 (0: 머리, 1: 앞발, 2: 무기 등)")]
    public int hitboxIndex = -1;
    
    [Tooltip("이 동작 중 슈퍼아머(경직 면역)를 적용할 것인지 여부")]
    public bool hasSuperArmor = true;

    [Header("Tracking 설정")]
    [Tooltip("이 공격을 하는 동안 타겟을 쳐다보도록 몸을 틀 것인가?")]
    public bool enableTracking = true;
    
    [Tooltip("트래킹을 멈출 시점 (0~1 퍼센트). 예: 0.5면 액션 절반까지만 타겟을 따라가고 그 후엔 고정됨")]
    [Range(0f, 1f)]
    public float trackingStopPercent = 0.5f; 
    
    [Tooltip("트래킹(회전) 속도")]
    public float trackingSpeed = 3.0f;
}

[CreateAssetMenu(menuName = "ServerSouls/Boss Modules/Boss Pattern")]
public class BossPatternModule : ScriptableObject
{
    [Header("Pattern Info")]
    public string patternId;
    public string displayName;

    [Header("AI Conditions")]
    [Tooltip("이 패턴이 발동될 수 있는 최소 사거리")]
    public float minRange = 0f;
    [Tooltip("이 패턴이 발동될 수 있는 최대 사거리")]
    public float maxRange = 10f;
    [Tooltip("패턴 추첨 시 가중치 (높을수록 자주 나옴)")]
    public int weight = 10;

    [Header("Actions Sequence")]
    [Tooltip("위에서 아래로 순차적으로 실행될 동작 블록들")]
    public List<BossActionModule> actions = new List<BossActionModule>();
    
    public int ActionCount => actions.Count;
    public BossActionModule GetAction(int index) => actions[index];

    // 인스펙터에서 기획자가 값을 수정할 때마다 유니티가 자동으로 실행하는 함수
    private void OnValidate()
    {
        for (int i = 0; i < actions.Count; i++)
        {
            if (!string.IsNullOrEmpty(actions[i].animationStateName))
            {
                // 문자열을 정수형 Hash로 미리 변환해서 저장해 둡니다! (게임 실행 중 연산 비용 0)
                actions[i].animationHash = Animator.StringToHash(actions[i].animationStateName);
            }
        }
    }
}