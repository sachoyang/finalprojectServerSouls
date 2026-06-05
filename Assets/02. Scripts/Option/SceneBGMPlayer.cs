using UnityEngine;

public class SceneBGMPlayer : MonoBehaviour
{
    [Header("BGM")]
    [SerializeField] private AudioClip bgmClip;

    [Header("Options")]
    [SerializeField] private bool playOnStart = true;
    [SerializeField] private bool stopBgmIfClipIsEmpty = false;

    private void Start()
    {
        if (!playOnStart)
            return;

        if (AudioManager.Instance == null)
        {
            Debug.LogWarning("[SceneBGMPlayer] AudioManager가 없습니다.");
            return;
        }

        if (bgmClip != null)
        {
            AudioManager.Instance.PlayBGM(bgmClip);
        }
        else if (stopBgmIfClipIsEmpty)
        {
            AudioManager.Instance.StopBGM();
        }
    }
}