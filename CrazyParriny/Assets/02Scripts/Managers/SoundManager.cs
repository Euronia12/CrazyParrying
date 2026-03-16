using Cysharp.Threading.Tasks;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Audio;

public class SoundManager : Singleton<SoundManager>
{
    [Header("Audio Mixer")]
    [SerializeField] private AudioMixer audioMixer;

    [Header("Audio Sources")]
    [SerializeField] private AudioSource bgmSourceA;
    [SerializeField] private AudioSource bgmSourceB;
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioSource posSfxSource;

    [Header("Settings")]
    [SerializeField] private float crossfadeDuration = 1.5f;

    private const string MASTER_VOLUME = "Master";
    private const string BGM_VOLUME = "BGMVolume";
    private const string SFX_VOLUME = "SFXVolume";

    [Header("Audio Clips")]
    private Dictionary<string, AudioClip> bgmDict = new();
    private Dictionary<string, AudioClip> sfxDict = new();
    private Dictionary<string, AudioClip> rhythmDict = new();

    private bool isUsingSourceA = true;
    private Coroutine crossfadeCoroutine;


    private AudioSource CurrentBgmSource => isUsingSourceA ? bgmSourceA : bgmSourceB;
    private AudioSource NextBgmSource => isUsingSourceA ? bgmSourceB : bgmSourceA;

    //���� �ʿ� ����� �ε�
    public async UniTask Prewarm()
    {
        var ResourceMgr = ResourceManager.Instance;

        await ResourceMgr.LoadAsset<AudioMixer>("AudioMixer", eAddressableType.DefaultLocalGroup, (obj) =>
        {
            audioMixer = obj;
        });

        if (audioMixer == null)
        {
            Debug.LogError("AudioMixer is null"); 
            return;
        }

        foreach(var key in ResourceMgr.addressableMap[eAddressableType.PrevSound].Keys)
        {
            await ResourceMgr.LoadAsset<AudioClip>(key, eAddressableType.PrevSound, (clip) =>
            {
                if(key.Contains("Bgm_"))
                {
                    bgmDict[clip.name] = clip;   
                }
                else if (key.Contains("Sfx_"))
                {
                    sfxDict[clip.name] = clip;
                }
                else
                {
                    Debug.LogWarning($"Sound : {key} is null");
                }

            });
        }
    }

    //�߰� ����� �ε�
    public async Task LoadAudio(eAudioType type, string clipName)
    {
        // �̹� �ִ��� üũ
        if (type == eAudioType.Bgm && bgmDict.ContainsKey(clipName))
        {
            Debug.LogWarning($"Already loaded: {type} / {clipName}");
            return;
        }
        if (type == eAudioType.Sfx && sfxDict.ContainsKey(clipName))
        {
            Debug.LogWarning($"Already loaded: {type} / {clipName}");
            return;
        }

        AudioClip clip = null;
        await ResourceManager.Instance.LoadAsset<AudioClip>(clipName, eAddressableType.Sound,(obj) =>
        {
            clip = obj;
        });

        if (clip == null)
        {
            Debug.LogError($"Failed to load: {type} / {clipName}");
            return;
        }

        // ��ųʸ��� ���
        if (type == eAudioType.Bgm)
            bgmDict[clipName] = clip;
        else
            sfxDict[clipName] = clip;

    }

    // ���� �� �ѹ��� �ε�
    public async Task LoadAudios(eAudioType type, params string[] clipNames)
    {
        foreach (var name in clipNames)
        {
            await LoadAudio(type, name);
        }
    }

    // ����� ��ε�
    public void UnloadAudio(eAudioType type, string clipName)
    {
        if (type == eAudioType.Bgm)
            bgmDict.Remove(clipName);
        else if (type == eAudioType.Sfx)
            sfxDict.Remove(clipName);
        else
            rhythmDict.Remove(clipName);
    }

    // Ÿ�Ժ� ��ü ��ε�
    public void UnloadAllAudio(eAudioType type)
    {
        if (type == eAudioType.Bgm)
            bgmDict.Clear();
        else if (type == eAudioType.Sfx)
            sfxDict.Clear();
        else
            rhythmDict.Clear();
    }

    #region BGM
    // ����� ���ڿ��� ��� (ũ�ν����̵�)
    public void PlayBGM(string clipName)
    {
        if (!bgmDict.TryGetValue(clipName, out AudioClip clip))
        {
            Debug.LogWarning($"BGM not found: {clipName}");
            //TODO : ���� �ε� �� ���⿡�� �ε�
            return;
        }

        PlayBGM(clip);
    }

    // ����� Ŭ������ ��� (ũ�ν����̵�)
    public void PlayBGM(AudioClip clip)
    {
        if (clip == null) return;

        // ���� ���̸� ����
        if (CurrentBgmSource.clip == clip && CurrentBgmSource.isPlaying)
            return;

        if (crossfadeCoroutine != null)
            StopCoroutine(crossfadeCoroutine);

        crossfadeCoroutine = StartCoroutine(CrossfadeBGM(clip));
    }

