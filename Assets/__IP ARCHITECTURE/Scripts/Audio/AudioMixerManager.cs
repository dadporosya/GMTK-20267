using UnityEngine;
using UnityEngine.Audio;
using M = System.MathF;

public static class AudioMixerManager
{
    public static AudioMixer audioMixer = Resources.Load<AudioMixer>("ARCHITECTURE/Audio/Mixers/MainMixer");

    public static string masterGroupName = "Master";
    public static string SFXGroupName = "SFX";
    public static string musicGroupName = "Music";
    
    public static void SetMixerGroupVolume(float level, string groupName)
    {
        audioMixer.SetFloat(groupName, M.Log10(level) * 20f);
    }
    
    public static void SetMasterVolume(float level)
    {
        SetMixerGroupVolume(level, masterGroupName);
    }
    
    public static void SetMusicVolume(float level)
    {
        SetMixerGroupVolume(level, SFXGroupName);
    }
    
    public static void SetSFXVolume(float level)
    {
        SetMixerGroupVolume(level, musicGroupName);
    }

    public static AudioMixerGroup GetMixerGroup(string groupName)
    {
        return audioMixer.FindMatchingGroups(groupName)[0];
    }
    
    public static AudioMixerGroup GetMasterGroup()
    {
        return GetMixerGroup(masterGroupName);
    }
    
    public static AudioMixerGroup GetSFXGroup()
    {
        return GetMixerGroup(SFXGroupName);
    }
    
    public static AudioMixerGroup GetMusicGroup()
    {
        return GetMixerGroup(musicGroupName);
    }

    
}
