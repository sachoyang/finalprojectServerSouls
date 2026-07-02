using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

// ==========================================
// 💡 카테고리를 마음껏 추가하세요!
// ==========================================
public enum SoundCategory
{
    BGM,         // 배경음
    UI,          // UI 클릭, 알림
    Footstep,    // 걷기, 뛰기
    CombatHit,   // 타격음 (칼 챙강!)
    CombatHurt,  // 피격음 (윽!)
    SkillEffect, // 스킬 발동 소리
    BossGimmick, // 보스 포효, 기믹 소리
    PlayerSound  // 플레이어 이동, 구르기, 착지 등 동작음
}

// ==========================================
// 인스펙터에서 편집할 카테고리별 세부 설정값
// ==========================================
[System.Serializable]
public class SoundCategorySetting
{
    public SoundCategory category;

    [Header("그룹 볼륨 배율")]
    [Tooltip("기본 볼륨에 곱해집니다. (1 = 100%, 0.5 = 50%)")]
    [Range(0f, 2f)] public float volumeMultiplier = 1.0f;

    [Header("3D 사운드 거리 (3D 재생시에만 적용)")]
    public float minDistance = 5f;
    public float maxDistance = 30f;
}

#if UNITY_EDITOR
// 🔥 에디터에서만 사용하는 클릭 전용 모니터링 구조체
[System.Serializable]
public struct SFXMonitorInfo
{
    [Tooltip("클릭하면 하이라키(Hierarchy)의 스피커 위치로 이동합니다.")]
    public AudioSource speaker; 
    [Tooltip("클릭하면 프로젝트(Project) 창의 원본 오디오 파일로 이동합니다.")]
    public AudioClip clip;      
}
#endif

public class SoundManager : MonoSingleton<SoundManager>
{
    [Header("Audio Mixers")]
    public AudioMixer mainMixer;
    public AudioMixerGroup bgmMixerGroup;
    public AudioMixerGroup sfxMixerGroup;

    [Header("카테고리별 사운드 설정")]
    [Tooltip("여기에 카테고리를 추가하고 그룹별 볼륨/거리를 조절하세요.")]
    public List<SoundCategorySetting> categorySettings = new List<SoundCategorySetting>();

    private Dictionary<SoundCategory, SoundCategorySetting> _settingsDict = new Dictionary<SoundCategory, SoundCategorySetting>();

    [Header("오브젝트 풀 설정")]
    public int sfxPoolSize = 20;

    [Header("BGM 설정")]
    public float bgmCrossfadeDuration = 1.5f;

#if UNITY_EDITOR
    // 🔥 최적화: 빌드 시 완전히 삭제되는 모니터링 변수들
    [Header("👀 모니터링 (에디터 전용, 빌드 시 삭제됨)")]
    [SerializeField] private AudioClip currentBGM;
    [SerializeField] private List<SFXMonitorInfo> activeSFX = new List<SFXMonitorInfo>();

    private float _monitorTimer = 0f;
    private const float MONITOR_INTERVAL = 0.2f; // 초당 5번만 검사하여 성능 최적화
#endif

    private AudioSource _bgmSourceA;
    private AudioSource _bgmSourceB;
    private AudioSource _activeBgmSource; 
    private Coroutine _crossfadeCoroutine;

    private List<AudioSource> _sfxSources = new List<AudioSource>();

    protected override void Awake()
    {
        base.Awake();

        foreach (var setting in categorySettings)
        {
            if (!_settingsDict.ContainsKey(setting.category))
            {
                _settingsDict.Add(setting.category, setting);
            }
        }

        InitializeSoundManager();
    }

    private void InitializeSoundManager()
    {
        GameObject bgmObjectA = new GameObject("BGM_Player_A");
        bgmObjectA.transform.SetParent(transform);
        _bgmSourceA = bgmObjectA.AddComponent<AudioSource>();
        _bgmSourceA.loop = true;
        _bgmSourceA.spatialBlend = 0f;
        _bgmSourceA.outputAudioMixerGroup = bgmMixerGroup;

        GameObject bgmObjectB = new GameObject("BGM_Player_B");
        bgmObjectB.transform.SetParent(transform);
        _bgmSourceB = bgmObjectB.AddComponent<AudioSource>();
        _bgmSourceB.loop = true;
        _bgmSourceB.spatialBlend = 0f;
        _bgmSourceB.outputAudioMixerGroup = bgmMixerGroup;

        _activeBgmSource = _bgmSourceA;

        GameObject sfxParent = new GameObject("SFX_Pool");
        sfxParent.transform.SetParent(transform);

        for (int i = 0; i < sfxPoolSize; i++)
        {
            GameObject speakerObj = new GameObject($"Speaker_{i}");
            speakerObj.transform.SetParent(sfxParent.transform);

            AudioSource sfxSource = speakerObj.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
            sfxSource.outputAudioMixerGroup = sfxMixerGroup;
            sfxSource.rolloffMode = AudioRolloffMode.Linear;
            _sfxSources.Add(sfxSource);
        }
    }

#if UNITY_EDITOR
    // 🔥 최적화: 타이머를 적용하여 0.2초마다 한 번씩만 갱신 (에디터 렉 방지)
    private void Update()
    {
        _monitorTimer += Time.deltaTime;
        if (_monitorTimer >= MONITOR_INTERVAL)
        {
            _monitorTimer = 0f;

            // 1. BGM 모니터링 (오디오 클립 오브젝트 띄우기)
            if (_activeBgmSource != null && _activeBgmSource.isPlaying)
                currentBGM = _activeBgmSource.clip;
            else
                currentBGM = null;

            // 2. SFX 모니터링 (가비지 컬렉션 최소화를 위해 List 재사용)
            activeSFX.Clear();
            for (int i = 0; i < _sfxSources.Count; i++)
            {
                AudioSource source = _sfxSources[i];
                if (source.isPlaying && source.clip != null)
                {
                    activeSFX.Add(new SFXMonitorInfo 
                    { 
                        speaker = source, 
                        clip = source.clip 
                    });
                }
            }
        }
    }
#endif

