using System.Collections.Generic;
using EZCameraShake;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

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
    public static Taximeter Instance;
    
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

    [Header("Global light")]
    [Tooltip("Light recolored over the trip. Falls back to RenderSettings.sun, then the first Light " +
             "in the scene, if left empty.")]
    [SerializeField] private Light globalLight;
    [SerializeField] private bool  changeLightColor = true;
    [Tooltip("Color the light reaches exactly when the timer runs out. The start color is whatever " +
             "the light has when the trip starts.")]
    [SerializeField] private Color targetLightColor = Color.white;
    [Tooltip("Remaps normalized trip time before the color lerp. Leave linear for an even fade.")]
    [SerializeField] private AnimationCurve lightColorCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    private Color startLightColor;

    [Header("Ending")]
    [SerializeField] private float timeBeforeEndToCallEndingDialogue; // seconds left in the trip at which the ending dialogue fires
    private bool endDialogueWasCalled = false;

    [Header("\"Don't have much time\" dialogue")]
    [SerializeField] private float speedForDontHaveMuchTimeDialogue = 50f; // speed at/above which the dialogue fires
    private bool dontHaveMuchTimeDialogueWasCalled = false;

    [Header("Km OST")]
    [Tooltip("Switches the background music as the trip progresses: when the distance travelled passes " +
             "kmCounts[n], ost[n] is crossfaded in through the BGMManager.")]
    [SerializeField] public bool sequenceOst;
    [Tooltip("Tracks played over the trip, index-matched to kmCounts.")]
    [SerializeField] private List<AudioClip> ost = new List<AudioClip>();
    [Tooltip("Km TRAVELLED at which the matching ost entry starts. Ascending, e.g. 0, 3, 7, 10.")]
    [SerializeField] private List<float> kmCounts = new List<float>();
    [SerializeField] private float ostFadeTime = 1.5f;

    private int currentOstIndex = -1;

    [Header("Debug")]
    [Tooltip("While on, pressing debugSkipKey jumps the trip forward so only debugSkipToKm km are left.")]
    [SerializeField] private bool  debugSkipEnabled = true;
    [SerializeField] private Key   debugSkipKey = Key.L;
    [SerializeField] private float debugSkipToKm = 0.67f;

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
        h.CreateStaticInstance(this, ref Instance);
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
        currentOstIndex = -1;

        // Capture the light's current color as the start of the fade, so the trip always begins
        // from whatever the scene is lit with and ends on targetLightColor.
        Light light = ResolveGlobalLight();
        if (light) startLightColor = light.color;

        UpdateText();
        UpdateLightColor(0f);
    }

    private void Update()
    {
        HandleDebugSkip();

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
        UpdateLightColor(t);
        UpdateOst();

        // Fire the "don't have much time" dialogue once, when speed reaches its threshold.
        if (!dontHaveMuchTimeDialogueWasCalled && currentSpeed >= speedForDontHaveMuchTimeDialogue)
        {
            dontHaveMuchTimeDialogueWasCalled = true;
            EndingManager.Instance.StartDontHaveMuchTimeDialogue();
        }

        // Fire the ending dialogue once, when the trip has this many seconds left.
        if (!endDialogueWasCalled && (duration - elapsed) <= timeBeforeEndToCallEndingDialogue)
        {
            endDialogueWasCalled = true;
            // EndingManager.Instance.StartEndingDialogue();
        }

        if (t >= 1f || kmValue <= 0f)
        {
            kmValue      = 0f;
            currentSpeed = maxSpeed;   // both profiles are at maxSpeed by the end
            ApplyRoadSpeed();
            running      = false;
            UpdateText();
            UpdateLightColor(1f);   // land exactly on targetLightColor
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

    // ------------------------------------------------------------------ ost

    /// <summary>
    /// Distance driven so far, i.e. the counterpart of <see cref="kmValue"/> (which is km LEFT).
    /// </summary>
    public float KmTravelled => Mathf.Max(0f, totalKm - kmValue);

    /// <summary>
    /// Picks the track whose km threshold the trip has passed and crossfades it in through the
    /// BGMManager. Driven by distance rather than by one-shot flags, so debug skips and rewinds land
    /// on the right track too: the index is recomputed every frame and the music only changes when it
    /// actually differs from what is playing.
    /// </summary>
    private void UpdateOst()
    {
        if (!sequenceOst || ost == null || ost.Count == 0 || kmCounts == null) return;

        int index = ResolveOstIndex(KmTravelled);
        if (index < 0 || index == currentOstIndex) return;

        currentOstIndex = index;

        AudioClip clip = ost[index];
        if (!clip) return;   // an empty slot just leaves the previous track running

        if (BGMManager.Instance == null)
        {
            h.Out("Taximeter: no BGMManager to play the km OST on");
            return;
        }

        // PlayMusic crossfades between the manager's two sources, so the current track fades out
        // while the new one fades in.
        BGMManager.Instance.PlayMusic(clip, ostFadeTime);
    }

    /// <summary>
    /// Highest index whose kmCounts entry has been reached by <paramref name="kmTravelled"/>.
    /// Returns -1 while the trip is still short of the first threshold. Entries past the end of
    /// either list are ignored, so mismatched list lengths degrade gracefully.
    /// </summary>
    private int ResolveOstIndex(float kmTravelled)
    {
        int index = -1;
        int count = Mathf.Min(ost.Count, kmCounts.Count);

        for (int i = 0; i < count; i++)
            if (kmTravelled >= kmCounts[i]) index = i;

        return index;
    }

    // ---------------------------------------------------------------- debug

    /// <summary>Debug shortcut: press debugSkipKey to jump straight to debugSkipToKm km left.</summary>
    private void HandleDebugSkip()
    {
        if (!debugSkipEnabled || Keyboard.current == null) return;
        if (!Keyboard.current[debugSkipKey].wasPressedThisFrame) return;

        SetKmLeft(debugSkipToKm);
    }

    /// <summary>
    /// Moves the trip to the moment where <paramref name="km"/> km are left. kmValue is recomputed
    /// from elapsed every frame, so the clock itself has to be moved — writing kmValue alone would be
    /// overwritten on the next Update. Speed, road speed and the dialogue triggers all follow from
    /// elapsed, so they stay consistent with the new position.
    /// </summary>
    public void SetKmLeft(float km)
    {
        if (totalKm <= 0f) return;

        km = Mathf.Clamp(km, 0f, totalKm);

        float travelledFraction = 1f - km / totalKm;
        float t = acceleration ? InverseAccelFraction(travelledFraction) : travelledFraction;

        elapsed = Mathf.Clamp01(t) * duration;
        running = true;    // lets a finished trip be rewound for testing
        reached = false;   // so OnTargetReached still fires when the new position runs out

        h.Out("Taximeter: debug skip to", km, "km left");
    }

    /// <summary>
    /// Inverse of <see cref="AccelFraction"/>: the time-fraction t at which the given fraction of the
    /// distance has been covered. Solves area(t) = f * total, a quadratic in t, and takes the root in
    /// 0..1. Falls back to the linear case when the speed never ramps.
    /// </summary>
    private float InverseAccelFraction(float f)
    {
        f = Mathf.Clamp01(f);

        float total = (minSpeed + maxSpeed) * 0.5f;
        if (total <= 0f) return f;

        float a = (maxSpeed - minSpeed) * 0.5f;
        float c = -f * total;

        // No ramp: area is linear in t, so t = -c / minSpeed.
        if (Mathf.Abs(a) < 0.0001f) return minSpeed <= 0f ? f : Mathf.Clamp01(-c / minSpeed);

        float disc = minSpeed * minSpeed - 4f * a * c;
        if (disc < 0f) return f;

        return Mathf.Clamp01((-minSpeed + Mathf.Sqrt(disc)) / (2f * a));
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

    /// <summary>
    /// Lerps the global light from the color it had at trip start to <see cref="targetLightColor"/>,
    /// reaching it exactly at t = 1 (the end of the timer). Driven by normalized time rather than by
    /// a tween, so debug skips and rewinds stay in sync with the clock.
    /// </summary>
    private void UpdateLightColor(float t)
    {
        if (!changeLightColor) return;

        Light light = ResolveGlobalLight();
        if (!light) return;

        float k = lightColorCurve != null && lightColorCurve.length > 0
            ? lightColorCurve.Evaluate(Mathf.Clamp01(t))
            : Mathf.Clamp01(t);

        light.color = Color.Lerp(startLightColor, targetLightColor, k);
    }

    /// <summary>Light to recolor: the assigned one, else the sun, else the first Light in the scene.</summary>
    private Light ResolveGlobalLight()
    {
        if (globalLight) return globalLight;

        globalLight = RenderSettings.sun ? RenderSettings.sun : FindFirstObjectByType<Light>();
        return globalLight;
    }

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