    private IEnumerator CrossfadeBGM(AudioClip newClip)
    {
        AudioSource fadeOut = CurrentBgmSource;
        AudioSource fadeIn = NextBgmSource;

        // �� �� �غ�
        fadeIn.clip = newClip;
        fadeIn.volume = 0f;
        fadeIn.Play();

        // ũ�ν����̵�
        float elapsed = 0f;
        float startVolume = fadeOut.volume;

        while (elapsed < crossfadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / crossfadeDuration;

            fadeOut.volume = Mathf.Lerp(startVolume, 0f, t);
            fadeIn.volume = Mathf.Lerp(0f, 1f, t);

            yield return null;
        }

        // ������
        fadeOut.Stop();
        fadeOut.volume = 0f;
        fadeIn.volume = 1f;

        isUsingSourceA = !isUsingSourceA;
        crossfadeCoroutine = null;
    }

    // ����� ��� ����
    public void StopBGM()
    {
        if (crossfadeCoroutine != null)
        {
            StopCoroutine(crossfadeCoroutine);
            crossfadeCoroutine = null;
        }

        bgmSourceA.Stop();
        bgmSourceB.Stop();
    }

    // ����� ���̵�ƿ� ����
    public void StopBGMWithFade(float duration = 1f)
    {
        if (crossfadeCoroutine != null)
            StopCoroutine(crossfadeCoroutine);

        crossfadeCoroutine = StartCoroutine(FadeOutBGM(duration));
    }

    private IEnumerator FadeOutBGM(float duration)
    {
        AudioSource current = CurrentBgmSource;
        float startVolume = current.volume;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            current.volume = Mathf.Lerp(startVolume, 0f, elapsed / duration);
            yield return null;
        }

        current.Stop();
        current.volume = 0f;
        crossfadeCoroutine = null;
    }

    // ����� �Ͻ�����
    public void PauseBGM()
    {
        CurrentBgmSource.Pause();
    }

    // ����� �簳
    public void ResumeBGM()
    {
        CurrentBgmSource.UnPause();
    }

    #endregion

    #region SFX
    // ȿ���� ���
    public void PlaySFX(string clipName)
    {
        if (!sfxDict.TryGetValue(clipName, out AudioClip clip))
        {
            Debug.LogWarning($"SFX not found: {clipName}");
            return;
        }

        PlaySFX(clip);
    }

    // ȿ���� ���
    public void PlaySFX(AudioClip clip)
    {
        if (clip == null) return;
        sfxSource.PlayOneShot(clip);
    }

    // ȿ���� ��� (���� ����)
    public void PlaySFX(AudioClip clip, float volumeScale)
    {
        if (clip == null) return;
        sfxSource.PlayOneShot(clip, volumeScale);
    }

    // ȿ���� ��� (��ġ ����) ex)�߼Ҹ�
    public void PlaySFXRandomPitch(AudioClip clip, float minPitch = 0.9f, float maxPitch = 1.1f)
    {
        if (clip == null) return;

        float originalPitch = sfxSource.pitch;
        sfxSource.pitch = Random.Range(minPitch, maxPitch);
        sfxSource.PlayOneShot(clip);
        sfxSource.pitch = originalPitch;
    }

    // Ư�� ��ġ���� ȿ���� ��� (3D ����)
    // * �ѹ��� �Ѱ��� ���� ���� ���� �� ��� �� �����丵 ��
    public void PlaySFXAtPosition(AudioClip clip, Vector3 position, float volume = 1f)
    {
        // sfxSource ��ġ �̵�
        posSfxSource.transform.position = position;

        // 3D ����� ����
        posSfxSource.spatialBlend = 1f;

        posSfxSource.PlayOneShot(clip, volume);
    }
    #endregion

    #region ���� ���� (Audio Mixer.ver)
    // ������ ���� ���� (0 ~ 100)
    public void SetMasterVolume(float volume)
    {
        SetMixerVolume(MASTER_VOLUME, volume);
    }

    // ����� ���� ���� (0 ~ 100)
    public void SetBGMVolume(float volume)
    {
        SetMixerVolume(BGM_VOLUME, volume);
    }

    // ȿ���� ���� ���� (0 ~ 100)
    public void SetSFXVolume(float volume)
    {
        SetMixerVolume(SFX_VOLUME, volume);
    }

    private void SetMixerVolume(string parameterName, float volume)
    {
        // 0~100 �� 0~1 �� -80dB ~ 0dB ��ȯ
        float normalized = Mathf.Clamp(volume, 0f, 100f) / 100f;
        float dB = normalized > 0.0001f ? Mathf.Log10(normalized) * 20f : -80f;
        audioMixer.SetFloat(parameterName, dB);
    }

    public float GetMasterVolume() => GetMixerVolume(MASTER_VOLUME);
    public float GetBGMVolume() => GetMixerVolume(BGM_VOLUME);
    public float GetSFXVolume() => GetMixerVolume(SFX_VOLUME);

    private float GetMixerVolume(string parameterName)
    {
        if (audioMixer.GetFloat(parameterName, out float dB))
        {
            // -80dB ~ 0dB �� 0~100 ��ȯ
            return Mathf.Pow(10f, dB / 20f) * 100f;
        }
        return 50f;
    }

    // ��ü ���Ұ�
    public void MuteAll(bool mute)
    {
        SetMasterVolume(mute ? 0f : 100f);
    }

    // ����� ���Ұ�
    public void MuteBGM(bool mute)
    {
        SetBGMVolume(mute ? 0f : 100f);
    }

    // ȿ���� ���Ұ�
    public void MuteSFX(bool mute)
    {
        SetSFXVolume(mute ? 0f : 100f);
    }

    public void SetCrossfadeDuration(float duration)
    {
        crossfadeDuration = Mathf.Max(0.1f, duration);
    }
    #endregion
}
