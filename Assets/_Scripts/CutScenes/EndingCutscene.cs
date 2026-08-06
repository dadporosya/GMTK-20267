using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;
using PrimeTween;
using Unity.VisualScripting;
using VolFx;

/// <summary>
/// sfx победы или что-то в этом роде
/// -> small delay
/// -> camera turns to the left window
/// -> environment completely changes it color to the matching sin's color. then, small delay
/// -> dialogue start, and camera turns to angel
/// -> after dialogue gp starts again
/// </summary>
/// <returns></returns>

public class EngingCutscene : CutSceneBase
{
    [SerializeField] private DialogueContainer dialogue;
    [Tooltip("Played instead of 'dialogue' when GamePlusManager reports every sin with a cutscene " +
             "has been achieved (collected). Leave empty to always use the default dialogue.")]
    [SerializeField] private DialogueContainer secretEndingDialogue;
    [SerializeField] private bool runDialogue = false;
    [SerializeField] private float fadeDurationInSeconds = 60;
    [Tooltip("Easing used for the VHS density / Invert weight / Bloom threshold fade.")]
    [SerializeField] private Ease fadeEase = Ease.Default;

    [Header("VHS / Invert end values")]
    [Tooltip("Value the VHS noise density fades to by the end of the cutscene (0..1).")]
    [Range(0f, 1f)]
    [SerializeField] private float vhsDensityTarget = 1f;
    [Tooltip("Value the Invert effect weight fades to by the end of the cutscene (0..1).")]
    [Range(0f, 1f)]
    [SerializeField] private float invertWeightTarget = 1f;

    [Header("Bloom threshold modulation")]
    [Tooltip("When enabled, the Bloom threshold is tweened to bloomThresholdTarget over the same fade.")]
    [SerializeField] private bool modulateBloom = true;
    [Tooltip("Bloom Volume Manager to modulate. Auto-found in the scene if left empty.")]
    [SerializeField] private BloomVolumeManager bloomVolumeManager;
    [Tooltip("Value the Bloom threshold fades to by the end of the cutscene.")]
    [SerializeField] private float bloomThresholdTarget = 0f;

    [SerializeField] private float delayBeforeBlackScreen=2f;

    [Header("Black screen / credits")]
    [Tooltip("How long the fully black screen is held after FadeIn before the credits scene loads.")]
    [SerializeField] private float blackScreenDuration = 3f;
    [Tooltip("Name of the credits scene to load once the cutscene ends. Must be added to Build Settings.")]
    [SerializeField] private string creditsSceneName = "CreditsScene";

    [SerializeField] private AudioClip ost;

    [Header("hoarime edited this shittt")]
    [SerializeField] AudioClip taxometerEndingSound;
    [SerializeField] AudioSource taxometerEndingAS;
    
    
    [Tooltip("If on, the object(s) tagged with eyeTag have their SpriteRenderer color set to the " +
             "sin's color during the cutscene, then restored on cutscene end.")]
    [SerializeField] private bool lightenEye = true;
    [Tooltip("If on, the eye is colored with this sin's color (CP.SuitColor). If off, eyeColor is used.")]
    [SerializeField] private bool eyeUseSinColor = true;
    [Tooltip("Color the eye's SpriteRenderer is set to when eyeUseSinColor is off.")]
    [SerializeField] private Color eyeColor = Color.white;
    [Tooltip("How much the eye's target color is washed out towards white. 0 = the raw sin/eye color, " +
             "1 = pure white. Use this to make the eye read brighter without changing the sin's palette.")]
    [Range(0f, 1f)]
    [SerializeField] private float eyeLightenAmount = 0f;
    [Tooltip("Multiplies the eye's target RGB after lightening. 1 = unchanged, >1 pushes the color " +
             "into HDR territory so it can bloom. Alpha is never touched.")]
    [SerializeField] private float eyeColorIntensity = 1f;
    [Tooltip("Tag used to find the eye object(s).")]
    [SerializeField] private string eyeTag = "AngelEye";
    private class EyeTintState
    {
        public SpriteRenderer renderer;
        public Color originalColor;
    }

    private readonly List<EyeTintState> eyeStates = new List<EyeTintState>();
    public override void Init()
    {
        base.Init();

        // A sin cutscene's real end point is the dialogue closing, which happens long after the step
        // sequence finishes. Keep the instance alive until DialogueEnd runs and calls DestroyCutscene().
        customDestroy = true;

        List<IEnumerator> rawSteps = new List<IEnumerator>()
        {
            DefaultStep(),

        };

        foreach (IEnumerator step in rawSteps)
        {
            cutsceneSteps.Add(step);
        }
    }

    /// <summary>
    /// sfx победы или что-то в этом роде
    /// -> small delay
    /// -> camera turns to the left window
    /// -> environment completely changes it color to the matching sin's color. then, small delay
    /// -> dialogue start (place holder if suit was previusly selected), and camera turns to angel
    /// -> after dialogue gp starts again
    /// </summary>
    /// <returns></returns>

