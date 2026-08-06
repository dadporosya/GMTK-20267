using System.Collections.Generic;
using EZCameraShake;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class Taximeter : MonoBehaviour
{
    public static Taximeter Instance;

    [Header("Speed (km/h)")]
    [SerializeField] private float minSpeed = 0f;
    [SerializeField] private float maxSpeed = 60f;
    [SerializeField] private float currentSpeed;
    [SerializeField] private bool acceleration;

    [Header("Road")]
    [SerializeField] private float roadSpeedScale = 1f;

    [Header("Camera shake")]
    [SerializeField] private float speedThreshHoldForCameraShake = 40f;
    [SerializeField] private float maxCameraShake = 2f;
    [SerializeField] private float cameraShakeRoughness = 6f;

    private CameraShakeInstance shakeInstance;

    [Header("Display")]
    [SerializeField] private TMP_Text text;
    [SerializeField] private string label = " km";
    [SerializeField] private int decimalPlaces = 1;

    [Header("Trip")]
    [SerializeField] private float kmValue;
    [SerializeField] private float timeInMinutes = 5f;

    [Header("Global light")]
    [SerializeField] private Light globalLight;
    [SerializeField] private bool changeLightColor = true;
    [SerializeField] private Color targetLightColor = Color.white;
    [SerializeField] private AnimationCurve lightColorCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    private Color startLightColor;

    [Header("Ending")]
    [SerializeField] private float timeBeforeEndToCallEndingDialogue;
    private bool endDialogueWasCalled = false;

    [Header("\"Don't have much time\" dialogue")]
    [SerializeField] private float speedForDontHaveMuchTimeDialogue = 50f;
    private bool dontHaveMuchTimeDialogueWasCalled = false;

    [Header("Km OST")]
    [SerializeField] public bool sequenceOst;
    [SerializeField] private List<AudioClip> ost = new List<AudioClip>();
    [SerializeField] private List<float> kmCounts = new List<float>();
    [SerializeField] private float ostFadeTime = 1.5f;

    private int currentOstIndex = -1;
    private bool cutscenePlaying = false;

    [Header("Debug")]
    [SerializeField] private bool debugSkipEnabled = true;
    [SerializeField] private Key debugSkipKey = Key.L;
    [SerializeField] private float debugSkipToKm = 0.67f;

    private float totalKm;
    private float elapsed;
    private float duration;
    private bool running;
    private bool reached;

    public float CurrentSpeed => currentSpeed;
    public float KmValue => kmValue;
    public float TotalKm => totalKm;
    public bool IsRunning => running;

    private void Start()
    {
        h.CreateStaticInstance(this, ref Instance);
        StartTrip();
    }

    public void StartTrip()
    {
        duration = Mathf.Max(0.0001f, timeInMinutes * 60f);
        float hours = timeInMinutes / 60f;

        float avgSpeed = acceleration ? (minSpeed + maxSpeed) * 0.5f : maxSpeed;
        totalKm = avgSpeed * hours;

        kmValue = totalKm;
        elapsed = 0f;
        reached = false;
        running = true;
        currentOstIndex = -1;

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
        float t = Mathf.Clamp01(elapsed / duration);

        currentSpeed = acceleration ? Mathf.Lerp(minSpeed, maxSpeed, t) : maxSpeed;
        ApplyRoadSpeed();
        UpdateCameraShake();

        float travelledFraction = acceleration ? AccelFraction(t) : t;
        kmValue = Mathf.Max(0f, totalKm * (1f - travelledFraction));

        UpdateText();
        UpdateLightColor(t);
        UpdateOst();

        if (!dontHaveMuchTimeDialogueWasCalled && currentSpeed >= speedForDontHaveMuchTimeDialogue)
        {
            dontHaveMuchTimeDialogueWasCalled = true;
            EndingManager.Instance.StartDontHaveMuchTimeDialogue();
        }

        if (!endDialogueWasCalled && (duration - elapsed) <= timeBeforeEndToCallEndingDialogue)
        {
            endDialogueWasCalled = true;
        }

        if (t >= 1f || kmValue <= 0f)
        {
            kmValue = 0f;
            currentSpeed = maxSpeed;
            ApplyRoadSpeed();
            running = false;
            UpdateText();
            UpdateLightColor(1f);

            if (!reached)
            {
                reached = true;
                OnTargetReached();
            }
        }
    }

    private float AccelFraction(float t)
    {
        float area = minSpeed * t + (maxSpeed - minSpeed) * t * t * 0.5f;
        float total = (minSpeed + maxSpeed) * 0.5f;
        return total <= 0f ? t : area / total;
    }

    public float KmTravelled => Mathf.Max(0f, totalKm - kmValue);

    private void UpdateOst()
    {
        if (!sequenceOst || ost == null || ost.Count == 0 || kmCounts == null) return;

        if (cutscenePlaying) return;

        int index = ResolveOstIndex(KmTravelled);
        if (index < 0 || index == currentOstIndex) return;

        currentOstIndex = index;

        AudioClip clip = ost[index];
        if (!clip) return;

        if (BGMManager.Instance == null)
        {
            h.Out("Taximeter: no BGMManager to play the km OST on");
            return;
        }

        BGMManager.Instance.PlayMusic(clip, ostFadeTime);
    }

    public void OnCutsceneStart()
    {
        cutscenePlaying = true;
    }

    public void OnCutsceneEnd()
    {
        cutscenePlaying = false;

        // Play whatever OST should be playing based on current distance
        if (sequenceOst && ost != null && ost.Count > 0 && kmCounts != null)
        {
            int index = ResolveOstIndex(KmTravelled);
            if (index >= 0 && index != currentOstIndex)
            {
                currentOstIndex = index;
                AudioClip clip = ost[index];
                if (clip && BGMManager.Instance)
                {
                    BGMManager.Instance.PlayMusic(clip, ostFadeTime);
                }
            }
        }
    }

    /// <summary>
    /// Called when a sin cutscene ends and the sin's soundtrack should keep playing.
    /// Resumes OST tracking without changing the current music.
    /// </summary>
    public void OnCutsceneEnd_KeepCurrentMusic()
    {
        cutscenePlaying = false;
        // Update currentOstIndex to match current distance, but don't play anything
        if (sequenceOst && ost != null && ost.Count > 0 && kmCounts != null)
        {
            currentOstIndex = ResolveOstIndex(KmTravelled);
        }
    }

    private int ResolveOstIndex(float kmTravelled)
    {
        int index = -1;
        int count = Mathf.Min(ost.Count, kmCounts.Count);

        for (int i = 0; i < count; i++)
            if (kmTravelled >= kmCounts[i]) index = i;

        return index;
    }

    private void HandleDebugSkip()
    {
        if (!debugSkipEnabled || Keyboard.current == null) return;
        if (!Keyboard.current[debugSkipKey].wasPressedThisFrame) return;

        SetKmLeft(debugSkipToKm);
    }

    public void SetKmLeft(float km)
    {
        if (totalKm <= 0f) return;

        km = Mathf.Clamp(km, 0f, totalKm);

        float travelledFraction = 1f - km / totalKm;
        float t = acceleration ? InverseAccelFraction(travelledFraction) : travelledFraction;

        elapsed = Mathf.Clamp01(t) * duration;
        running = true;
        reached = false;

        h.Out("Taximeter: debug skip to", km, "km left");
    }

    private float InverseAccelFraction(float f)
    {
        f = Mathf.Clamp01(f);

        float total = (minSpeed + maxSpeed) * 0.5f;
        if (total <= 0f) return f;

        float a = (maxSpeed - minSpeed) * 0.5f;
        float c = -f * total;

        if (Mathf.Abs(a) < 0.0001f) return minSpeed <= 0f ? f : Mathf.Clamp01(-c / minSpeed);

        float disc = minSpeed * minSpeed - 4f * a * c;
        if (disc < 0f) return f;

        return Mathf.Clamp01((-minSpeed + Mathf.Sqrt(disc)) / (2f * a));
    }

    private void ApplyRoadSpeed()
    {
        EndlessRoadManager.Instance.speed = currentSpeed * roadSpeedScale;
    }

    private void UpdateCameraShake()
    {
        if (maxCameraShake <= 0f || CameraShaker.Instance == null) return;

        float range = Mathf.Max(0.0001f, maxSpeed - speedThreshHoldForCameraShake);
        float intensity = Mathf.Clamp01((currentSpeed - speedThreshHoldForCameraShake) / range);

        if (currentSpeed < speedThreshHoldForCameraShake)
        {
            if (shakeInstance != null) shakeInstance.ScaleMagnitude = 0f;
            return;
        }

        if (shakeInstance == null)
        {
            shakeInstance = CameraShaker.Instance.StartShake(0, cameraShakeRoughness, 0);
        }

        shakeInstance.Magnitude = intensity;
    }

    private void StopCameraShake()
    {
        if (shakeInstance == null) return;

        shakeInstance.DeleteOnInactive = true;
        shakeInstance.StartFadeOut(0.5f);
        shakeInstance = null;
    }

    private void OnDisable() => StopCameraShake();

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

    private void OnTargetReached()
    {
        EndingManager.Instance.StartEndingCutscene();
    }
}