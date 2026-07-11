using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using System.Collections.Generic;

public static class PlayerAnimatorSetupTool
{
    // 플레이어 Animator Controller를 에디터 메뉴에서 자동 보정하기 위한 도구.
    // 기본 전투/락온 세팅과 액티브 스킬 모듈 동기화를 분리해, 새 스킬 추가 시 기본 Animator 구성이 불필요하게 덮이지 않도록 한다.
    private const string ControllerPath = "Assets/07. Animations/PlayerCtrl.controller";

    private const string IsMoving = "IsMoving";
    private const string IsRunning = "IsRunning";
    private const string MoveSpeed = "MoveSpeed";
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
    private const string Jump2 = "Jump2";
    private const string Attack2 = "Attack2";
    private const string Attack3 = "Attack3";
    private const string Attack4 = "Attack4";

    private const string LockOnMachineName = "LockOn Movement";
    private const string LegacyLockOnLayerName = "LockOn";
    private const string LockOnBlendStateName = "LockOn Blend Tree";
    private const string NormalLocomotionBlendStateName = "Locomotion Blend Tree";
    private const string CombatMachineName = "Combat Actions";
    private const string HitAndDeathMachineName = "Hit & Death";
    private const string SkillMachineName = "Skill Actions";
    // SkillModule은 런타임 Resources.Load와 에디터 도구가 같은 에셋을 사용하도록
    // 반드시 Resources/SkillModule 경로를 기준으로 검색한다.
    private const string SkillModuleFolder = "Assets/02. Scripts/Player/Abilities/Resources/SkillModule";
    private const float ComboInputOpenNormalizedTime = 0.72f;

    // 이전에는 Unity 스크립트 리로드 때마다 자동 마이그레이션을 실행했다.
    // 폴더 이동이나 컴파일 중에는 에셋/스크립트 참조가 잠시 null이 될 수 있어
    // Animator Controller가 의도치 않게 수정되거나 에디터 오류가 반복될 수 있으므로 수동 메뉴로만 실행한다.
    [MenuItem("Tools/ServerSouls/Migrate Player Animator State Behaviours")]
    private static void MigrateCombinedStateBehavioursFromMenu()
    {
        MigrateCombinedStateBehaviours();
    }

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
        RemoveLegacyLockOnLayer(controller);
        AnimatorStateMachine root = controller.layers[0].stateMachine;
        ConfigureNormalLocomotion(controller, root);
        AnimatorStateMachine lockOnMachine = FindStateMachine(root, LockOnMachineName)
            ?? root.AddStateMachine(LockOnMachineName, new Vector3(640f, 40f, 0f));
        RemoveDirectStates(lockOnMachine, "slash2", "slash3", "slash4", "Jump", "Jump2", "blocking1", "blocking2", "blocking3", "Sprinting Forward Roll");

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
        SetupBuiltInActionStates(root, lockOnState);
        RemoveMovementBoolTransitions(root);
        RemoveParameter(controller, IsMoving);
        RemoveParameter(controller, IsRunning);
        RemoveOldNormalMovementStates(root);
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

