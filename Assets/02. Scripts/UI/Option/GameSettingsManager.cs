using UnityEngine;
using UnityEngine.Audio;

public class GameSettingsManager : MonoBehaviour
{
    public static GameSettingsManager Instance { get; private set; }

    private const string GraphicsQualityKey = "Settings_GraphicsQuality";
    private const string MasterVolumeKey = "Settings_MasterVolume";
    private const string BgmVolumeKey = "Settings_BgmVolume";
    private const string SfxVolumeKey = "Settings_SfxVolume";

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(transform.root.gameObject);
    }

    private void Start()
    {
        LoadAndApplySettings();
    }

    public void LoadAndApplySettings()
    {
        SetGraphicsQuality(GetInt(SettingKey.GraphicsQuality, QualitySettings.GetQualityLevel()));

        SetFloat(SettingKey.MasterVolume, GetFloat(SettingKey.MasterVolume, 1f));
        SetFloat(SettingKey.BgmVolume, GetFloat(SettingKey.BgmVolume, 1f));
        SetFloat(SettingKey.SfxVolume, GetFloat(SettingKey.SfxVolume, 1f));
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

    private void ApplyIntSetting(SettingKey key, int value)
    {
        if (key == SettingKey.GraphicsQuality)
        {
            int qualityIndex = Mathf.Clamp(value, 0, QualitySettings.names.Length - 1);
            QualitySettings.SetQualityLevel(qualityIndex, true);

            Debug.Log(
                $"[Settings] Quality Changed: " +
                $"{QualitySettings.GetQualityLevel()} / " +
                $"{QualitySettings.names[QualitySettings.GetQualityLevel()]}, " +
                $"Pipeline: {QualitySettings.renderPipeline?.name}");
        }
    }

    private void ApplyFloatSetting(SettingKey key, float value)
    {
        if (SoundManager.Instance == null) return;

        if (key == SettingKey.MasterVolume)
            SoundManager.Instance.SetMasterVolume(value);
        else if (key == SettingKey.BgmVolume)
            SoundManager.Instance.SetBGMVolume(value);
        else if (key == SettingKey.SfxVolume)
            SoundManager.Instance.SetSFXVolume(value);
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
