using UnityEngine;
using UnityEngine.UI;

public class AudioSettingsPresenter : MonoBehaviour
{
    private const string MusicKey = "Settings.MusicVolume";
    private const string SfxKey = "Settings.SfxVolume";

    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider sfxSlider;

    private void Start()
    {
        float music = PlayerPrefs.GetFloat(MusicKey, 0.7f);
        float sfx = PlayerPrefs.GetFloat(SfxKey, 0.8f);

        musicSlider.SetValueWithoutNotify(music);
        sfxSlider.SetValueWithoutNotify(sfx);

        ApplyMusic(music);
        ApplySfx(sfx);

        musicSlider.onValueChanged.AddListener(ApplyMusic);
        sfxSlider.onValueChanged.AddListener(ApplySfx);
    }

    private void OnDisable()
    {
        PlayerPrefs.Save();
    }

    private void OnDestroy()
    {
        musicSlider.onValueChanged.RemoveListener(ApplyMusic);
        sfxSlider.onValueChanged.RemoveListener(ApplySfx);
        PlayerPrefs.Save();
    }

    private void ApplyMusic(float value)
    {
        GameBootstrap.Audio?.SetMusicVolume(value);
        PlayerPrefs.SetFloat(MusicKey, value);
    }

    private void ApplySfx(float value)
    {
        GameBootstrap.Audio?.SetSfxVolume(value);
        PlayerPrefs.SetFloat(SfxKey, value);
    }
}