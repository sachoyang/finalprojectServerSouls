#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// 사용법: Project 창에서 보스의 AnimatorController 에셋을 1개 선택한 뒤,
//         상단 메뉴 Tools > Boss > Dump Animator States 클릭.
//         콘솔에 모든 State 이름이 (서브 스테이트 머신이면 "폴더.상태" 풀패스로) 찍힌다.
//         거기 찍힌 문자열을 BossActionModule.animationStateName 에 그대로 복붙하면 됨.
// 이 파일은 Editor 폴더에 두는 것을 권장 (UNITY_EDITOR 가드가 있어 다른 폴더여도 빌드는 됨).
public static class AnimatorStateDumper
{
    [MenuItem("Tools/Boss/Dump Animator States")]
    private static void Dump()
    {
        var ctrl = Selection.activeObject as AnimatorController;
        if (ctrl == null)
        {
            Debug.LogError("[Dumper] Project 창에서 AnimatorController 에셋을 선택한 뒤 실행하세요.");
            return;
        }

        Debug.Log($"================ '{ctrl.name}' State 목록 ================");
        foreach (var layer in ctrl.layers)
        {
            Debug.Log($"=== Layer {ctrl.layers.Length}개 중: '{layer.name}' ===");
            DumpStateMachine(layer.stateMachine, "");
        }
        Debug.Log("================ 끝. 위 문자열을 animationStateName 에 그대로 사용 ================");
    }

    private static void DumpStateMachine(AnimatorStateMachine sm, string prefix)
    {
        // 이 스테이트 머신에 직접 있는 상태들
        foreach (var s in sm.states)
        {
            string fullName = prefix + s.state.name;
            Debug.Log($"State: \"{fullName}\"   (hash={Animator.StringToHash(fullName)})");
        }

        // 하위 서브 스테이트 머신 재귀 (풀패스 접두어 누적)
        foreach (var sub in sm.stateMachines)
        {
            DumpStateMachine(sub.stateMachine, prefix + sub.stateMachine.name + ".");
        }
    }
}
#endif
