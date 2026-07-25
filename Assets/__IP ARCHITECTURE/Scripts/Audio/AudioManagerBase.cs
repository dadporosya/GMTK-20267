using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

public class AudioManagerBase : MonoBehaviour
{
    [HideInInspector] public AudioSource defaultAudioSource;
    
    public void InitSource(ref AudioSource source, AudioMixerGroup mixerGroup = null)
    {
        if (!source)
        {
            GameObject go = new GameObject("AudioSource");
            go.transform.SetParent(transform); // optional
            source = go.AddComponent<AudioSource>();
        }

        source.outputAudioMixerGroup = mixerGroup
            ? mixerGroup
            : AudioMixerManager.GetMasterGroup();
    }

    /// <summary>
    /// Resolves the pitch to apply to an AudioSource.
    /// If <paramref name="randomPitchRange"/> is set, returns a random value inside it (x = min, y = max),
    /// otherwise returns <paramref name="pitchIn"/>, otherwise the source's default pitch.
    /// </summary>
    private float ResolvePitch(float? pitchIn, Vector2? randomPitchRange)
    {
        if (randomPitchRange.HasValue)
            return UnityEngine.Random.Range(randomPitchRange.Value.x, randomPitchRange.Value.y);

        return pitchIn ?? defaultAudioSource.pitch;
    }

    /// <summary>
    /// Plays audio clip.
    /// </summary>
    /// <param name="clip">Clip</param>
    /// <param name="volumeIn">Clip's volume</param>
    /// <param name="parent">Parent, where clip's AudioSource would be instantiated. If == null: no instantiation
    /// (plays in manager)</param>
    /// <param name="pitchIn">Explicit pitch. Ignored if <paramref name="randomPitchRange"/> is set.</param>
    /// <param name="randomPitchRange">Random pitch range (x = min, y = max). Picks a random pitch per play.</param>
    public void PlayClipIndependently(AudioClip clip, float? volumeIn=null, Transform parent=null,
        float? pitchIn = null, Vector2? randomPitchRange = null)
    {
        try
        {
            float volume = volumeIn ?? defaultAudioSource.volume;
            float pitch = ResolvePitch(pitchIn, randomPitchRange);

            // if (parent == null)
            // {
            //     defaultAudioSource.volume = volume;
            //     defaultAudioSource.PlayOneShot(clip);
            //     return;
            // }

            if (!parent) parent = transform;

            AudioSource audioSource = Instantiate(defaultAudioSource, parent.position, Quaternion.identity, parent);

            audioSource.clip = clip;
            audioSource.volume = volume;
            audioSource.pitch = pitch;

            audioSource.Play();

            float clipLength = audioSource.clip.length / Mathf.Max(0.01f, Mathf.Abs(pitch));
            Destroy(audioSource.gameObject, clipLength);

            // h.Out("Played");
        }
        catch (Exception e)
        {
            h.Out($"Error playing audio clip: {e.Message}");
        }

    }

    public void PlayClipIndependently(string path, float? volumeIn = null, Transform parent = null,
        float? pitchIn = null, Vector2? randomPitchRange = null)
    {
        PlayClipIndependently(Resources.Load<AudioClip>(path), volumeIn, parent, pitchIn, randomPitchRange);
    }

    public void PlayRandomClipIndependently(List<AudioClip> clip, float? volumeIn = null, Transform parent = null,
        float? pitchIn = null, Vector2? randomPitchRange = null)
    {
        PlayClipIndependently(h.RandChoice(clip), volumeIn, parent, pitchIn, randomPitchRange);
    }

    public void PlayRandomClipIndependently(List<string> paths, float? volumeIn = null, Transform parent = null,
        float? pitchIn = null, Vector2? randomPitchRange = null)
    {
        List<AudioClip> clips = new List<AudioClip>();
        foreach (string path in paths)
        {
            AudioClip clip = Resources.Load<AudioClip>(path);
            if (clip != null) clips.Add(clip);
        }
        PlayRandomClipIndependently(clips, volumeIn, parent, pitchIn, randomPitchRange);
    }
    
    public void PlayAudioResource(AudioResource resource, float? volumeIn = null, Transform parent = null)
    {
        float volume = volumeIn ?? defaultAudioSource.volume;

        if (parent == null)
        {
            defaultAudioSource.volume = volume;
            defaultAudioSource.resource = resource;
            defaultAudioSource.Play();
            return;
        }

        AudioSource audioSource = Instantiate(defaultAudioSource, parent.position, Quaternion.identity);

        audioSource.resource = resource;
        audioSource.volume = volume;

        audioSource.Play();

        // ⚠️ We cannot reliably get length from AudioResource
        Destroy(audioSource.gameObject, 10f); // fallback lifetime
        
        h.Out("Played");
    }

    public void PlayAudioResource(string path, float? volumeIn = null, Transform parent = null)
    {
        AudioResource resource = Resources.Load<AudioResource>(path);
        if (resource != null)
        {
            PlayAudioResource(resource, volumeIn, parent);
        }
    }

    public void PlayClip(AudioClip clip, float? volumeIn = null,
        float? pitchIn = null, Vector2? randomPitchRange = null)
    {
        float volumeToUse = volumeIn ?? defaultAudioSource.volume;
        defaultAudioSource.volume = volumeToUse;
        defaultAudioSource.pitch = ResolvePitch(pitchIn, randomPitchRange);
        defaultAudioSource.clip = clip;
        defaultAudioSource.Play();
    }

    public void PlayClip(string path, float? volumeIn = null,
        float? pitchIn = null, Vector2? randomPitchRange = null)
    {
        AudioClip clip = Resources.Load<AudioClip>(path);
        if (clip != null)
        {
            PlayClipIndependently(clip, volumeIn, null, pitchIn, randomPitchRange);
        }
    }

    public void PlayRandomClip(List<AudioClip> clip, float? volumeIn = null,
        float? pitchIn = null, Vector2? randomPitchRange = null)
    {
        PlayClipIndependently(h.RandChoice(clip), volumeIn, null, pitchIn, randomPitchRange);
    }

    public void PlayRandomClip(List<string> paths, float? volumeIn = null,
        float? pitchIn = null, Vector2? randomPitchRange = null)
    {
        List<AudioClip> clips = new List<AudioClip>();
        foreach (string path in paths)
        {
            AudioClip clip = Resources.Load<AudioClip>(path);
            if (clip != null) clips.Add(clip);
        }
        PlayClipIndependently(h.RandChoice(clips), volumeIn, null, pitchIn, randomPitchRange);
    }
}