    public virtual IEnumerator DefaultStep()
    {
        ChangeEyeColor(Color.white);
        yield return null;
        BGMManager.Instance.FadeOutMusic(1f);
        TableTopCameraController.Instance.ChangeMainState(TableTopCameraController.State.TaxometerView);
        taxometerEndingAS.PlayOneShot(taxometerEndingSound);
        GameObject.Find("---CARDS PARENT IDK---").gameObject.SetActive(false);
        yield return new WaitForSeconds(3.33f);
        // if (!ost) ost = 
        BGMManager.Instance.PlayMusic(ost, 3.33f);
        
        // Park the camera in Free and lock the home state there. The scene wires DialogueManager's
        // onDialogueEnd to TableTopCameraController.ChangeMainStateToHand, which would otherwise pull
        // the view back to the hand the moment the ending dialogue closes — the lock makes that call a
        // no-op. Nothing unlocks it: the run ends in the credits scene right after this.
        TableTopCameraController.Instance.ForceChangeMainState(TableTopCameraController.State.Free);
        TableTopCameraController.Instance.SetMainStateLocked(true);

        if (runDialogue)
        {
            DialogueManager.Instance.StartDialogue(ResolveDialogue());
        }

        // Fade the global volume's VHS noise density and the Invert effect weight to their configured
        // end values over fadeDurationInSeconds, using PrimeTween (fadeEase controls the easing). Both
        // overrides live on the global volume that VhsVolumeManager.Instance controls, so it's used to
        // reach both.
        VhsVolumeManager vm = VhsVolumeManager.Instance;
        if (vm != null)
        {
            vm.TweenFloat(vm.Vhs?._density, vhsDensityTarget, fadeDurationInSeconds, fadeEase);

            InvertVol invert = vm.Get<InvertVol>();
            if (invert != null)
                vm.TweenFloat(invert.m_Weight, invertWeightTarget, fadeDurationInSeconds, fadeEase);
        }

        // Modulate the Bloom threshold over the same fade so the scene's glow shifts with the change.
        if (modulateBloom)
        {
            if (bloomVolumeManager == null)
                bloomVolumeManager = FindFirstObjectByType<BloomVolumeManager>();

            if (bloomVolumeManager != null)
                bloomVolumeManager.TweenFloat(bloomVolumeManager.Bloom?.threshold,
                                              bloomThresholdTarget, fadeDurationInSeconds, fadeEase);
        }

        // Wait for every volume tween above to reach its desired value.
        yield return new WaitForSeconds(fadeDurationInSeconds);

        // Short beat, then snap the screen fully black (FadeIn to alpha 1 with a 0s duration).
        yield return new WaitForSeconds(delayBeforeBlackScreen);
        if (FadeImageController.Instance != null)
            FadeImageController.Instance.FadeIn(0f);
        BGMManager.Instance.FadeOutMusic(0f);

        // Hold the black screen, then load the credits scene.
        yield return new WaitForSeconds(blackScreenDuration);
        SceneManager.LoadScene(creditsSceneName);
    }

    /// <summary>
    /// The secret ending dialogue when the player has collected every sin that has a cutscene
    /// (<see cref="GamePlusManager.AreAllSinsAchieved"/>), otherwise the default one. Falls back to
    /// the default whenever no secret dialogue is assigned or the manager isn't in the scene.
    /// </summary>
    public virtual DialogueContainer ResolveDialogue()
    {
        if (secretEndingDialogue == null) return dialogue;

        GamePlusManager gamePlus = GamePlusManager.Instance;
        if (gamePlus == null)
        {
            h.Out("EngingCutscene: no GamePlusManager in the scene — using the default ending dialogue.");
            return dialogue;
        }

        if (!gamePlus.AreAllSinsAchieved())
        {
            h.Out("EngingCutscene: sins still missing", gamePlus.MissingSins(), "— using the default ending dialogue.");
            return dialogue;
        }

        h.Out("EngingCutscene: all sins achieved — running the secret ending dialogue.");
        return secretEndingDialogue;
    }

    /// <summary>
/// Находит все объекты с тегом <see cref="eyeTag"/> ("AngelEye") и плавно перекрашивает их
/// SpriteRenderer.color в цвет текущего sin'а (или в eyeColor, если eyeUseSinColor выключен),
/// с учётом осветления (eyeLightenAmount) и интенсивности (eyeColorIntensity).
/// Исходные цвета сохраняются в eyeStates, чтобы позже их можно было восстановить.
/// </summary>
private void ChangeEyeColor(Color colorIn)
{
    eyeStates.Clear();

    if (string.IsNullOrEmpty(eyeTag))
    {
        h.Out("SinCutsceneBase: eyeTag is empty — eye color skipped.");
        return;
    }

    // --- Определяем целевой цвет (аналог Lighten) ---
    Color baseColor = colorIn;
    Color target = Color.Lerp(baseColor, Color.white, Mathf.Clamp01(eyeLightenAmount));

    float intensity = Mathf.Max(0f, eyeColorIntensity);
    target.r *= intensity;
    target.g *= intensity;
    target.b *= intensity;
    target.a = baseColor.a;

    // --- Находим все рендереры глаза (аналог ResolveEyeRenderers) ---
    List<SpriteRenderer> eyeRenderers = new List<SpriteRenderer>();
    foreach (GameObject go in GameObject.FindGameObjectsWithTag(eyeTag))
    {
        if (!go) continue;
        foreach (SpriteRenderer sr in go.GetComponentsInChildren<SpriteRenderer>(true))
            if (sr && !eyeRenderers.Contains(sr)) eyeRenderers.Add(sr);
    }

    if (eyeRenderers.Count == 0)
    {
        h.Out($"SinCutsceneBase: no object tagged '{eyeTag}' with a SpriteRenderer found — eye color skipped.");
        return;
    }

    // --- Перекрашиваем каждый найденный рендерер, запоминая исходный цвет ---
    foreach (SpriteRenderer sr in eyeRenderers)
    {
        if (!sr) continue;

        eyeStates.Add(new EyeTintState { renderer = sr, originalColor = sr.color });

        // Alpha оставляем как было у рендерера — меняем только RGB.
        Color finalTarget = target;
        finalTarget.a = sr.color.a;

        Tween.Custom(sr, sr.color, finalTarget, 1,
            (SpriteRenderer r, Color c) => { if (r) r.color = c; });
    }
}
}