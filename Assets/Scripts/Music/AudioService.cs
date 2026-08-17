using UnityEngine;
using UnityEngine.Audio;

public class AudioService : MonoBehaviour
{
    private const string MusicVolumeParameter = "MusicVolume";
    private const string SfxVolumeParameter = "SfxVolume";

    [Header("Mixer")]
    [SerializeField] private AudioMixer audioMixer;
    [SerializeField] private AudioMixerGroup musicGroup;
    [SerializeField] private AudioMixerGroup sfxGroup;

    [Header("Sources")]
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioSource sfx2DSource;
    [SerializeField] private Transform worldSfxRoot;

    [Min(1)]
    [SerializeField] private int worldSourceCount = 8;

    [Header("3D Sound")]
    [SerializeField] private float minDistance = 2f;
    [SerializeField] private float maxDistance = 22f;

    private AudioSource[] worldSources;
    private int nextWorldSourceIndex;

    private void Awake()
    {
        CreateWorldSourcePool();
    }

    public void PlayMusic(AudioClip clip, bool loop = true)
    {
        if (clip == null || musicSource == null)
        {
            return;
        }

        if (musicSource.clip == clip && musicSource.isPlaying)
        {
            return;
        }

        musicSource.outputAudioMixerGroup = musicGroup;
        musicSource.clip = clip;
        musicSource.loop = loop;
        musicSource.Play();
    }

    public void PlaySfx2D(AudioClip clip, float volume = 1f)
    {
        if (clip == null || sfx2DSource == null)
        {
            return;
        }

        sfx2DSource.outputAudioMixerGroup = sfxGroup;
        sfx2DSource.spatialBlend = 0f;
        sfx2DSource.PlayOneShot(clip, Mathf.Clamp01(volume));
    }

    public void PlaySfxAt(
        AudioClip clip,
        Vector3 worldPosition,
        float volume = 1f)
    {
        if (clip == null || worldSources == null || worldSources.Length == 0)
        {
            return;
        }

        AudioSource source = FindAvailableWorldSource();
        source.transform.position = worldPosition;
        source.clip = clip;
        source.volume = Mathf.Clamp01(volume);
        source.Play();
    }

    public void SetMusicVolume(float normalizedValue)
    {
        SetMixerVolume(MusicVolumeParameter, normalizedValue);
    }

    public void SetSfxVolume(float normalizedValue)
    {
        SetMixerVolume(SfxVolumeParameter, normalizedValue);
    }

    private void CreateWorldSourcePool()
    {
        worldSources = new AudioSource[worldSourceCount];

        for (int i = 0; i < worldSources.Length; i++)
        {
            GameObject sourceObject = new GameObject($"WorldSfx_{i:00}");
            sourceObject.transform.SetParent(worldSfxRoot, false);

            AudioSource source = sourceObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = minDistance;
            source.maxDistance = maxDistance;
            source.outputAudioMixerGroup = sfxGroup;

            worldSources[i] = source;
        }
    }

    private AudioSource FindAvailableWorldSource()
    {
        foreach (AudioSource source in worldSources)
        {
            if (!source.isPlaying)
            {
                return source;
            }
        }

        // 同时播放数量超过池大小时，复用最早轮到的音源。
        AudioSource fallback = worldSources[nextWorldSourceIndex];
        nextWorldSourceIndex =
            (nextWorldSourceIndex + 1) % worldSources.Length;
        return fallback;
    }

    private void SetMixerVolume(string parameterName, float normalizedValue)
    {
        float value = Mathf.Clamp01(normalizedValue);
        float decibels = value <= 0.0001f
            ? -80f
            : Mathf.Log10(value) * 20f;

        audioMixer.SetFloat(parameterName, decibels);
    }
}