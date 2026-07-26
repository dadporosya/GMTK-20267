using System.Collections.Generic;
using UnityEngine;

public class BGMManagerController : MonoBehaviour
{
    public AudioClip deafultBGMusic;
    public List<AudioClip> bgTracks;

    private void Start()
    {
        BGMManager.Instance.deafultBGMusic = deafultBGMusic;
        BGMManager.Instance.bgTracks = bgTracks;
        
        BGMManager.Instance.PlayMusic(deafultBGMusic, 1.67f);
    }
}