    [MenuItem("Tools/ServerSouls/Pull Animator Skill Speeds To Ability Modules")]
    public static void PullAnimatorSkillSpeedsToAbilityModules()
    {
        if (!TryLoadController(out AnimatorController controller))
        {
            return;
        }

        AnimatorStateMachine root = controller.layers[0].stateMachine;
        AnimatorStateMachine skillMachine = FindStateMachine(root, SkillMachineName);
        if (skillMachine == null)
        {
            Debug.LogWarning($"Skill state machine not found: {SkillMachineName}");
            return;
        }

        int updatedCount = 0;
        string[] moduleGuids = PlayerAbilityAssetSearch.FindAbilityAssetGuids(new[] { SkillModuleFolder });
        foreach (string moduleGuid in moduleGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(moduleGuid);
            PlayerAbilityModule module = AssetDatabase.LoadAssetAtPath<PlayerAbilityModule>(path);
            if (module == null)
            {
                continue;
            }

            AnimatorState state = FindState(skillMachine, GetModuleStateName(module));
            if (state == null)
            {
                continue;
            }

            SerializedObject serializedModule = new SerializedObject(module);
            SerializedProperty animationSpeed = serializedModule.FindProperty("animationSpeed");
            if (animationSpeed == null)
            {
                continue;
            }

            float pulledSpeed = Mathf.Max(0.01f, state.speed);
            if (Mathf.Approximately(animationSpeed.floatValue, pulledSpeed))
            {
                continue;
            }

            animationSpeed.floatValue = pulledSpeed;
            serializedModule.ApplyModifiedProperties();
            EditorUtility.SetDirty(module);
            updatedCount++;
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"Animator skill state speeds pulled to ability modules. Updated: {updatedCount}");
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
        AddParameter(controller, MoveSpeed, AnimatorControllerParameterType.Float);
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
        AddParameter(controller, Jump2, AnimatorControllerParameterType.Trigger);
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

    private static void RemoveLegacyLockOnLayer(AnimatorController controller)
    {
        for (int i = controller.layers.Length - 1; i >= 1; i--)
        {
            if (controller.layers[i].name == LegacyLockOnLayerName)
            {
                controller.RemoveLayer(i);
            }
        }
    }

    private static void SetupSkillActionsFromModules(AnimatorController controller, AnimatorStateMachine root)
    {
        // 액티브 모듈만 Animator에 연결한다.
        // 패시브 모듈은 획득 즉시 효과만 적용하므로 애니메이션 State나 Trigger를 만들 필요가 없다.
        AnimatorStateMachine skillMachine = FindStateMachine(root, SkillMachineName)
            ?? root.AddStateMachine(SkillMachineName, new Vector3(640f, 280f, 0f));

        AnimatorState idleState = FindState(root, "idle1");
        string[] moduleGuids = PlayerAbilityAssetSearch.FindAbilityAssetGuids(new[] { SkillModuleFolder });
        ClearSkillActions(controller, root, skillMachine, moduleGuids);
        int syncedCount = 0;

        foreach (string moduleGuid in moduleGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(moduleGuid);
            PlayerAbilityModule module = AssetDatabase.LoadAssetAtPath<PlayerAbilityModule>(path);
            if (module == null || !module.UnlockedSkill)
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
            state.speed = module.AnimationSpeed;
            state.tag = "Action";
            // 스킬 모션 중에는 공격/패링/다른 스킬로 캔슬되지 않도록 Skill 타입 액션락 Behaviour를 자동 부착한다.
            EnsureActionBehaviour(
                state,
                StateActionLockType.Skill,
                module.OpensComboInput,
                delaysStaminaRecovery: module.DelaysStaminaRecovery,
                usesRootMotion: module.UsesRootMotion,
                comboInputOpenNormalizedTime: module.ComboInputOpenNormalizedTime,
                preserveExistingInspectorValues: false);

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
            if (module == null || module.UnlockedSkill)
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
                module.UnlockedSkill &&
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
        AnimatorStateMachine hitAndDeathMachine = FindStateMachine(root, HitAndDeathMachineName)
            ?? root.AddStateMachine(HitAndDeathMachineName, new Vector3(360f, 300f, 0f));
        RemoveDirectStates(root, Impact, Impact2, Death, "Crawling");

        AnimatorState idleState = FindState(root, "idle1");
        AnimatorState impactState = EnsureState(hitAndDeathMachine, Impact, "Assets/04. Images/Animation/Great Sword Impact.fbx", new Vector3(80f, 80f, 0f), "Action");
        AnimatorState parryImpactState = EnsureState(hitAndDeathMachine, Impact2, "Assets/04. Images/Animation/Great Sword Impact2.fbx", new Vector3(300f, 80f, 0f), "Action");
        AnimatorState deathState = EnsureState(hitAndDeathMachine, Death, "Assets/04. Images/Animation/Great Sword Death.fbx", new Vector3(520f, 80f, 0f), "Action");
        AnimatorState crawlingState = EnsureState(hitAndDeathMachine, "Crawling", "Assets/04. Images/Animation/Crawling.fbx", new Vector3(740f, 80f, 0f), string.Empty);
        EnsureActionBehaviour(impactState, StateActionLockType.Impact, false);
        EnsureActionBehaviour(parryImpactState, StateActionLockType.Impact, false, enablesInvincibility: true);
        EnsureActionBehaviour(deathState, StateActionLockType.Death, false);
        EnsureCrawlingCapeResetBehaviour(crawlingState);
        hitAndDeathMachine.defaultState = impactState;

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

    private static void ConfigureNormalLocomotion(AnimatorController controller, AnimatorStateMachine root)
    {
        AnimatorState locomotionState = FindState(root, "idle1");
        if (locomotionState == null)
        {
            locomotionState = root.AddState("idle1", new Vector3(80f, 80f, 0f));
        }

        BlendTree tree = locomotionState.motion as BlendTree;
        if (tree == null)
        {
            tree = new BlendTree { name = NormalLocomotionBlendStateName };
            AssetDatabase.AddObjectToAsset(tree, controller);
            locomotionState.motion = tree;
        }

        tree.name = NormalLocomotionBlendStateName;
        tree.blendType = BlendTreeType.Simple1D;
        tree.blendParameter = MoveSpeed;
        tree.useAutomaticThresholds = false;
        tree.children = new[]
        {
            CreateThresholdChild("Assets/04. Images/Animation/Great Sword Idle.fbx", 0f),
            CreateThresholdChild("Assets/04. Images/Animation/Great Sword Walk.fbx", 0.5f),
            CreateThresholdChild("Assets/04. Images/Animation/Great Sword Run.fbx", 1f),
        };

        locomotionState.tag = string.Empty;
        if (root.defaultState == null)
        {
            root.defaultState = locomotionState;
        }
    }

    private static void ConfigureBlendTree(BlendTree tree)
    {
        tree.blendType = BlendTreeType.FreeformCartesian2D;
        tree.blendParameter = LockMoveX;
        tree.blendParameterY = LockMoveY;
        tree.useAutomaticThresholds = false;

        tree.children = new[]
        {
            CreateChild("Assets/04. Images/Animation/Great Sword Idle.fbx", new Vector2(0f, 0f)),
            CreateChild("Assets/04. Images/Animation/Great Sword Walk.fbx", new Vector2(0f, 1f)),
            CreateChild("Assets/04. Images/Animation/Great Sword Walk2.fbx", new Vector2(0f, -1f)),
            CreateChild("Assets/04. Images/Animation/Great Sword Run.fbx", new Vector2(0f, 2f)),
            CreateChild("Assets/04. Images/Animation/Great Sword Run2.fbx", new Vector2(0f, -2f)),
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

    private static ChildMotion CreateThresholdChild(string clipPath, float threshold)
    {
        return new ChildMotion
        {
            motion = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath),
            position = Vector2.zero,
            threshold = threshold,
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

    private static void EnsureStateTriggerTransition(AnimatorState source, AnimatorState destination, string triggerName, float duration)
    {
        if (source == null || destination == null)
        {
            return;
        }

        foreach (AnimatorStateTransition transition in source.transitions)
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
                    transition.hasFixedDuration = true;
                    transition.duration = duration;
                    transition.canTransitionToSelf = false;
                    transition.interruptionSource = TransitionInterruptionSource.None;
                    transition.orderedInterruption = true;
                    return;
                }
            }
        }

        AnimatorStateTransition stateTransition = source.AddTransition(destination);
        stateTransition.hasExitTime = false;
        stateTransition.hasFixedDuration = true;
        stateTransition.duration = duration;
        stateTransition.canTransitionToSelf = false;
        stateTransition.interruptionSource = TransitionInterruptionSource.None;
        stateTransition.orderedInterruption = true;
        stateTransition.AddCondition(AnimatorConditionMode.If, 0f, triggerName);
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

    private static void SetupBuiltInActionStates(AnimatorStateMachine root, AnimatorState lockOnState)
    {
        // 기본 공격 콤보는 slash2 -> slash3 -> slash4 순서로 사용한다.
        // slash2, slash3만 애니메이션 중반 이후 다음 콤보 선입력을 열어준다.
        AnimatorStateMachine combatMachine = FindStateMachine(root, CombatMachineName)
            ?? root.AddStateMachine(CombatMachineName, new Vector3(360f, 160f, 0f));
        RemoveDirectStates(root, "slash2", "slash3", "slash4", "Jump", "Jump2", "blocking1", "blocking2", "blocking3", "Sprinting Forward Roll");

        AnimatorState idleState = FindState(root, "idle1");
        AnimatorState slash2 = EnsureState(combatMachine, "slash2", "Assets/04. Images/Animation/Great Sword Slash2.fbx", new Vector3(80f, 80f, 0f), "Action");
        AnimatorState slash3 = EnsureState(combatMachine, "slash3", "Assets/04. Images/Animation/Great Sword Slash3.fbx", new Vector3(300f, 80f, 0f), "Action");
        AnimatorState slash4 = EnsureState(combatMachine, "slash4", "Assets/04. Images/Animation/Great Sword Slash4.fbx", new Vector3(520f, 80f, 0f), "Action");
        AnimatorState jumpState = EnsureState(combatMachine, "Jump", "Assets/04. Images/Animation/Great Sword Jump.fbx", new Vector3(80f, 220f, 0f), "Action");
        AnimatorState jump2State = EnsureState(combatMachine, "Jump2", "Assets/04. Images/Animation/Great Sword Jump2Fix.fbx", new Vector3(300f, 220f, 0f), "Action");
        AnimatorState parryRaiseState = EnsureState(combatMachine, "blocking1", "Assets/04. Images/Animation/Great Sword Blocking1.fbx", new Vector3(520f, 220f, 0f), "Action");
        AnimatorState parryGuardState = EnsureState(combatMachine, "blocking2", "Assets/04. Images/Animation/Great Sword Blocking2.fbx", new Vector3(740f, 220f, 0f), "Action");
        AnimatorState parryLowerState = EnsureState(combatMachine, "blocking3", "Assets/04. Images/Animation/Great Sword Blocking3.fbx", new Vector3(960f, 220f, 0f), "Action");
        AnimatorState rollState = EnsureState(combatMachine, "Sprinting Forward Roll", "Assets/04. Images/Animation/Sprinting Forward Roll(Root).fbx", new Vector3(1180f, 220f, 0f), "Action");
        combatMachine.defaultState = slash2;

        EnsureAnyStateTriggerTransition(root, slash2, Attack2);
        EnsureAnyStateTriggerTransition(root, slash3, Attack3);
        EnsureAnyStateTriggerTransition(root, slash4, Attack4);
        EnsureAnyStateTriggerTransition(root, jumpState, Jump);
        EnsureAnyStateTriggerTransition(root, jump2State, Jump2);
        EnsureAnyStateTriggerTransition(root, parryRaiseState, Parry);
        EnsureAnyStateTriggerTransition(root, rollState, Roll);
        EnsureActionBehaviour(slash2, StateActionLockType.Attack, true, delaysStaminaRecovery: true);
        EnsureActionBehaviour(slash3, StateActionLockType.Attack, true, delaysStaminaRecovery: true);
        EnsureActionBehaviour(slash4, StateActionLockType.Attack, false, delaysStaminaRecovery: true);
        EnsureActionBehaviour(jumpState, StateActionLockType.Jump, false, delaysStaminaRecovery: true);
        EnsureActionBehaviour(
            jump2State,
            StateActionLockType.Jump,
            false,
            delaysStaminaRecovery: true,
            usesRootMotion: true);
        EnsureActionBehaviour(parryRaiseState, StateActionLockType.Parry, false, true);
        EnsureActionBehaviour(parryGuardState, StateActionLockType.Parry, false, true);
        EnsureActionBehaviour(
            parryLowerState,
            StateActionLockType.Parry,
            false,
            true,
            delaysStaminaRecovery: true);
        EnsureActionBehaviour(
            rollState,
            StateActionLockType.Roll,
            false,
            delaysStaminaRecovery: true,
            usesRootMotion: true);
        EnsureTimedExitTransition(slash2, idleState);
        EnsureTimedExitTransition(slash3, idleState);
        EnsureTimedExitTransition(slash4, idleState);
        EnsureTimedExitTransition(jumpState, idleState);
        EnsureTimedExitTransition(jump2State, idleState);
        RemoveAllStateTransitions(parryRaiseState);
        RemoveAllStateTransitions(parryGuardState);
        RemoveAllStateTransitions(parryLowerState);
        EnsureTimedExitTransition(parryRaiseState, parryGuardState, 0.95f, 0.02f);
        EnsureTimedExitTransition(parryGuardState, parryLowerState, 0.95f, 0.02f);
        EnsureTimedExitTransition(parryLowerState, idleState);
        EnsureParryImpactExitTransition(root, parryLowerState);
        EnsureTimedExitTransition(rollState, idleState);
    }

    private static void EnsureActionBehaviour(
        AnimatorState state,
        StateActionLockType lockType,
        bool opensComboInput,
        bool enablesParryGuard = false,
        bool enablesInvincibility = false,
        bool delaysStaminaRecovery = false,
        bool usesRootMotion = false,
        float comboInputOpenNormalizedTime = ComboInputOpenNormalizedTime,
        bool preserveExistingInspectorValues = true)
    {
        // StateMachineBehaviour는 Animator State에 붙는 스크립트다.
        // 여기서 타입까지 자동 지정해두면 Inspector에서 문자열 이름을 직접 입력하지 않아도 액션락이 맞물린다.
        if (state == null)
        {
            return;
        }

        ActionLockStateBehaviour actionLock =
            EnsureBehaviour<ActionLockStateBehaviour>(state);
        actionLock.Configure(lockType);
        EditorUtility.SetDirty(actionLock);

        ConfigureOptionalComboBehaviour(
            state,
            opensComboInput,
            comboInputOpenNormalizedTime,
            preserveExistingInspectorValues);
        ConfigureOptionalBehaviour<GuardWindowStateBehaviour>(state, enablesParryGuard);
        ConfigureOptionalBehaviour<InvincibilityStateBehaviour>(state, enablesInvincibility);
        ConfigureOptionalBehaviour<StaminaRecoveryDelayStateBehaviour>(state, delaysStaminaRecovery);
        ConfigureOptionalBehaviour<AnimatorRootMotionStateBehaviour>(state, usesRootMotion);
    }

    private static void MigrateCombinedStateBehaviours()
    {
        if (!TryLoadController(out AnimatorController controller))
        {
            return;
        }

        bool changed = false;
        foreach (AnimatorControllerLayer layer in controller.layers)
        {
            changed |= MigrateCombinedStateBehaviours(layer.stateMachine);
        }

        if (!changed)
        {
            return;
        }

        SaveController(controller);
        Debug.Log("Player Animator StateBehaviours were migrated to single-purpose components.");
    }

    private static bool MigrateCombinedStateBehaviours(AnimatorStateMachine stateMachine)
    {
        bool changed = false;
        foreach (ChildAnimatorState childState in stateMachine.states)
        {
            AnimatorState state = childState.state;
            ActionLockStateBehaviour actionLock =
                FindBehaviour<ActionLockStateBehaviour>(state);
            if (actionLock == null)
            {
                continue;
            }

            bool hasLegacySettings =
                actionLock.LegacyOpensComboInput ||
                actionLock.LegacyEnablesParryGuard ||
                actionLock.LegacyEnablesInvincibility;
            if (hasLegacySettings && actionLock.LegacyOpensComboInput)
            {
                ConfigureOptionalComboBehaviour(
                    state,
                    true,
                    actionLock.LegacyComboInputOpenNormalizedTime);
            }

            if (hasLegacySettings && actionLock.LegacyEnablesParryGuard)
            {
                ConfigureOptionalBehaviour<GuardWindowStateBehaviour>(state, true);
            }

            if (hasLegacySettings && actionLock.LegacyEnablesInvincibility)
            {
                ConfigureOptionalBehaviour<InvincibilityStateBehaviour>(state, true);
            }

            if (hasLegacySettings)
            {
                actionLock.ClearLegacySettings();
                EditorUtility.SetDirty(actionLock);
                changed = true;
            }

            bool shouldDelayStamina =
                actionLock.LockType == StateActionLockType.Attack ||
                actionLock.LockType == StateActionLockType.Jump ||
                actionLock.LockType == StateActionLockType.Roll ||
                (actionLock.LockType == StateActionLockType.Parry && state.name == "blocking3");
            bool shouldUseRootMotion =
                state.name == "Jump2" ||
                state.name == "Sprinting Forward Roll";

            if (actionLock.LockType == StateActionLockType.Skill &&
                TryFindAbilityModuleForState(state.name, out PlayerAbilityModule module))
            {
                shouldDelayStamina = module.DelaysStaminaRecovery;
                shouldUseRootMotion = module.UsesRootMotion;
                ConfigureOptionalComboBehaviour(
                    state,
                    module.OpensComboInput,
                    module.ComboInputOpenNormalizedTime,
                    false);
            }

            changed |= SetOptionalBehaviour<StaminaRecoveryDelayStateBehaviour>(
                state,
                shouldDelayStamina);
            changed |= SetOptionalBehaviour<AnimatorRootMotionStateBehaviour>(
                state,
                shouldUseRootMotion);
        }

        foreach (ChildAnimatorStateMachine childStateMachine in stateMachine.stateMachines)
        {
            changed |= MigrateCombinedStateBehaviours(childStateMachine.stateMachine);
        }

        return changed;
    }

    private static bool TryFindAbilityModuleForState(
        string stateName,
        out PlayerAbilityModule matchingModule)
    {
        // 폴더 이동이나 삭제 중 도메인 리로드가 발생해도 FindAssets가 반복 오류를 내지 않게 막는다.
        if (!AssetDatabase.IsValidFolder(SkillModuleFolder))
        {
            matchingModule = null;
            return false;
        }

        string[] moduleGuids =
            PlayerAbilityAssetSearch.FindAbilityAssetGuids(new[] { SkillModuleFolder });
        foreach (string moduleGuid in moduleGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(moduleGuid);
            PlayerAbilityModule module = AssetDatabase.LoadAssetAtPath<PlayerAbilityModule>(path);
            if (module != null && GetModuleStateName(module) == stateName)
            {
                matchingModule = module;
                return true;
            }
        }

        matchingModule = null;
        return false;
    }

    private static void ConfigureOptionalComboBehaviour(
        AnimatorState state,
        bool enabled,
        float openNormalizedTime,
        bool preserveExistingValue = false)
    {
        ComboInputWindowStateBehaviour behaviour =
            FindBehaviour<ComboInputWindowStateBehaviour>(state);
        if (!enabled)
        {
            RemoveBehaviour(behaviour);
            return;
        }

        bool created = behaviour == null;
        behaviour ??= state.AddStateMachineBehaviour<ComboInputWindowStateBehaviour>();
        if (created || !preserveExistingValue)
        {
            behaviour.Configure(openNormalizedTime);
        }
        EditorUtility.SetDirty(behaviour);
    }

    private static void ConfigureOptionalBehaviour<T>(AnimatorState state, bool enabled)
        where T : StateMachineBehaviour
    {
        T behaviour = FindBehaviour<T>(state);
        if (!enabled)
        {
            RemoveBehaviour(behaviour);
            return;
        }

        behaviour ??= state.AddStateMachineBehaviour<T>();
        EditorUtility.SetDirty(behaviour);
    }

    private static bool SetOptionalBehaviour<T>(AnimatorState state, bool enabled)
        where T : StateMachineBehaviour
    {
        T existing = FindBehaviour<T>(state);
        if (enabled == (existing != null))
        {
            return false;
        }

        ConfigureOptionalBehaviour<T>(state, enabled);
        return true;
    }

    private static T EnsureBehaviour<T>(AnimatorState state)
        where T : StateMachineBehaviour
    {
        return FindBehaviour<T>(state) ?? state.AddStateMachineBehaviour<T>();
    }

    private static T FindBehaviour<T>(AnimatorState state)
        where T : StateMachineBehaviour
    {
        if (state == null)
        {
            return null;
        }

        foreach (StateMachineBehaviour existing in state.behaviours)
        {
            if (existing is T behaviour)
            {
                return behaviour;
            }
        }

        return null;
    }

    private static void RemoveBehaviour(StateMachineBehaviour behaviour)
    {
        if (behaviour != null)
        {
            Object.DestroyImmediate(behaviour, true);
        }
    }

    private static void EnsureCrawlingCapeResetBehaviour(AnimatorState state)
    {
        if (state == null)
        {
            return;
        }

        foreach (StateMachineBehaviour existing in state.behaviours)
        {
            if (existing is AnimatorStateResetBehaviour existingReset)
            {
                existingReset.Configure("Crawling");
                EditorUtility.SetDirty(existingReset);
                return;
            }
        }

        AnimatorStateResetBehaviour behaviour =
            state.AddStateMachineBehaviour<AnimatorStateResetBehaviour>();
        behaviour.Configure("Crawling");
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

    private static void EnsureTimedExitTransition(AnimatorState source, AnimatorState destination, float exitTime, float duration)
    {
        if (source == null || destination == null)
        {
            return;
        }

        foreach (AnimatorStateTransition transition in source.transitions)
        {
            if (transition.destinationState == destination && transition.hasExitTime)
            {
                transition.exitTime = exitTime;
                transition.hasFixedDuration = true;
                transition.duration = duration;
                transition.interruptionSource = TransitionInterruptionSource.None;
                transition.orderedInterruption = true;
                return;
            }
        }

        AnimatorStateTransition exitTransition = source.AddTransition(destination);
        exitTransition.hasExitTime = true;
        exitTransition.hasFixedDuration = true;
        exitTransition.exitTime = exitTime;
        exitTransition.duration = duration;
        exitTransition.interruptionSource = TransitionInterruptionSource.None;
        exitTransition.orderedInterruption = true;
    }

    private static void EnsureParryImpactExitTransition(AnimatorStateMachine root, AnimatorState parryLowerState)
    {
        AnimatorStateMachine hitAndDeathMachine = FindStateMachine(root, HitAndDeathMachineName);
        AnimatorState parryImpactState = hitAndDeathMachine != null ? FindState(hitAndDeathMachine, Impact2) : null;
        if (parryImpactState == null || parryLowerState == null)
        {
            return;
        }

        RemoveAllStateTransitions(parryImpactState);
        EnsureTimedExitTransition(parryImpactState, parryLowerState, 0.95f, 0.04f);
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
            "Great Sword Strafe4",
            "Great Sword Run2"
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

    private static void RemoveMovementBoolTransitions(AnimatorStateMachine machine)
    {
        RemoveAnyStateTransitionsWithParameter(machine, IsMoving);
        RemoveAnyStateTransitionsWithParameter(machine, IsRunning);

        foreach (ChildAnimatorState child in machine.states)
        {
            RemoveStateTransitionsWithParameter(child.state, IsMoving);
            RemoveStateTransitionsWithParameter(child.state, IsRunning);
        }

        foreach (ChildAnimatorStateMachine child in machine.stateMachines)
        {
            RemoveMovementBoolTransitions(child.stateMachine);
        }
    }

    private static void RemoveAnyStateTransitionsWithParameter(AnimatorStateMachine machine, string parameterName)
    {
        foreach (AnimatorStateTransition transition in machine.anyStateTransitions)
        {
            if (HasCondition(transition, parameterName))
            {
                machine.RemoveAnyStateTransition(transition);
            }
        }
    }

    private static void RemoveStateTransitionsWithParameter(AnimatorState state, string parameterName)
    {
        foreach (AnimatorStateTransition transition in state.transitions)
        {
            if (HasCondition(transition, parameterName))
            {
                state.RemoveTransition(transition);
            }
        }
    }

    private static void RemoveAllStateTransitions(AnimatorState state)
    {
        if (state == null)
        {
            return;
        }

        foreach (AnimatorStateTransition transition in state.transitions)
        {
            state.RemoveTransition(transition);
        }
    }

    private static bool HasCondition(AnimatorStateTransition transition, string parameterName)
    {
        foreach (AnimatorCondition condition in transition.conditions)
        {
            if (condition.parameter == parameterName)
            {
                return true;
            }
        }

        return false;
    }

    private static void RemoveOldNormalMovementStates(AnimatorStateMachine root)
    {
        string[] oldStateNames =
        {
            "walk1",
            "run1"
        };

        foreach (string stateName in oldStateNames)
        {
            AnimatorState state = FindState(root, stateName);
            if (state == null)
            {
                continue;
            }

            RemoveTransitionsToState(root, state);
            root.RemoveState(state);
        }
    }

    private static void RemoveDirectStates(AnimatorStateMachine root, params string[] stateNames)
    {
        foreach (string stateName in stateNames)
        {
            AnimatorState state = FindState(root, stateName);
            if (state == null)
            {
                continue;
            }

            RemoveTransitionsToState(root, state);
            root.RemoveState(state);
        }
    }

    private static void RemoveTransitionsToState(AnimatorStateMachine machine, AnimatorState destination)
    {
        foreach (AnimatorStateTransition transition in machine.anyStateTransitions)
        {
            if (transition.destinationState == destination)
            {
                machine.RemoveAnyStateTransition(transition);
            }
        }

        foreach (ChildAnimatorState child in machine.states)
        {
            foreach (AnimatorStateTransition transition in child.state.transitions)
            {
                if (transition.destinationState == destination)
                {
                    child.state.RemoveTransition(transition);
                }
            }
        }

        foreach (ChildAnimatorStateMachine child in machine.stateMachines)
        {
            RemoveTransitionsToState(child.stateMachine, destination);
        }
    }
}
