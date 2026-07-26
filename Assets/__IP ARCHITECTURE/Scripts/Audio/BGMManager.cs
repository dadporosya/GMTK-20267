using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

public class BGMManager : AudioManagerBase
{
    public static BGMManager Instance; // Instance

    [SerializeField] private AudioClip deafultBGMusic;
    private string defaultBGMusicPath = "Audio/Music/bgMusicTest";
    
    private AudioSource musicSourceA;
    private AudioSource musicSourceB;
    private AudioSource current;
    private AudioSource next;
    private void Awake()
    {
        // h.Out(Instance);
        h.CreateStaticInstance(this, ref Instance);
        // h.Out(Instance);
        
        InitSource(ref musicSourceA, AudioMixerManager.GetMusicGroup());
        InitSource(ref musicSourceB, AudioMixerManager.GetMusicGroup());
        current = musicSourceA;
        next = musicSourceB;
        InitSource(ref defaultAudioSource, AudioMixerManager.GetMusicGroup());
    }

    private void Start()
    {
        var clip = Resources.Load<AudioClip>(defaultBGMusicPath);
        // var clip = Resources.Load<AudioClip>("Audio/Music/bgMusicTest");
        if (clip) deafultBGMusic = clip;
        if (deafultBGMusic) PlayMusic(deafultBGMusic);
        else h.Out("Default music is not found'");
    }
    
    public void PlayMusic(AudioClip newClip, float fadeTime = 1.5f)
    {
        if (current.clip == newClip) return;
        StopAllCoroutines();
        StartCoroutine(CrossFade(newClip, fadeTime));
    }

    /// <summary>
    /// Fades the currently playing music down to silence over <paramref name="fadeTime"/> seconds,
    /// then stops the source and restores its volume so a later <see cref="PlayMusic"/> starts clean.
    /// </summary>
    public void FadeOutMusic(float fadeTime = 1.5f)
    {
        StopAllCoroutines();
        StartCoroutine(FadeOut(fadeTime));
    }

    private IEnumerator FadeOut(float fadeTime)
    {
        float startVolume = current.volume;
        float t = 0f;

        while (t < fadeTime)
        {
            t += Time.deltaTime;
            current.volume = Mathf.Lerp(startVolume, 0f, t / fadeTime);
            yield return null;
        }

        current.Stop();
        current.clip = null;
        current.volume = 1f;
    }
    
    private IEnumerator CrossFade(AudioClip newClip, float fadeTime, bool changeSource=true)
    {
        next.clip = newClip;
        next.volume = 0f;
        next.loop = true;
        next.Play();

        float t = 0f;

        while (t < fadeTime)
        {
            t += Time.deltaTime;
            float normalized = t / fadeTime;

            if (changeSource)
            {
                current.volume = Mathf.Lerp(1f, 0f, normalized);
            }
            
            next.volume = Mathf.Lerp(0f, 1f, normalized);

            yield return null;
        }
        if (changeSource)
        {
            current.Stop();
            current.volume = 1f;
            (current, next) = (next, current);
        }
    }
    
}