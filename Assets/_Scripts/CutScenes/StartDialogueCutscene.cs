using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using PrimeTween;
using Unity.VisualScripting;

/// <summary>
/// sfx победы или что-то в этом роде
/// -> small delay
/// -> camera turns to the left window
/// -> environment completely changes it color to the matching sin's color. then, small delay
/// -> dialogue start, and camera turns to angel
/// -> after dialogue gp starts again
/// </summary>
/// <returns></returns>

public class WeDontHaveMuchTimeCutscene : CutSceneBase
{
    [SerializeField] private DialogueContainer dialogue;

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


        yield return null;
    }
    



}