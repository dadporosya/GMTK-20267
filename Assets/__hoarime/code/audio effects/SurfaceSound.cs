using UnityEngine;

public class SurfaceSound : MonoBehaviour
{
    [SerializeField] AudioClip[] footstepSounds;

    public AudioClip GetSound()
    {
        return footstepSounds[Random.Range(0, footstepSounds.Length)];
    }
}

