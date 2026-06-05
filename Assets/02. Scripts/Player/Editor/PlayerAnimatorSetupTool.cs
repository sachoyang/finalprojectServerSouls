using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using System.Collections.Generic;

public static class PlayerAnimatorSetupTool
{
    // 플레이어 Animator Controller를 에디터 메뉴에서 자동 보정하기 위한 도구.
    // 기본 전투/락온 세팅과 액티브 스킬 모듈 동기화를 분리해, 새 스킬 추가 시 기본 Animator 구성이 불필요하게 덮이지 않도록 한다.
    private const string ControllerPath = "Assets/07. Animations/PlayerCtrl.controller";

    private const string IsLockOn = "IsLockOn";
    private const string LockMoveX = "LockMoveX";
    private const string LockMoveY = "LockMoveY";
    private const string LockMoveSpeed = "LockMoveSpeed";
    private const string Impact = "Impact";
    private const string Impact2 = "Impact2";
    private const string Death = "Death";
    private const string IsCrawling = "IsCrawling";
    private const string Parry = "Parry";
    private const string Roll = "Roll";
    private const string Jump = "Jump";
    private const string Attack2 = "Attack2";
    private const string Attack3 = "Attack3";
    private const string Attack4 = "Attack4";

    private const string LockOnMachineName = "LockOn Movement";
    private const string LockOnBlendStateName = "LockOn Blend Tree";
    private const string SkillMachineName = "Skill Actions";
    private const string SkillModuleFolder = "Assets/02. Scripts/Player/Abilities/SkillModule";

    [MenuItem("Tools/ServerSouls/Setup Player Base Animator")]
    public static void SetupPlayerBaseAnimator()
    {
        // 기본 세팅 메뉴는 이동/락온/기본 공격/피격/패링/구르기처럼 플레이어가 항상 가져야 하는 상태만 관리한다.
        // 액티브 스킬 모듈은 별도 메뉴에서 처리해서 수동으로 다듬은 기본 Animator가 스킬 동기화 때문에 흔들리지 않게 한다.
        if (!TryLoadController(out AnimatorController controller))
        {
            return;
        }

        AddBaseParameters(controller);
        AnimatorStateMachine root = controller.layers[0].stateMachine;
        AnimatorStateMachine lockOnMachine = FindStateMachine(root, LockOnMachineName)
            ?? root.AddStateMachine(LockOnMachineName, new Vector3(640f, 40f, 0f));

        AnimatorState lockOnState = FindState(lockOnMachine, LockOnBlendStateName);
        if (lockOnState == null)
        {
            lockOnState = lockOnMachine.AddState(LockOnBlendStateName, new Vector3(280f, 80f, 0f));
        }

        BlendTree tree = lockOnState.motion as BlendTree;
        if (tree == null)
        {
            tree = new BlendTree { name = LockOnBlendStateName };
            AssetDatabase.AddObjectToAsset(tree, controller);
            lockOnState.motion = tree;
        }

        ConfigureBlendTree(tree);
        lockOnMachine.defaultState = lockOnState;

        EnsureAnyStateTransition(root, lockOnState);
        EnsureExitTransition(lockOnState, FindState(root, "idle1"));
        SetupDamageAndDownStates(root);
        SetupBuiltInActionStates(root);
        RemoveOldLockOnStates(root);

        SaveController(controller);
        Debug.Log("Player base animator setup complete.");
    }

    [MenuItem("Tools/ServerSouls/Sync Ability Modules To Animator")]
    public static void SyncAbilityModulesToAnimator()
    {
        // 새 액티브 스킬 모듈을 만들었을 때 사용하는 메뉴.
        // PlayerAbilityModule의 Animation Clip / State Name / Trigger 값을 읽어서 Skill Actions 하위 State만 추가 또는 갱신한다.
        if (!TryLoadController(out AnimatorController controller))
        {
            return;
        }

        AnimatorStateMachine root = controller.layers[0].stateMachine;
        SetupSkillActionsFromModules(controller, root);

        SaveController(controller);
        Debug.Log("Active ability modules synced to player animator.");
    }

