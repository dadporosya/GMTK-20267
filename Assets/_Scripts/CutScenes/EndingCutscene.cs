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
    [SerializeField] private float fadeDurationInSeconds = 60;
    [Tooltip("Easing used for the VHS density / Invert weight fade.")]
    [SerializeField] private Ease fadeEase = Ease.Default;
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

        
        DialogueManager.Instance.StartDialogue(dialogue);

        // Fade the global volume's VHS noise density and the Invert effect weight both to full over
        // fadeDurationInSeconds, using PrimeTween (fadeEase controls the easing). Both overrides live
        // on the global volume that VhsVolumeManager.Instance controls, so it's used to reach both.
        VhsVolumeManager vm = VhsVolumeManager.Instance;
        if (vm != null)
        {
            vm.TweenFloat(vm.Vhs?._density, 1f, fadeDurationInSeconds, fadeEase);

            InvertVol invert = vm.Get<InvertVol>();
            if (invert != null)
                vm.TweenFloat(invert.m_Weight, 1f, fadeDurationInSeconds, fadeEase);
        }

        yield return null;
    }


}