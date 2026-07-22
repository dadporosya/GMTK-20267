using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class DistanceSound : MonoBehaviour
{

    [SerializeField] Transform playerTransform;
    [SerializeField] Transform targetTransform;

    [SerializeField] float maxDistance;
    [SerializeField] float minDistance;

    [SerializeField] float targetVolume;
    [SerializeField] float targetPitch;
    [SerializeField] float targetLPF;

    private AudioSource audioSource;
    private AudioLowPassFilter LPF;
    private float initialVolume;
    private float initialPitch;
    private float initialLPF;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        LPF = GetComponent<AudioLowPassFilter>();

        initialVolume = audioSource.volume;
        initialPitch = audioSource.pitch;
        initialLPF = LPF.cutoffFrequency;
    }

    void Update()
    {
        float currentDistance = Vector3.Distance(targetTransform.position, playerTransform.position);
        float clampedDistance = Mathf.Clamp(currentDistance, minDistance, maxDistance);
        float approachFactor = 1f - ((clampedDistance - minDistance) / (maxDistance - minDistance));
        audioSource.volume = Mathf.Lerp(initialVolume, targetVolume, approachFactor);
        audioSource.pitch = Mathf.Lerp(initialPitch, targetPitch, approachFactor);
        LPF.cutoffFrequency = Mathf.Lerp(initialLPF, targetLPF, approachFactor);
    }


}
