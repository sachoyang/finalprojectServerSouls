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
    BossGimmick  // 보스 포효, 기믹 소리
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

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    [Header("Audio Mixers")]
    public AudioMixer mainMixer;
    public AudioMixerGroup bgmMixerGroup;
    public AudioMixerGroup sfxMixerGroup;

    [Header("카테고리별 사운드 설정")]
    [Tooltip("여기에 카테고리를 추가하고 그룹별 볼륨/거리를 조절하세요.")]
    public List<SoundCategorySetting> categorySettings = new List<SoundCategorySetting>();

    // 검색(탐색) 속도를 O(1)로 만들기 위한 딕셔너리
    private Dictionary<SoundCategory, SoundCategorySetting> _settingsDict = new Dictionary<SoundCategory, SoundCategorySetting>();

    [Header("오브젝트 풀 설정")]
    public int sfxPoolSize = 20;

    private AudioSource _bgmSource;
    private List<AudioSource> _sfxSources = new List<AudioSource>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // 인스펙터에 등록된 리스트를 빠르게 찾을 수 있도록 딕셔너리로 변환
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
        GameObject bgmObject = new GameObject("BGM_Player");
        bgmObject.transform.SetParent(transform);
        _bgmSource = bgmObject.AddComponent<AudioSource>();
        _bgmSource.loop = true;
        _bgmSource.spatialBlend = 0f;
        _bgmSource.outputAudioMixerGroup = bgmMixerGroup;

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

    // ==========================================
    // 🔍 카테고리 설정 가져오기 헬퍼 함수
    // ==========================================
    private SoundCategorySetting GetCategorySetting(SoundCategory category)
    {
        if (_settingsDict.TryGetValue(category, out SoundCategorySetting setting))
        {
            return setting;
        }
        // 인스펙터에서 설정을 안 해뒀을 경우를 대비한 기본값
        return new SoundCategorySetting { category = category, volumeMultiplier = 1f, minDistance = 5f, maxDistance = 30f };
    }

    // ==========================================
    // 🎵 BGM 재생
    // ==========================================
    public void PlayBGM(AudioClip clip, float baseVolume = 1.0f)
    {
        if (clip == null) return;
        if (_bgmSource.clip == clip && _bgmSource.isPlaying) return;

        SoundCategorySetting setting = GetCategorySetting(SoundCategory.BGM);

        _bgmSource.clip = clip;
        _bgmSource.volume = baseVolume * setting.volumeMultiplier; // 그룹 볼륨 적용!
        _bgmSource.Play();
    }

    // ==========================================
    // 🔊 2D 사운드 재생 (카테고리 필수 입력)
    // ==========================================
    public void PlaySFX_2D(AudioClip clip, SoundCategory category, float baseVolume = 1.0f)
    {
        if (clip == null) return;

        AudioSource source = GetAvailableSFXSource();
        if (source != null)
        {
            SoundCategorySetting setting = GetCategorySetting(category);

            source.spatialBlend = 0f;
            source.clip = clip;
            source.volume = baseVolume * setting.volumeMultiplier; // 그룹 볼륨 적용!
            source.Play();
        }
    }

    // ==========================================
    // 🔊 3D 사운드 재생 (카테고리 필수 입력)
    // ==========================================
    public void PlaySFX_3D(AudioClip clip, Vector3 playPosition, SoundCategory category, float baseVolume = 1.0f)
    {
        if (clip == null) return;

        AudioSource source = GetAvailableSFXSource();
        if (source != null)
        {
            SoundCategorySetting setting = GetCategorySetting(category);

            source.transform.position = playPosition;
            source.spatialBlend = 1f;
            
            // 🔥 카테고리에 설정된 3D 거리 적용!
            source.minDistance = setting.minDistance;
            source.maxDistance = setting.maxDistance;

            source.clip = clip;
            source.volume = baseVolume * setting.volumeMultiplier; // 그룹 볼륨 적용!
            source.Play();
        }
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
    
    /// <summary>
    /// 마스터(전체) 볼륨 조절
    /// </summary>
    /// <param name="sliderValue">UI 슬라이더 값 (0.0001f ~ 1.0f)</param>
    public void SetMasterVolume(float sliderValue)
    {
        // 슬라이더 값이 0이면 Log 연산 시 에러(-Infinity)가 나므로 최소값을 0.0001로 보정
        float value = Mathf.Max(0.0001f, sliderValue);
        // 선형 값(0~1)을 오디오 믹서용 데시벨(dB)로 변환
        mainMixer.SetFloat("MasterVolume", Mathf.Log10(value) * 20);
    }

    /// <summary>
    /// BGM(배경음) 볼륨 조절
    /// </summary>
    public void SetBGMVolume(float sliderValue)
    {
        float value = Mathf.Max(0.0001f, sliderValue);
        mainMixer.SetFloat("BGMVolume", Mathf.Log10(value) * 20);
    }

    /// <summary>
    /// SFX(효과음) 볼륨 조절
    /// </summary>
    public void SetSFXVolume(float sliderValue)
    {
        float value = Mathf.Max(0.0001f, sliderValue);
        mainMixer.SetFloat("SFXVolume", Mathf.Log10(value) * 20);
    }
}