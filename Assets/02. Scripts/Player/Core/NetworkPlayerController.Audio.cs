using System;
using UnityEngine;

public partial class NetworkPlayerController
{
    [Header("Movement Audio")]
    [SerializeField, Min(0f)] private float minimumPlayerSoundInterval = 0.08f;

    private float _lastPlayerSoundTime = float.NegativeInfinity;

    // Animation Event 공용 함수.
    // Object Reference: AudioClip
    // Float: 볼륨(0 이하이면 1)
    // String: SoundCategory 이름(비어 있으면 PlayerSound)
    public void AnimationEvent_PlaySound(AnimationEvent animationEvent)
    {
        if (animationEvent == null)
        {
            return;
        }

        AudioClip clip = animationEvent.objectReferenceParameter as AudioClip;
        float volume = animationEvent.floatParameter > 0f
            ? animationEvent.floatParameter
            : 1f;
        SoundCategory category = ParseSoundCategory(
            animationEvent.stringParameter);

        if (!IsDominantAnimationEvent(animationEvent))
        {
            return;
        }

        PlayAnimationSound(
            clip,
            volume,
            category,
            category == SoundCategory.PlayerSound);
    }

    private void PlayAnimationSound(
        AudioClip clip,
        float volume,
        SoundCategory category,
        bool enforcePlayerSoundInterval)
    {
        if (clip == null ||
            !SoundManager.HasInstance ||
            (enforcePlayerSoundInterval &&
             Time.time - _lastPlayerSoundTime < minimumPlayerSoundInterval))
        {
            return;
        }

        if (enforcePlayerSoundInterval)
        {
            _lastPlayerSoundTime = Time.time;
        }

        SoundManager.Instance.PlaySFX_3D(
            clip,
            transform.position,
            category,
            volume);
    }

    private bool IsDominantAnimationEvent(AnimationEvent animationEvent)
    {
        AnimationClip sourceClip = animationEvent.animatorClipInfo.clip;
        if (animator == null || sourceClip == null)
        {
            return true;
        }

        float sourceWeight = animationEvent.animatorClipInfo.weight;
        AnimatorClipInfo[] activeClips = animator.GetCurrentAnimatorClipInfo(0);
        foreach (AnimatorClipInfo activeClip in activeClips)
        {
            if (activeClip.clip == null || activeClip.clip == sourceClip)
            {
                continue;
            }

            if (activeClip.weight > sourceWeight + Mathf.Epsilon)
            {
                return false;
            }

            // 정확히 같은 가중치일 때도 하나의 클립만 선택해 중복 재생을 막는다.
            if (Mathf.Approximately(activeClip.weight, sourceWeight) &&
                activeClip.clip.GetInstanceID() < sourceClip.GetInstanceID())
            {
                return false;
            }
        }

        return true;
    }

    private static SoundCategory ParseSoundCategory(string categoryName)
    {
        if (!string.IsNullOrWhiteSpace(categoryName) &&
            Enum.TryParse(
                categoryName,
                true,
                out SoundCategory category))
        {
            return category;
        }

        return SoundCategory.PlayerSound;
    }
}
