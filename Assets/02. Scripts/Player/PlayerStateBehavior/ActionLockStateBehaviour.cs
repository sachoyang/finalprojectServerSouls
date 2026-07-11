using UnityEngine;

public enum StateActionLockType
{
    None,
    Attack,
    Parry,
    Impact,
    Roll,
    Jump,
    Skill,
    Death
}

public sealed class ActionLockStateBehaviour : AnimatorStateBehaviourBase
{
    [SerializeField] private StateActionLockType actionLockType = StateActionLockType.None;

    // These fields only preserve values serialized by the previous combined behaviour.
    // The editor migration moves them to dedicated behaviours and then clears them.
    [SerializeField, HideInInspector] private bool opensComboInput;
    [SerializeField, HideInInspector] private float comboInputOpenNormalizedTime = 0.72f;
    [SerializeField, HideInInspector] private bool enablesParryGuard;
    [SerializeField, HideInInspector] private bool enablesInvincibility;

    public bool LegacyOpensComboInput => opensComboInput;
    public float LegacyComboInputOpenNormalizedTime => comboInputOpenNormalizedTime;
    public bool LegacyEnablesParryGuard => enablesParryGuard;
    public bool LegacyEnablesInvincibility => enablesInvincibility;
    public StateActionLockType LockType => actionLockType;

    public void Configure(StateActionLockType lockType)
    {
        actionLockType = lockType;
    }

    public void ClearLegacySettings()
    {
        opensComboInput = false;
        enablesParryGuard = false;
        enablesInvincibility = false;
    }

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        ForEachReceiver<IActionLockStateReceiver>(
            animator,
            receiver => receiver.BeginActionAnimation(actionLockType));
    }

    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        ForEachReceiver<IActionLockStateReceiver>(
            animator,
            receiver => receiver.EndActionAnimation(actionLockType));
    }
}
