using UnityEngine;
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
        yield return null;

        
        if (runDialogue) DialogueManager.Instance.StartDialogue(dialogue);

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


        yield return null;
    }


}