    private static bool TryLoadController(out AnimatorController controller)
    {
        controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller != null)
        {
            return true;
        }

        Debug.LogError($"Animator Controller not found: {ControllerPath}");
        return false;
    }

    private static void SaveController(AnimatorController controller)
    {
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
    }

    private static void AddBaseParameters(AnimatorController controller)
    {
        // 코드가 SetTrigger/SetBool/SetFloat로 접근하는 기본 파라미터를 보장한다.
        // 이미 있는 파라미터는 AddParameter에서 건너뛰므로 여러 번 실행해도 중복 생성되지 않는다.
        AddParameter(controller, IsLockOn, AnimatorControllerParameterType.Bool);
        AddParameter(controller, LockMoveX, AnimatorControllerParameterType.Float);
        AddParameter(controller, LockMoveY, AnimatorControllerParameterType.Float);
        AddParameter(controller, LockMoveSpeed, AnimatorControllerParameterType.Float);
        AddParameter(controller, Impact, AnimatorControllerParameterType.Trigger);
        AddParameter(controller, Impact2, AnimatorControllerParameterType.Trigger);
        AddParameter(controller, Death, AnimatorControllerParameterType.Trigger);
        AddParameter(controller, IsCrawling, AnimatorControllerParameterType.Bool);
        AddParameter(controller, Parry, AnimatorControllerParameterType.Trigger);
        AddParameter(controller, Roll, AnimatorControllerParameterType.Trigger);
        AddParameter(controller, Jump, AnimatorControllerParameterType.Trigger);
        AddParameter(controller, Attack2, AnimatorControllerParameterType.Trigger);
        AddParameter(controller, Attack3, AnimatorControllerParameterType.Trigger);
        AddParameter(controller, Attack4, AnimatorControllerParameterType.Trigger);
    }

    private static void AddParameter(AnimatorController controller, string name, AnimatorControllerParameterType type)
    {
        foreach (AnimatorControllerParameter parameter in controller.parameters)
        {
            if (parameter.name == name)
            {
                return;
            }
        }

        controller.AddParameter(name, type);
    }

    private static void SetupSkillActionsFromModules(AnimatorController controller, AnimatorStateMachine root)
    {
        // 액티브 모듈만 Animator에 연결한다.
        // 패시브 모듈은 획득 즉시 효과만 적용하므로 애니메이션 State나 Trigger를 만들 필요가 없다.
        AnimatorStateMachine skillMachine = FindStateMachine(root, SkillMachineName)
            ?? root.AddStateMachine(SkillMachineName, new Vector3(640f, 280f, 0f));

        AnimatorState idleState = FindState(root, "idle1");
        string[] moduleGuids = AssetDatabase.FindAssets("t:PlayerAbilityModule", new[] { SkillModuleFolder });
        ClearSkillActions(controller, root, skillMachine, moduleGuids);
        int syncedCount = 0;

        foreach (string moduleGuid in moduleGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(moduleGuid);
            PlayerAbilityModule module = AssetDatabase.LoadAssetAtPath<PlayerAbilityModule>(path);
            if (module == null || !module.IncludeInRewardPool)
            {
                continue;
            }

            if (module.AnimationClip == null || string.IsNullOrWhiteSpace(module.AnimationTrigger))
            {
                // 액티브 스킬은 런타임에서 Trigger로 재생하므로 Clip과 Trigger 둘 중 하나라도 없으면 Animator 연결을 만들지 않는다.
                if (module.IsActive)
                {
                    Debug.LogWarning($"Skipped active ability module with missing animation data: {path}");
                }
                continue;
            }

            // State Name을 비워둔 모듈은 클립 이름을 그대로 State 이름으로 사용해 빠르게 세팅할 수 있게 한다.
            string stateName = GetModuleStateName(module);

            AddParameter(controller, module.AnimationTrigger, AnimatorControllerParameterType.Trigger);

            AnimatorState state = FindState(skillMachine, stateName);
            if (state == null)
            {
                state = skillMachine.AddState(stateName, GetSkillStatePosition(syncedCount));
            }

            state.motion = module.AnimationClip;
            state.tag = "Action";
            // 스킬 모션 중에는 공격/패링/다른 스킬로 캔슬되지 않도록 Skill 타입 액션락 Behaviour를 자동 부착한다.
            EnsureActionBehaviour(state, PlayerActionLockType.Skill, false);

            EnsureAnyStateTriggerTransition(root, state, module.AnimationTrigger);
            EnsureTimedExitTransition(state, idleState);
            syncedCount++;
        }
    }

    private static void RemoveExcludedSkillAnimatorEntries(
        AnimatorController controller,
        AnimatorStateMachine root,
        AnimatorStateMachine skillMachine,
        string[] moduleGuids)
    {
        foreach (string moduleGuid in moduleGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(moduleGuid);
            PlayerAbilityModule module = AssetDatabase.LoadAssetAtPath<PlayerAbilityModule>(path);
            if (module == null || module.IncludeInRewardPool)
            {
                continue;
            }

            string stateName = GetModuleStateName(module);
            if (!string.IsNullOrWhiteSpace(stateName))
            {
                AnimatorState state = FindState(skillMachine, stateName);
                if (state != null)
                {
                    RemoveAnyStateTransitionsTo(root, state);
                    skillMachine.RemoveState(state);
                }
            }

            if (!string.IsNullOrWhiteSpace(module.AnimationTrigger) &&
                !IsTriggerUsedByIncludedModule(module.AnimationTrigger, moduleGuids))
            {
                RemoveAnyStateTransitionsWithTrigger(root, module.AnimationTrigger);
                RemoveParameter(controller, module.AnimationTrigger);
            }
        }
    }

    private static void ClearSkillActions(
        AnimatorController controller,
        AnimatorStateMachine root,
        AnimatorStateMachine skillMachine,
        string[] moduleGuids)
    {
        List<AnimatorState> states = new List<AnimatorState>();
        foreach (ChildAnimatorState child in skillMachine.states)
        {
            states.Add(child.state);
        }

        foreach (AnimatorState state in states)
        {
            RemoveAnyStateTransitionsTo(root, state);
            skillMachine.RemoveState(state);
        }

        foreach (string moduleGuid in moduleGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(moduleGuid);
            PlayerAbilityModule module = AssetDatabase.LoadAssetAtPath<PlayerAbilityModule>(path);
            if (module != null && !string.IsNullOrWhiteSpace(module.AnimationTrigger))
            {
                RemoveAnyStateTransitionsWithTrigger(root, module.AnimationTrigger);
                RemoveParameter(controller, module.AnimationTrigger);
            }
        }
    }

    private static string GetModuleStateName(PlayerAbilityModule module)
    {
        if (!string.IsNullOrWhiteSpace(module.AnimationStateName))
        {
            return module.AnimationStateName;
        }

        if (!string.IsNullOrWhiteSpace(module.AnimationTrigger))
        {
            return module.AnimationTrigger;
        }

        return module.AnimationClip != null ? module.AnimationClip.name : string.Empty;
    }

    private static bool IsTriggerUsedByIncludedModule(string triggerName, string[] moduleGuids)
    {
        foreach (string moduleGuid in moduleGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(moduleGuid);
            PlayerAbilityModule module = AssetDatabase.LoadAssetAtPath<PlayerAbilityModule>(path);
            if (module != null &&
                module.IncludeInRewardPool &&
                module.AnimationTrigger == triggerName)
            {
                return true;
            }
        }

        return false;
    }

    private static void RemoveAnyStateTransitionsTo(AnimatorStateMachine root, AnimatorState destination)
    {
        foreach (AnimatorStateTransition transition in root.anyStateTransitions)
        {
            if (transition.destinationState == destination)
            {
                root.RemoveAnyStateTransition(transition);
            }
        }
    }

    private static void RemoveAnyStateTransitionsWithTrigger(AnimatorStateMachine root, string triggerName)
    {
        foreach (AnimatorStateTransition transition in root.anyStateTransitions)
        {
            foreach (AnimatorCondition condition in transition.conditions)
            {
                if (condition.parameter == triggerName)
                {
                    root.RemoveAnyStateTransition(transition);
                    break;
                }
            }
        }
    }

    private static void RemoveParameter(AnimatorController controller, string name)
    {
        foreach (AnimatorControllerParameter parameter in controller.parameters)
        {
            if (parameter.name == name)
            {
                controller.RemoveParameter(parameter);
                return;
            }
        }
    }

    private static Vector3 GetSkillStatePosition(int index)
    {
        return new Vector3(260f, 20f + index * 70f, 0f);
    }

    private static void SetupDamageAndDownStates(AnimatorStateMachine root)
    {
        // 피격 상태도 액션락 대상이다.
        // 공격/스킬 중 맞았을 때 이전 State의 Exit가 피격 락을 풀지 못하도록 Impact 타입 Behaviour를 붙인다.
        AnimatorState idleState = FindState(root, "idle1");
        AnimatorState impactState = EnsureState(root, Impact, "Assets/04. Images/Animation/Great Sword Impact.fbx", new Vector3(80f, 420f, 0f), "Action");
        AnimatorState parryImpactState = EnsureState(root, Impact2, "Assets/04. Images/Animation/Great Sword Impact2.fbx", new Vector3(300f, 420f, 0f), "Action");
        AnimatorState deathState = EnsureState(root, Death, "Assets/04. Images/Animation/Great Sword Death.fbx", new Vector3(520f, 420f, 0f), "Action");
        AnimatorState crawlingState = EnsureState(root, "Crawling", "Assets/04. Images/Animation/Crawling.fbx", new Vector3(740f, 420f, 0f), string.Empty);
        EnsureActionBehaviour(impactState, PlayerActionLockType.Impact, false);
        EnsureActionBehaviour(parryImpactState, PlayerActionLockType.Impact, false);

        EnsureAnyStateTriggerTransition(root, impactState, Impact);
        EnsureAnyStateTriggerTransition(root, parryImpactState, Impact2);
        EnsureAnyStateTriggerTransition(root, deathState, Death);
        EnsureTimedExitTransition(impactState, idleState);
        EnsureTimedExitTransition(parryImpactState, idleState);
        EnsureTimedExitTransition(deathState, crawlingState);
    }

    private static AnimatorState EnsureState(AnimatorStateMachine root, string stateName, string clipPath, Vector3 position, string tag)
    {
        AnimatorState state = FindState(root, stateName);
        if (state == null)
        {
            state = root.AddState(stateName, position);
        }

        state.motion = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
        state.tag = tag;
        return state;
    }

    private static AnimatorStateMachine FindStateMachine(AnimatorStateMachine root, string name)
    {
        foreach (ChildAnimatorStateMachine child in root.stateMachines)
        {
            if (child.stateMachine.name == name)
            {
                return child.stateMachine;
            }
        }

        return null;
    }

    private static AnimatorState FindState(AnimatorStateMachine machine, string name)
    {
        foreach (ChildAnimatorState child in machine.states)
        {
            if (child.state.name == name)
            {
                return child.state;
            }
        }

        return null;
    }

    private static void ConfigureBlendTree(BlendTree tree)
    {
        tree.blendType = BlendTreeType.FreeformDirectional2D;
        tree.blendParameter = LockMoveX;
        tree.blendParameterY = LockMoveY;
        tree.useAutomaticThresholds = false;

        tree.children = new[]
        {
            CreateChild("Assets/04. Images/Animation/Great Sword Idle.fbx", new Vector2(0f, 0f)),
            CreateChild("Assets/04. Images/Animation/Great Sword Walk.fbx", new Vector2(0f, 1f)),
            CreateChild("Assets/04. Images/Animation/Great Sword Walk2.fbx", new Vector2(0f, -1f)),
            CreateChild("Assets/04. Images/Animation/Great Sword Strafe.fbx", new Vector2(-1f, 0f)),
            CreateChild("Assets/04. Images/Animation/Great Sword Strafe2.fbx", new Vector2(1f, 0f)),
            CreateChild("Assets/04. Images/Animation/Great Sword Strafe3.fbx", new Vector2(-2f, 0f)),
            CreateChild("Assets/04. Images/Animation/Great Sword Strafe4.fbx", new Vector2(2f, 0f)),
        };
    }

    private static ChildMotion CreateChild(string clipPath, Vector2 position)
    {
        return new ChildMotion
        {
            motion = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath),
            position = position,
            threshold = 0f,
            timeScale = 1f,
            cycleOffset = 0f,
            directBlendParameter = string.Empty,
            mirror = false
        };
    }

    private static void EnsureAnyStateTransition(AnimatorStateMachine root, AnimatorState lockOnState)
    {
        foreach (AnimatorStateTransition transition in root.anyStateTransitions)
        {
            if (transition.destinationState == lockOnState)
            {
                return;
            }
        }

        AnimatorStateTransition anyToLockOn = root.AddAnyStateTransition(lockOnState);
        anyToLockOn.hasExitTime = false;
        anyToLockOn.duration = 0.08f;
        anyToLockOn.canTransitionToSelf = false;
        anyToLockOn.AddCondition(AnimatorConditionMode.If, 0f, IsLockOn);
    }

    private static void EnsureAnyStateTriggerTransition(AnimatorStateMachine root, AnimatorState destination, string triggerName)
    {
        foreach (AnimatorStateTransition transition in root.anyStateTransitions)
        {
            if (transition.destinationState != destination)
            {
                continue;
            }

            foreach (AnimatorCondition condition in transition.conditions)
            {
                if (condition.parameter == triggerName)
                {
                    transition.hasExitTime = false;
                    transition.duration = 0.05f;
                    transition.canTransitionToSelf = !IsBasicAttackComboTrigger(triggerName);
                    return;
                }
            }
        }

        AnimatorStateTransition anyToSkill = root.AddAnyStateTransition(destination);
        anyToSkill.hasExitTime = false;
        anyToSkill.duration = 0.05f;
        anyToSkill.canTransitionToSelf = !IsBasicAttackComboTrigger(triggerName);
        anyToSkill.AddCondition(AnimatorConditionMode.If, 0f, triggerName);
    }

    private static bool IsBasicAttackComboTrigger(string triggerName)
    {
        return triggerName == Attack2 || triggerName == Attack3 || triggerName == Attack4;
    }

    private static void EnsureExitTransition(AnimatorState lockOnState, AnimatorState idleState)
    {
        if (idleState == null)
        {
            return;
        }

        foreach (AnimatorStateTransition transition in lockOnState.transitions)
        {
            if (transition.destinationState == idleState)
            {
                return;
            }
        }

        AnimatorStateTransition lockOnToIdle = lockOnState.AddTransition(idleState);
        lockOnToIdle.hasExitTime = false;
        lockOnToIdle.duration = 0.08f;
        lockOnToIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, IsLockOn);
    }

    private static void SetupBuiltInActionStates(AnimatorStateMachine root)
    {
        // 기본 공격 콤보는 slash2 -> slash3 -> slash4 순서로 사용한다.
        // slash2, slash3만 애니메이션 중반 이후 다음 콤보 선입력을 열어준다.
        AnimatorState idleState = FindState(root, "idle1");
        AnimatorState slash2 = EnsureState(root, "slash2", "Assets/04. Images/Animation/Great Sword Slash2.fbx", new Vector3(280f, 20f, 0f), "Action");
        AnimatorState slash3 = EnsureState(root, "slash3", "Assets/04. Images/Animation/Great Sword Slash3.fbx", new Vector3(500f, 20f, 0f), "Action");
        AnimatorState slash4 = EnsureState(root, "slash4", "Assets/04. Images/Animation/Great Sword Slash4.fbx", new Vector3(720f, 20f, 0f), "Action");

        EnsureAnyStateTriggerTransition(root, slash2, Attack2);
        EnsureAnyStateTriggerTransition(root, slash3, Attack3);
        EnsureAnyStateTriggerTransition(root, slash4, Attack4);
        EnsureActionBehaviour(slash2, PlayerActionLockType.Attack, true);
        EnsureActionBehaviour(slash3, PlayerActionLockType.Attack, true);
        EnsureActionBehaviour(slash4, PlayerActionLockType.Attack, false);
        EnsureTimedExitTransition(slash2, idleState);
        EnsureTimedExitTransition(slash3, idleState);
        EnsureTimedExitTransition(slash4, idleState);
        SetActionTag(root, "Jump");
        EnsureActionBehaviour(FindState(root, "Jump"), PlayerActionLockType.Jump, false);
        EnsureActionBehaviour(FindState(root, "blocking1"), PlayerActionLockType.Parry, false);
        EnsureActionBehaviour(FindState(root, "Sprinting Forward Roll"), PlayerActionLockType.Roll, false);
    }

    private static void EnsureActionBehaviour(AnimatorState state, PlayerActionLockType lockType, bool opensComboInput)
    {
        // StateMachineBehaviour는 Animator State에 붙는 스크립트다.
        // 여기서 타입까지 자동 지정해두면 Inspector에서 문자열 이름을 직접 입력하지 않아도 액션락이 맞물린다.
        if (state == null)
        {
            return;
        }

        PlayerActionStateBehaviour behaviour = null;
        foreach (StateMachineBehaviour existing in state.behaviours)
        {
            if (existing is PlayerActionStateBehaviour actionBehaviour)
            {
                behaviour = actionBehaviour;
                break;
            }
        }

        if (behaviour == null)
        {
            behaviour = state.AddStateMachineBehaviour<PlayerActionStateBehaviour>();
        }

        behaviour.Configure(lockType, opensComboInput, 0.5f);
        EditorUtility.SetDirty(behaviour);
    }

    private static void SetActionTag(AnimatorStateMachine root, string stateName)
    {
        AnimatorState state = FindState(root, stateName);
        if (state != null)
        {
            state.tag = "Action";
        }
    }

    private static void EnsureTimedExitTransition(AnimatorState state, AnimatorState idleState)
    {
        if (idleState == null)
        {
            return;
        }

        foreach (AnimatorStateTransition transition in state.transitions)
        {
            if (transition.destinationState == idleState && transition.hasExitTime)
            {
                return;
            }
        }

        AnimatorStateTransition exitToIdle = state.AddTransition(idleState);
        exitToIdle.hasExitTime = true;
        exitToIdle.exitTime = 0.95f;
        exitToIdle.duration = 0.08f;
    }

    private static void RemoveOldLockOnStates(AnimatorStateMachine root)
    {
        // 예전 방식의 개별 락온 이동 State가 남아 있으면 Blend Tree와 역할이 겹치므로 정리한다.
        // 이동 애니메이션은 LockOn Blend Tree 하나에서 방향 파라미터로 선택한다.
        string[] oldStateNames =
        {
            "Great Sword Walk",
            "Great Sword Walk2",
            "Great Sword Strafe",
            "Great Sword Strafe2",
            "Great Sword Strafe3",
            "Great Sword Strafe4"
        };

        foreach (string stateName in oldStateNames)
        {
            AnimatorState state = FindState(root, stateName);
            if (state != null)
            {
                root.RemoveState(state);
            }
        }
    }
}
