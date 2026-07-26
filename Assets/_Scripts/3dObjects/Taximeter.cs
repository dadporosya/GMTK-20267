using EZCameraShake;
using TMPro;
using UnityEngine;

/// <summary>
/// A trip timer. The total distance is DERIVED from the speed settings and the
/// duration, then counted down to exactly 0 as the time runs out.
///
///   acceleration == false : the car moves at maxSpeed the whole time.
///                           total km = maxSpeed * time.
///   acceleration == true  : the car starts at minSpeed and reaches maxSpeed at the
///                           end, so total km = average(min, max) * time.
///
/// Speed is treated as km/h and time as minutes. kmValue is an OUTPUT: it is
/// computed on start and burns down to 0 exactly when the timer ends.
/// The TMP text is continuously refreshed with kmValue + label.
/// </summary>
public class Taximeter : MonoBehaviour
{
    [Header("Speed (km/h)")]
    [SerializeField] private float minSpeed = 0f;
    [SerializeField] private float maxSpeed = 60f;
    [SerializeField] private float currentSpeed;         // instantaneous speed, updated every frame
    [SerializeField] private bool  acceleration;

    [Header("Road")]
    [SerializeField] private float roadSpeedScale = 1f;  // km/h -> road units

    [Header("Camera shake")]
    [SerializeField] private float speedThreshHoldForCameraShake = 40f;  // shake begins at this speed
    [SerializeField] private float maxCameraShake = 2f;                  // magnitude at max speed
    [SerializeField] private float cameraShakeRoughness = 6f;

    private CameraShakeInstance shakeInstance;

    [Header("Display")]
    [SerializeField] private TMP_Text text;
    [SerializeField] private string  label = " km";
    [SerializeField] private int     decimalPlaces = 1;   // digits shown after the comma

    [Header("Trip")]
    [SerializeField] private float kmValue;              // OUTPUT: km left, computed then counted to 0
    [SerializeField] private float timeInMinutes = 5f;

    [Header("Ending")]
    [SerializeField] private float timeBeforeEndToCallEndingDialogue; // seconds left in the trip at which the ending dialogue fires
    private bool endDialogueWasCalled = false;

    private float totalKm;    // full distance for this trip, computed from speed + time
    private float elapsed;    // seconds
    private float duration;   // seconds
    private bool  running;
    private bool  reached;

    public float CurrentSpeed => currentSpeed;
    public float KmValue      => kmValue;
    public float TotalKm      => totalKm;
    public bool  IsRunning    => running;

    private void Start()
    { 

        StartTrip();
    }

    /// <summary>Computes the trip distance from the current speed/time and starts the countdown.</summary>
    public void StartTrip()
    {
        duration = Mathf.Max(0.0001f, timeInMinutes * 60f);
        float hours = timeInMinutes / 60f;

        // Average speed over the trip: constant maxSpeed, or the mean of the ramp.
        float avgSpeed = acceleration ? (minSpeed + maxSpeed) * 0.5f : maxSpeed;
        totalKm = avgSpeed * hours;

        kmValue = totalKm;
        elapsed = 0f;
        reached = false;
        running = true;
        UpdateText();
    }

    private void Update()
    {
        if (!running) return;

        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / duration);   // normalized time 0..1

        // Instantaneous speed (for display / other systems to read).
        currentSpeed = acceleration ? Mathf.Lerp(minSpeed, maxSpeed, t) : maxSpeed;
        ApplyRoadSpeed();
        UpdateCameraShake();

        // Distance already travelled = integral of the speed curve, normalized so it
        // equals totalKm at t = 1. km left is the remainder.
        float travelledFraction = acceleration ? AccelFraction(t) : t;
        kmValue = Mathf.Max(0f, totalKm * (1f - travelledFraction));

        UpdateText();

        // Fire the ending dialogue once, when the trip has this many seconds left.
        if (!endDialogueWasCalled && (duration - elapsed) <= timeBeforeEndToCallEndingDialogue)
        {
            endDialogueWasCalled = true;
            EndingManager.Instance.StartEndingDialogue();
        }

        if (t >= 1f || kmValue <= 0f)
        {
            kmValue      = 0f;
            currentSpeed = maxSpeed;   // both profiles are at maxSpeed by the end
            ApplyRoadSpeed();
            running      = false;
            UpdateText();
            // Camera shake intentionally left running when time hits 0.

            if (!reached)
            {
                reached = true;
                OnTargetReached();
            }
        }
    }

    /// <summary>
    /// Fraction of the total distance covered by time-fraction t when speed ramps
    /// linearly from minSpeed to maxSpeed. It is the normalized integral of the
    /// speed curve, so it returns exactly 1 at t = 1 for any min/max pair.
    /// </summary>
    private float AccelFraction(float t)
    {
        float area  = minSpeed * t + (maxSpeed - minSpeed) * t * t * 0.5f;
        float total = (minSpeed + maxSpeed) * 0.5f;
        return total <= 0f ? t : area / total;   // fallback if both speeds are 0
    }

    /// <summary>Pushes the current speed onto the road manager.</summary>
    private void ApplyRoadSpeed()
    {
            EndlessRoadManager.Instance.speed = currentSpeed * roadSpeedScale;
    }

    /// <summary>
    /// Continuous camera shake that switches on at speedThreshHoldForCameraShake and
    /// grows toward maxCameraShake as speed climbs from the threshold to maxSpeed.
    /// </summary>
    private void UpdateCameraShake()
    {
        if (maxCameraShake <= 0f || CameraShaker.Instance == null) return;

        // 0 at the threshold, 1 at maxSpeed.
        float range     = Mathf.Max(0.0001f, maxSpeed - speedThreshHoldForCameraShake);
        float intensity = Mathf.Clamp01((currentSpeed - speedThreshHoldForCameraShake) / range);

        if (currentSpeed < speedThreshHoldForCameraShake)
        {
            if (shakeInstance != null) shakeInstance.ScaleMagnitude = 0f;
            return;
        }

        if (shakeInstance == null)
        {
            // Sustained shake we keep alive and scale each frame. Influence vectors
            // must be set explicitly, otherwise the shaker multiplies the shake by
            // zero and nothing moves.
            // shakeInstance = new CameraShakeInstance(maxCameraShake, cameraShakeRoughness)
            // {
            //     DeleteOnInactive  = false,
            //     PositionInfluence = CameraShaker.Instance.DefaultPosInfluence,
            //     RotationInfluence = CameraShaker.Instance.DefaultRotInfluence
            // };
            shakeInstance = CameraShaker.Instance.StartShake(0, cameraShakeRoughness, 0);
        }

        shakeInstance.Magnitude = intensity;
        // h.Out(shakeInstance.Magnitude);
    }

    /// <summary>Fades the shake out and lets it clean itself up.</summary>
    private void StopCameraShake()
    {
        if (shakeInstance == null) return;

        shakeInstance.DeleteOnInactive = true;
        shakeInstance.StartFadeOut(0.5f);
        shakeInstance = null;
    }

    private void OnDisable() => StopCameraShake();

    private void UpdateText()
    {
        if (text == null) return;

        int places = Mathf.Max(0, decimalPlaces);
        string format = places == 0 ? "0" : "0." + new string('0', places);
        text.text = kmValue.ToString(format) + label;
    }

    /// <summary>Called once when km reaches 0 or the time runs out.</summary>
    private void OnTargetReached()
    {
        EndingManager.Instance.StartEndingCutscene();
    }
}
