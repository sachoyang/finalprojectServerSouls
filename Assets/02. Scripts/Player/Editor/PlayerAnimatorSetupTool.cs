using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class PlayerAnimatorSetupTool
{
    private const string ControllerPath = "Assets/07. Animations/PlayerCtrl.controller";

    private const string IsLockOn = "IsLockOn";
    private const string LockMoveX = "LockMoveX";
    private const string LockMoveY = "LockMoveY";
    private const string LockMoveSpeed = "LockMoveSpeed";
    private const string Impact = "Impact";
    private const string Impact2 = "Impact2";
    private const string Death = "Death";
    private const string IsCrawling = "IsCrawling";
    private const string Attack2 = "Attack2";
    private const string Attack3 = "Attack3";
    private const string Attack4 = "Attack4";

    private const string LockOnMachineName = "LockOn Movement";
    private const string LockOnBlendStateName = "LockOn Blend Tree";
    private const string SkillMachineName = "Skill Actions";

    [MenuItem("Tools/ServerSouls/Setup Player LockOn Blend Tree")]
    public static void SetupLockOnBlendTree()
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            Debug.LogError($"Animator Controller not found: {ControllerPath}");
            return;
        }

        AddParameter(controller, IsLockOn, AnimatorControllerParameterType.Bool);
        AddParameter(controller, LockMoveX, AnimatorControllerParameterType.Float);
        AddParameter(controller, LockMoveY, AnimatorControllerParameterType.Float);
        AddParameter(controller, LockMoveSpeed, AnimatorControllerParameterType.Float);
        AddParameter(controller, Impact, AnimatorControllerParameterType.Trigger);
        AddParameter(controller, Impact2, AnimatorControllerParameterType.Trigger);
        AddParameter(controller, Death, AnimatorControllerParameterType.Trigger);
        AddParameter(controller, IsCrawling, AnimatorControllerParameterType.Bool);
        AddParameter(controller, Attack2, AnimatorControllerParameterType.Trigger);
        AddParameter(controller, Attack3, AnimatorControllerParameterType.Trigger);
        AddParameter(controller, Attack4, AnimatorControllerParameterType.Trigger);

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
        SetupSkillActions(controller, root);

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        Debug.Log("Player lock-on blend tree setup complete.");
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

    private static void SetupSkillActions(AnimatorController controller, AnimatorStateMachine root)
    {
        AnimatorStateMachine skillMachine = FindStateMachine(root, SkillMachineName)
            ?? root.AddStateMachine(SkillMachineName, new Vector3(640f, 280f, 0f));

        AnimatorState idleState = FindState(root, "idle1");
        SkillAnimation[] skills =
        {
            new SkillAnimation("SlideAttack", "SlideAttack", "Assets/04. Images/Animation/Great Sword Slide Attack.fbx", new Vector3(260f, 20f, 0f)),
            new SkillAnimation("HighSpinAttack", "HighSpinAttack", "Assets/04. Images/Animation/Great Sword High Spin Attack.fbx", new Vector3(260f, 90f, 0f)),
            new SkillAnimation("JumpAttack", "JumpAttack", "Assets/04. Images/Animation/Great Sword Jump Attack.fbx", new Vector3(260f, 160f, 0f)),
            new SkillAnimation("Heal", "Heal", "Assets/04. Images/Animation/Great Sword Casting.fbx", new Vector3(260f, 230f, 0f)),
            new SkillAnimation("StaminaUp", "StaminaUp", "Assets/04. Images/Animation/Great Sword Power Up.fbx", new Vector3(260f, 300f, 0f)),
        };

        foreach (SkillAnimation skill in skills)
        {
            AddParameter(controller, skill.TriggerName, AnimatorControllerParameterType.Trigger);

            AnimatorState state = FindState(skillMachine, skill.StateName);
            if (state == null)
            {
                state = skillMachine.AddState(skill.StateName, skill.Position);
            }

            state.motion = AssetDatabase.LoadAssetAtPath<AnimationClip>(skill.ClipPath);
            state.tag = "Action";
            EnsureActionBehaviour(state, false);

            EnsureAnyStateTriggerTransition(root, state, skill.TriggerName);
            EnsureTimedExitTransition(state, idleState);
        }
    }

    private static void SetupDamageAndDownStates(AnimatorStateMachine root)
    {
        AnimatorState idleState = FindState(root, "idle1");
        AnimatorState impactState = EnsureState(root, Impact, "Assets/04. Images/Animation/Great Sword Impact.fbx", new Vector3(80f, 420f, 0f), "Action");
        AnimatorState parryImpactState = EnsureState(root, Impact2, "Assets/04. Images/Animation/Great Sword Impact2.fbx", new Vector3(300f, 420f, 0f), "Action");
        AnimatorState deathState = EnsureState(root, Death, "Assets/04. Images/Animation/Great Sword Death.fbx", new Vector3(520f, 420f, 0f), "Action");
        AnimatorState crawlingState = EnsureState(root, "Crawling", "Assets/04. Images/Animation/Crawling.fbx", new Vector3(740f, 420f, 0f), string.Empty);
        EnsureActionBehaviour(impactState, false);
        EnsureActionBehaviour(parryImpactState, false);

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
                    transition.canTransitionToSelf = true;
                    return;
                }
            }
        }

        AnimatorStateTransition anyToSkill = root.AddAnyStateTransition(destination);
        anyToSkill.hasExitTime = false;
        anyToSkill.duration = 0.05f;
        anyToSkill.canTransitionToSelf = true;
        anyToSkill.AddCondition(AnimatorConditionMode.If, 0f, triggerName);
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
        AnimatorState idleState = FindState(root, "idle1");
        AnimatorState slash2 = EnsureState(root, "slash2", "Assets/04. Images/Animation/Great Sword Slash2.fbx", new Vector3(280f, 20f, 0f), "Action");
        AnimatorState slash3 = EnsureState(root, "slash3", "Assets/04. Images/Animation/Great Sword Slash3.fbx", new Vector3(500f, 20f, 0f), "Action");
        AnimatorState slash4 = EnsureState(root, "slash4", "Assets/04. Images/Animation/Great Sword Slash4.fbx", new Vector3(720f, 20f, 0f), "Action");

        EnsureAnyStateTriggerTransition(root, slash2, Attack2);
        EnsureAnyStateTriggerTransition(root, slash3, Attack3);
        EnsureAnyStateTriggerTransition(root, slash4, Attack4);
        EnsureActionBehaviour(slash2, true);
        EnsureActionBehaviour(slash3, true);
        EnsureActionBehaviour(slash4, false);
        EnsureTimedExitTransition(slash2, idleState);
        EnsureTimedExitTransition(slash3, idleState);
        EnsureTimedExitTransition(slash4, idleState);
        SetActionTag(root, "Jump");
        EnsureActionBehaviour(FindState(root, "Jump"), false);
        EnsureActionBehaviour(FindState(root, "blocking1"), false);
        EnsureActionBehaviour(FindState(root, "Sprinting Forward Roll"), false);
    }

    private static void EnsureActionBehaviour(AnimatorState state, bool opensComboInput)
    {
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

        behaviour.Configure(opensComboInput, 0.5f);
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

    private readonly struct SkillAnimation
    {
        public SkillAnimation(string triggerName, string stateName, string clipPath, Vector3 position)
        {
            TriggerName = triggerName;
            StateName = stateName;
            ClipPath = clipPath;
            Position = position;
        }

        public string TriggerName { get; }
        public string StateName { get; }
        public string ClipPath { get; }
        public Vector3 Position { get; }
    }
}
