using UnityEngine;

public class SceneBGMPlayer : MonoBehaviour
{
    [Header("이 씬에서 재생할 배경음악")]
    public AudioClip sceneBGM;

    private void Start()
    {
        // 씬이 시작되자마자 매니저에게 BGM 재생을 요청합니다.
        if (sceneBGM != null && SoundManager.Instance != null)
        {
            SoundManager.Instance.PlayBGM(sceneBGM);
        }
    }
}