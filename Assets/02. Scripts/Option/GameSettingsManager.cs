using UnityEngine;
using UnityEngine.Audio;

public class GameSettingsManager : MonoBehaviour
{
    public static GameSettingsManager Instance { get; private set; }

    [Header("Audio Mixer")]
    [SerializeField] private AudioMixer audioMixer;

    private const string GraphicsQualityKey = "Settings_GraphicsQuality";
    private const string MasterVolumeKey = "Settings_MasterVolume";
    private const string BgmVolumeKey = "Settings_BgmVolume";
    private const string SfxVolumeKey = "Settings_SfxVolume";

    private const string MasterVolumeParam = "MasterVolume";
    private const string BgmVolumeParam = "BgmVolume";
    private const string SfxVolumeParam = "SfxVolume";

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(transform.root.gameObject);

        LoadAndApplySettings();
    }

    public void LoadAndApplySettings()
    {
        SetGraphicsQuality(GetInt(SettingKey.GraphicsQuality, QualitySettings.GetQualityLevel()));
        SetMasterVolume(GetFloat(SettingKey.MasterVolume, 1f));
        SetBgmVolume(GetFloat(SettingKey.BgmVolume, 1f));
        SetSfxVolume(GetFloat(SettingKey.SfxVolume, 1f));
    }

    public int GetInt(SettingKey key, int defaultValue)
    {
        return PlayerPrefs.GetInt(GetPrefsKey(key), defaultValue);
    }

    public float GetFloat(SettingKey key, float defaultValue)
    {
        return PlayerPrefs.GetFloat(GetPrefsKey(key), defaultValue);
    }

    public void SetInt(SettingKey key, int value)
    {
        PlayerPrefs.SetInt(GetPrefsKey(key), value);
        PlayerPrefs.Save();

        ApplyIntSetting(key, value);
    }

    public void SetFloat(SettingKey key, float value)
    {
        value = Mathf.Clamp01(value);

        PlayerPrefs.SetFloat(GetPrefsKey(key), value);
        PlayerPrefs.Save();

        ApplyFloatSetting(key, value);
    }

    public void SetGraphicsQuality(int qualityIndex)
    {
        SetInt(SettingKey.GraphicsQuality, qualityIndex);
    }

    public void SetMasterVolume(float volume)
    {
        SetFloat(SettingKey.MasterVolume, volume);
    }

    public void SetBgmVolume(float volume)
    {
        SetFloat(SettingKey.BgmVolume, volume);
    }

    public void SetSfxVolume(float volume)
    {
        SetFloat(SettingKey.SfxVolume, volume);
    }

    private void ApplyIntSetting(SettingKey key, int value)
    {
        if (key == SettingKey.GraphicsQuality)
        {
            int qualityIndex = Mathf.Clamp(value, 0, QualitySettings.names.Length - 1);
            QualitySettings.SetQualityLevel(qualityIndex);
        }
    }

    private void ApplyFloatSetting(SettingKey key, float value)
    {
        if (key == SettingKey.MasterVolume)
        {
            SetMixerVolume(MasterVolumeParam, value);
        }
        else if (key == SettingKey.BgmVolume)
        {
            SetMixerVolume(BgmVolumeParam, value);
        }
        else if (key == SettingKey.SfxVolume)
        {
            SetMixerVolume(SfxVolumeParam, value);
        }
    }

    private void SetMixerVolume(string parameterName, float volume)
    {
        if (audioMixer == null)
        {
            Debug.LogWarning("[GameSettingsManager] AudioMixer가 연결되지 않았습니다.");
            return;
        }

        float db = volume <= 0.0001f ? -80f : Mathf.Log10(volume) * 20f;
        bool success = audioMixer.SetFloat(parameterName, db);

        if (!success)
            Debug.LogWarning("[GameSettingsManager] AudioMixer 파라미터를 찾을 수 없습니다: " + parameterName);
    }

    private string GetPrefsKey(SettingKey key)
    {
        switch (key)
        {
            case SettingKey.GraphicsQuality:
                return GraphicsQualityKey;
            case SettingKey.MasterVolume:
                return MasterVolumeKey;
            case SettingKey.BgmVolume:
                return BgmVolumeKey;
            case SettingKey.SfxVolume:
                return SfxVolumeKey;
            default:
                return "Settings_None";
        }
    }
}