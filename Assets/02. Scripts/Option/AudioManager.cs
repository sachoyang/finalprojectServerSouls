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

    public AudioClip CurrentBGM => bgmSource != null ? bgmSource.clip : null;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(transform.root.gameObject);

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
        bgmSource.spatialBlend = 0f;

        sfxSource.loop = false;
        sfxSource.playOnAwake = false;
        sfxSource.spatialBlend = 0f;

        if (bgmMixerGroup != null)
            bgmSource.outputAudioMixerGroup = bgmMixerGroup;

        if (sfxMixerGroup != null)
            sfxSource.outputAudioMixerGroup = sfxMixerGroup;
    }

    public void PlayBGM(AudioClip clip)
    {
        if (clip == null)
        {
            Debug.LogWarning("[AudioManager] 재생할 BGM AudioClip이 비어 있습니다.");
            return;
        }

        if (bgmSource == null)
            SetupAudioSources();

        if (bgmSource.clip == clip && bgmSource.isPlaying)
            return;

        bgmSource.clip = clip;
        bgmSource.Play();

        Debug.Log("[AudioManager] BGM 재생: " + clip.name);
    }

    public void StopBGM()
    {
        if (bgmSource == null)
            return;

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

        sfxSource.pitch = 1f;
        sfxSource.PlayOneShot(clip);
    }

    public void PlaySFX(AudioClip clip, float volume, float pitch)
    {
        if (clip == null)
        {
            Debug.LogWarning("[AudioManager] 재생할 SFX AudioClip이 비어 있습니다.");
            return;
        }

        if (sfxSource == null)
            SetupAudioSources();

        float previousPitch = sfxSource.pitch;

        sfxSource.pitch = Mathf.Clamp(pitch, 0.1f, 3f);
        sfxSource.PlayOneShot(clip, Mathf.Clamp01(volume));
        sfxSource.pitch = previousPitch;
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