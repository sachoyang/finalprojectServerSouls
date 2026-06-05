using UnityEngine;
using UnityEngine.Audio;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Mixer Groups")]
    [SerializeField] private AudioMixerGroup bgmMixerGroup;
    [SerializeField] private AudioMixerGroup sfxMixerGroup;

    [Header("Audio Sources")]
    [SerializeField] private AudioSource bgmSource;
    [SerializeField] private AudioSource sfxSource;

    [Header("Default Sounds")]
    [SerializeField] private AudioClip defaultButtonClickSound;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        SetupAudioSources();
    }

    private void SetupAudioSources()
    {
        if (bgmSource == null)
        {
            GameObject bgmObject = new GameObject("BGM Source");
            bgmObject.transform.SetParent(transform);
            bgmSource = bgmObject.AddComponent<AudioSource>();
        }

        if (sfxSource == null)
        {
            GameObject sfxObject = new GameObject("SFX Source");
            sfxObject.transform.SetParent(transform);
            sfxSource = sfxObject.AddComponent<AudioSource>();
        }

        bgmSource.loop = true;
        bgmSource.playOnAwake = false;

        sfxSource.loop = false;
        sfxSource.playOnAwake = false;

        if (bgmMixerGroup != null)
            bgmSource.outputAudioMixerGroup = bgmMixerGroup;

        if (sfxMixerGroup != null)
            sfxSource.outputAudioMixerGroup = sfxMixerGroup;
    }

    public void PlayBGM(AudioClip clip)
    {
        if (clip == null)
            return;

        if (bgmSource.clip == clip && bgmSource.isPlaying)
            return;

        bgmSource.clip = clip;
        bgmSource.Play();
    }

    public void StopBGM()
    {
        bgmSource.Stop();
        bgmSource.clip = null;
    }

    public void PlaySFX(AudioClip clip)
    {
        if (clip == null)
        {
            Debug.LogWarning("[AudioManager] 재생할 SFX AudioClip이 비어 있습니다.");
            return;
        }

        if (sfxSource == null)
            SetupAudioSources();

        Debug.Log("[AudioManager] SFX 재생: " + clip.name);
        sfxSource.PlayOneShot(clip);
    }

    public void PlayButtonClick()
    {
        if (defaultButtonClickSound == null)
        {
            Debug.LogWarning("[AudioManager] Default Button Click Sound가 연결되지 않았습니다.");
            return;
        }

        PlaySFX(defaultButtonClickSound);
    }

    [ContextMenu("Debug/Play Button Click Sound")]
    private void DebugPlayButtonClickSound()
    {
        PlayButtonClick();
    }

}