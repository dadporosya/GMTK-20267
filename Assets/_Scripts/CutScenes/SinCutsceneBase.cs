using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;


public class SinCutsceneBase : CutSceneBase
{

    public override void Init()
    {
        base.Init();
        
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
    /// -> dialogue start, and camera turns to angel
    /// -> after dialogue gp starts again
    /// </summary>
    /// <returns></returns>
    public virtual IEnumerator DefaultStep()
    {
        yield return null;
        
        yield return null;
    }

    public virtual IEnumerator OnWin()
    {
        yield return null;
    }

    public virtual IEnumerator TurnCameraToWindow()
    {
     yield return null;    
    }

    public virtual IEnumerator ChangeEnvironment()
    {
        yield return null;
    }

    public virtual IEnumerator DialogueStart()
    {
        yield return null;
        // 
    }
    
    public virtual IEnumerator DialogueEnd()
    {
        yield return null;
    }


}