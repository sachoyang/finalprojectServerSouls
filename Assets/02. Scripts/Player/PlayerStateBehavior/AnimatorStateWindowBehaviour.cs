using UnityEngine;

public abstract class AnimatorStateWindowBehaviour : AnimatorStateBehaviourBase
{
    [Header("Active Window")]
    [Tooltip("0 means the effect starts as soon as the state begins.")]
    [SerializeField, Range(0f, 1f)] private float startNormalizedTime;

    [Tooltip("1 means the effect lasts until the state exits.")]
    [SerializeField, Range(0f, 1f)] private float endNormalizedTime = 1f;

    private bool _active;
    private bool _finished;

    public void ConfigureWindow(float startTime, float endTime)
    {
        startNormalizedTime = Mathf.Clamp01(startTime);
        endNormalizedTime = Mathf.Clamp(endTime, startNormalizedTime, 1f);
    }

    public override void OnStateEnter(
        Animator animator,
        AnimatorStateInfo stateInfo,
        int layerIndex)
    {
        _active = false;
        _finished = false;
        if (startNormalizedTime <= 0f)
        {
            SetActive(animator, true);
        }
    }

    public override void OnStateUpdate(
        Animator animator,
        AnimatorStateInfo stateInfo,
        int layerIndex)
    {
        if (_finished)
        {
            return;
        }

        float normalizedTime = GetNormalizedTime(stateInfo);
        if (!_active && normalizedTime >= startNormalizedTime)
        {
            SetActive(animator, true);
        }

        if (_active && normalizedTime >= endNormalizedTime)
        {
            SetActive(animator, false);
            _finished = true;
        }
    }

    public override void OnStateExit(
        Animator animator,
        AnimatorStateInfo stateInfo,
        int layerIndex)
    {
        SetActive(animator, false);
        _finished = true;
    }

    protected abstract void OnWindowChanged(Animator animator, bool isActive);

    private void SetActive(Animator animator, bool isActive)
    {
        if (_active == isActive)
        {
            return;
        }

        _active = isActive;
        OnWindowChanged(animator, isActive);
    }
}
