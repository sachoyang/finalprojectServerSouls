using System.Collections;
using UnityEngine;

public class BossSFXController : MonoBehaviour
{
    [Header("Profile")]
    [SerializeField] private BossSFXProfile profile;

    public void PlaySFX(AnimationClip animationClip)
    {
        if (profile == null)
        {
            Debug.LogWarning("[BossSFXController] BossSFXProfile이 연결되지 않았습니다.");
            return;
        }

        BossSFXEntry entry = profile.GetEntry(animationClip);

        if (entry == null)
        {
            Debug.LogWarning("[BossSFXController] AnimationClip에 연결된 SFX가 없습니다: " + animationClip.name);
            return;
        }

        if (entry.clips == null || entry.clips.Count == 0)
            return;

        for (int i = 0; i < entry.clips.Count; i++)
        {
            BossSFXClipData clipData = entry.clips[i];

            if (clipData == null || clipData.clip == null)
                continue;

            if (clipData.delay <= 0f)
                PlayClip(clipData);
            else
                StartCoroutine(PlayDelayedClip(clipData));
        }
    }

    private IEnumerator PlayDelayedClip(BossSFXClipData clipData)
    {
        yield return new WaitForSeconds(clipData.delay);
        PlayClip(clipData);
    }

    private void PlayClip(BossSFXClipData clipData)
    {
        if (AudioManager.Instance == null)
        {
            Debug.LogWarning("[BossSFXController] AudioManager가 없습니다.");
            return;
        }

        AudioManager.Instance.PlaySFX(clipData.clip, clipData.volume, clipData.pitch);
    }
}