    // ==========================================
    // 🔍 카테고리 설정 가져오기 헬퍼 함수
    // ==========================================
    private SoundCategorySetting GetCategorySetting(SoundCategory category)
    {
        if (_settingsDict.TryGetValue(category, out SoundCategorySetting setting))
        {
            return setting;
        }
        return new SoundCategorySetting { category = category, volumeMultiplier = 1f, minDistance = 5f, maxDistance = 30f };
    }

    // ==========================================
    // 🎵 BGM 재생
    // ==========================================
    public void PlayBGM(AudioClip clip, float baseVolume = 1.0f, float delay = 0f)
    {
        if (clip == null) return;
        if (_activeBgmSource.clip == clip && _activeBgmSource.isPlaying) return;
        if (_crossfadeCoroutine != null) StopCoroutine(_crossfadeCoroutine);

        _crossfadeCoroutine = StartCoroutine(CrossfadeRoutine(clip, baseVolume, delay));
    }

    private IEnumerator CrossfadeRoutine(AudioClip newClip, float baseVolume, float delay)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);

        SoundCategorySetting setting = GetCategorySetting(SoundCategory.BGM);
        float targetVolume = baseVolume * setting.volumeMultiplier;

        AudioSource fadeOutSource = _activeBgmSource;
        AudioSource fadeInSource = (_activeBgmSource == _bgmSourceA) ? _bgmSourceB : _bgmSourceA;

        fadeInSource.clip = newClip;
        fadeInSource.volume = 0f; 
        fadeInSource.Play();

        _activeBgmSource = fadeInSource; 

        float timer = 0f;
        float startVolumeFadeOut = fadeOutSource.volume;

        while (timer < bgmCrossfadeDuration)
        {
            timer += Time.deltaTime;
            float t = timer / bgmCrossfadeDuration;

            fadeOutSource.volume = Mathf.Lerp(startVolumeFadeOut, 0f, t);
            fadeInSource.volume = Mathf.Lerp(0f, targetVolume, t);

            yield return null;
        }

        fadeOutSource.Stop();
        fadeOutSource.volume = 0f;
        fadeInSource.volume = targetVolume;
    }

    public void StopBGM()
    {
        if (_crossfadeCoroutine != null) StopCoroutine(_crossfadeCoroutine);
        _bgmSourceA.Stop();
        _bgmSourceB.Stop();
    }
    
    // ==========================================
    // 🔊 2D 사운드 재생
    // ==========================================
    public void PlaySFX_2D(AudioClip clip, SoundCategory category, float baseVolume = 1.0f, float delay = 0f)
    {
        if (clip == null) return;

        if (delay > 0f)
        {
            StartCoroutine(CoPlaySFX_2D(clip, category, baseVolume, delay));
            return;
        }

        AudioSource source = GetAvailableSFXSource();
        if (source != null)
        {
            SoundCategorySetting setting = GetCategorySetting(category);

            source.spatialBlend = 0f;
            source.clip = clip;
            source.volume = baseVolume * setting.volumeMultiplier;
            source.Play();
        }
    }

    private IEnumerator CoPlaySFX_2D(AudioClip clip, SoundCategory category, float baseVolume, float delay)
    {
        yield return new WaitForSeconds(delay);
        PlaySFX_2D(clip, category, baseVolume, 0f);
    }

    // ==========================================
    // 🔊 3D 사운드 재생
    // ==========================================
    public void PlaySFX_3D(AudioClip clip, Vector3 playPosition, SoundCategory category, float baseVolume = 1.0f, float delay = 0f)
    {
        if (clip == null) return;

        if (delay > 0f)
        {
            StartCoroutine(CoPlaySFX_3D(clip, playPosition, category, baseVolume, delay));
            return;
        }

        AudioSource source = GetAvailableSFXSource();
        if (source != null)
        {
            SoundCategorySetting setting = GetCategorySetting(category);

            source.transform.position = playPosition;
            source.spatialBlend = 1f;
            source.minDistance = setting.minDistance;
            source.maxDistance = setting.maxDistance;

            source.clip = clip;
            source.volume = baseVolume * setting.volumeMultiplier;
            source.Play();
        }
    }

    private IEnumerator CoPlaySFX_3D(AudioClip clip, Vector3 playPosition, SoundCategory category, float baseVolume, float delay)
    {
        yield return new WaitForSeconds(delay);
        PlaySFX_3D(clip, playPosition, category, baseVolume, 0f);
    }

    private AudioSource GetAvailableSFXSource()
    {
        foreach (AudioSource source in _sfxSources)
        {
            if (!source.isPlaying) return source;
        }
        return null;
    }

    // ==========================================
    // UI 연동용 볼륨 조절 API
    // ==========================================
    public void SetMasterVolume(float sliderValue)
    {
        float value = Mathf.Max(0.0001f, sliderValue);
        mainMixer.SetFloat("MasterVolume", Mathf.Log10(value) * 20);
    }

    public void SetBGMVolume(float sliderValue)
    {
        float value = Mathf.Max(0.0001f, sliderValue);
        mainMixer.SetFloat("BgmVolume", Mathf.Log10(value) * 20);
    }

    public void SetSFXVolume(float sliderValue)
    {
        float value = Mathf.Max(0.0001f, sliderValue);
        mainMixer.SetFloat("SFXVolume", Mathf.Log10(value) * 20);
    }
}
