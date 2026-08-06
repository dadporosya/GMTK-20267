using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

public class BGMManager : AudioManagerBase
{
    public static BGMManager Instance; // Instance

    public AudioClip deafultBGMusic;
    public List<AudioClip> bgTracks = new List<AudioClip>();
    private string defaultBGMusicPath = "Audio/Music/bgMusicTest";
    
    private AudioSource musicSourceA;
    private AudioSource musicSourceB;
    [HideInInspector] public AudioSource current;
    [HideInInspector] public AudioSource next;
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

        // Kick off async loading of every playlist track up front so that starting a track later
        // never has to decompress/stream-open on the main thread (that was the mid-game freeze).
        PreloadAllTracks();

        if (deafultBGMusic) PlayMusic(deafultBGMusic);
        else h.Out("Default music is not found'");
    }

    /// <summary>
    /// Requests background loading of the default track and every clip in <see cref="bgTracks"/>.
    /// Clips must be imported with "Load In Background" enabled, otherwise LoadAudioData blocks.
    /// </summary>
    public void PreloadAllTracks()
    {
        RequestLoad(deafultBGMusic);
        if (bgTracks == null) return;
        foreach (var track in bgTracks) RequestLoad(track);
    }

    private static void RequestLoad(AudioClip clip)
    {
        if (!clip) return;
        if (clip.loadType == AudioClipLoadType.Streaming) return; // streamed clips need no preload
        if (clip.loadState == AudioDataLoadState.Loaded || clip.loadState == AudioDataLoadState.Loading) return;
        clip.LoadAudioData();
    }

    /// <summary>
    /// Yields until <paramref name="clip"/>'s audio data is resident, so the following Play() call
    /// costs nothing on the main thread.
    /// </summary>
    private static IEnumerator EnsureLoaded(AudioClip clip)
    {
        if (!clip || clip.loadType == AudioClipLoadType.Streaming) yield break;

        RequestLoad(clip);
        while (clip.loadState == AudioDataLoadState.Loading)
            yield return null;
    }

    /// <summary>
    /// Crossfades <paramref name="newClip"/> in over <paramref name="fadeTime"/> seconds.
    /// <paramref name="startTime"/> is the playback position (in seconds) the clip starts from, so a
    /// caller that remembered where a track was interrupted can resume it instead of restarting it.
    /// </summary>
    public void PlayMusic(AudioClip newClip, float fadeTime = 1.5f, float startTime = 0f)
    {
        if (current.clip == newClip) return;
        StopAllCoroutines();
        StartCoroutine(CrossFade(newClip, fadeTime, startTime: startTime));
    }

    /// <summary>
    /// Starts playing a random track from <see cref="bgTracks"/>. When that clip ends,
    /// another random track is picked and crossfaded in, looping the playlist indefinitely.
    /// </summary>
    public void PlayRandomBgTrack(float fadeTime = 1.5f)
    {
        if (bgTracks == null || bgTracks.Count == 0)
        {
            h.Out("bgTracks is empty");
            return;
        }

        var clip = h.RandChoice(bgTracks);
        // Avoid immediately repeating the same track when there's a choice.
        while (bgTracks.Count > 1 && clip == current.clip)
            clip = h.RandChoice(bgTracks);

        StopAllCoroutines();
        StartCoroutine(PlaylistRoutine(clip, fadeTime));
    }

    private IEnumerator PlaylistRoutine(AudioClip clip, float fadeTime)
    {
        // Crossfade in the new clip without looping so we can detect when it ends.
        yield return CrossFade(clip, fadeTime, loop: false);

        // Wait for the clip to finish (stalling while the game is paused).
        while (current.clip == clip && (current.isPlaying || GameFlowManager.Instance.IsPaused()))
            yield return null;

        // Clip ended, chain into the next random track.
        PlayRandomBgTrack(fadeTime);
    }

    /// <summary>
    /// Plays <paramref name="clip"/> once (no loop) and, when it finishes, crossfades into a random
    /// track from <see cref="bgTracks"/> and keeps looping that playlist. Used by cutscenes that swap
    /// in a one-off soundtrack and want the normal background music to resume afterwards.
    /// </summary>
    public void PlayMusicThenRandomBgTracks(AudioClip clip, float fadeTime = 1.5f)
    {
        if (!clip)
        {
            // Nothing to play up front — just go straight to the random playlist.
            PlayRandomBgTrack(fadeTime);
            return;
        }

        StopAllCoroutines();
        // PlaylistRoutine plays the clip once, waits for it to end, then chains PlayRandomBgTrack.
        StartCoroutine(PlaylistRoutine(clip, fadeTime));
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
    
    private IEnumerator CrossFade(AudioClip newClip, float fadeTime, bool changeSource=true, bool loop=true,
        float startTime=0f)
    {
        // Make sure the clip is fully resident before Play(), otherwise Unity decompresses it
        // synchronously on the main thread and the game hitches.
        yield return EnsureLoaded(newClip);

        next.clip = newClip;
        next.volume = 0f;
        next.loop = loop;

        // Resume from a remembered position when asked. Clamped just short of the clip's end so a
        // stale/overlong offset can't start the source at (or past) EOF and stop immediately.
        if (startTime > 0f && newClip)
            next.time = Mathf.Clamp(startTime, 0f, Mathf.Max(0f, newClip.length - 0.05f));
        else
            next.time = 0f;

        next.Play();

        // Give the streamer a frame to fill its buffer before the fade starts.
        yield return null;

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