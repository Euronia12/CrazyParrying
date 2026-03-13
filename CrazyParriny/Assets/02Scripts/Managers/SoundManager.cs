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

    //최초 필요 오디오 로드
    public async UniTask Prewarm()
    {
        if (audioMixer == null)
        {
            Debug.LogError("AudioMixer is null"); 
            return;
        }

        var ResourceMgr = ResourceManager.Instance;
        foreach(var key in ResourceMgr.addressableMap[eAddressableType.prevSound].Keys)
        {
            await ResourceMgr.LoadAsset<AudioClip>(key, eAddressableType.prevSound, (clip) =>
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

    //추가 오디오 로드
    public async Task LoadAudio(eAudioType type, string clipName)
    {
        // 이미 있는지 체크
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
        await ResourceManager.Instance.LoadAsset<AudioClip>(clipName, eAddressableType.sound,(obj) =>
        {
            clip = obj;
        });

        if (clip == null)
        {
            Debug.LogError($"Failed to load: {type} / {clipName}");
            return;
        }

        // 딕셔너리에 등록
        if (type == eAudioType.Bgm)
            bgmDict[clipName] = clip;
        else
            sfxDict[clipName] = clip;

        Debug.Log($"Audio loaded: {type} / {clipName}");
    }

    // 여러 개 한번에 로딩
    public async Task LoadAudios(eAudioType type, params string[] clipNames)
    {
        foreach (var name in clipNames)
        {
            await LoadAudio(type, name);
        }
    }

    // 오디오 언로드
    public void UnloadAudio(eAudioType type, string clipName)
    {
        if (type == eAudioType.Bgm)
            bgmDict.Remove(clipName);
        else if (type == eAudioType.Sfx)
            sfxDict.Remove(clipName);
        else
            rhythmDict.Remove(clipName);
    }

    // 타입별 전체 언로드
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
    // 배경음 문자열로 재생 (크로스페이드)
    public void PlayBGM(string clipName)
    {
        if (!bgmDict.TryGetValue(clipName, out AudioClip clip))
        {
            Debug.LogWarning($"BGM not found: {clipName}");
            //TODO : 동적 로드 시 여기에서 로드
            return;
        }

        PlayBGM(clip);
    }

    // 배경음 클립으로 재생 (크로스페이드)
    public void PlayBGM(AudioClip clip)
    {
        if (clip == null) return;

        // 같은 곡이면 무시
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

        // 새 곡 준비
        fadeIn.clip = newClip;
        fadeIn.volume = 0f;
        fadeIn.Play();

        // 크로스페이드
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

        // 마무리
        fadeOut.Stop();
        fadeOut.volume = 0f;
        fadeIn.volume = 1f;

        isUsingSourceA = !isUsingSourceA;
        crossfadeCoroutine = null;
    }

    // 배경음 즉시 정지
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

    // 배경음 페이드아웃 정지
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

    // 배경음 일시정지
    public void PauseBGM()
    {
        CurrentBgmSource.Pause();
    }

    // 배경음 재개
    public void ResumeBGM()
    {
        CurrentBgmSource.UnPause();
    }

    #endregion

    #region SFX
    // 효과음 재생
    public void PlaySFX(string clipName)
    {
        if (!sfxDict.TryGetValue(clipName, out AudioClip clip))
        {
            Debug.LogWarning($"SFX not found: {clipName}");
            return;
        }

        PlaySFX(clip);
    }

    // 효과음 재생
    public void PlaySFX(AudioClip clip)
    {
        if (clip == null) return;
        sfxSource.PlayOneShot(clip);
    }

    // 효과음 재생 (볼륨 지정)
    public void PlaySFX(AudioClip clip, float volumeScale)
    {
        if (clip == null) return;
        sfxSource.PlayOneShot(clip, volumeScale);
    }

    // 효과음 재생 (피치 랜덤) ex)발소리
    public void PlaySFXRandomPitch(AudioClip clip, float minPitch = 0.9f, float maxPitch = 1.1f)
    {
        if (clip == null) return;

        float originalPitch = sfxSource.pitch;
        sfxSource.pitch = Random.Range(minPitch, maxPitch);
        sfxSource.PlayOneShot(clip);
        sfxSource.pitch = originalPitch;
    }

    // 특정 위치에서 효과음 재생 (3D 사운드)
    // * 한번에 한곳만 가능 추후 여러 곳 사용 시 리팩토링 필
    public void PlaySFXAtPosition(AudioClip clip, Vector3 position, float volume = 1f)
    {
        // sfxSource 위치 이동
        posSfxSource.transform.position = position;

        // 3D 사운드로 설정
        posSfxSource.spatialBlend = 1f;

        posSfxSource.PlayOneShot(clip, volume);
    }
    #endregion

    #region 볼륨 조절 (Audio Mixer.ver)
    // 마스터 볼륨 설정 (0 ~ 100)
    public void SetMasterVolume(float volume)
    {
        SetMixerVolume(MASTER_VOLUME, volume);
    }

    // 배경음 볼륨 설정 (0 ~ 100)
    public void SetBGMVolume(float volume)
    {
        SetMixerVolume(BGM_VOLUME, volume);
    }

    // 효과음 볼륨 설정 (0 ~ 100)
    public void SetSFXVolume(float volume)
    {
        SetMixerVolume(SFX_VOLUME, volume);
    }

    private void SetMixerVolume(string parameterName, float volume)
    {
        // 0~100 → 0~1 → -80dB ~ 0dB 변환
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
            // -80dB ~ 0dB → 0~100 변환
            return Mathf.Pow(10f, dB / 20f) * 100f;
        }
        return 50f;
    }

    // 전체 음소거
    public void MuteAll(bool mute)
    {
        SetMasterVolume(mute ? 0f : 100f);
    }

    // 배경음 음소거
    public void MuteBGM(bool mute)
    {
        SetBGMVolume(mute ? 0f : 100f);
    }

    // 효과음 음소거
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
