using UnityEngine;
using System.Collections;
using System.Collections.Generic;
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
public class SinCutsceneBase : CutSceneBase
{
    public CP.Suit sin;
    [Header("Place Holder Settings")]
    [SerializeField] private DialogueContainer placeholderDialogue;
    
    [Header("After Win")]
    [SerializeField] private float delayAfterWin = 2f;
    
    [Header("Environment")]
    [SerializeField] private Color lightColor=Color.white;
    [SerializeField] private float colorFadeDuration = 3f;
    [SerializeField] private float delayAfterColorChange = 1f;

    [Header("Dialogue")]
    [SerializeField] private DialogueContainer dialogue;
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
    /// -> dialogue start (place holder if suit was previusly selected), and camera turns to angel
    /// -> after dialogue gp starts again
    /// </summary>
    /// <returns></returns>
    
    public virtual IEnumerator DefaultStep()
    {
        yield return null;
        
        
        yield return OnWin();
        yield return TurnCameraToWindow();
        
        
        yield return null;
    }
    
    
    public virtual IEnumerator OnWin()
    {
        // TODO: some onWin effects like sfx and stuff
        yield return new WaitForSeconds(delayAfterWin);
        
        yield return null;
    }

    public virtual IEnumerator TurnCameraToWindow()
    {
        if (!TableTopCameraController.Instance) yield break;
        TableTopCameraController.Instance.SwitchToWindowView();
        yield return new WaitForSeconds(2f);
        
        yield return null;    
    }

    public virtual IEnumerator ChangeEnvironment()
    {
        /// tASK: change current light filter color to light color in colorFadeDuration.
        
        yield return null;
    }

    public virtual IEnumerator DialogueStart()
    {
        DialogueContainer desiredDialogue;
        if (TableManager.Instance.playedCutScenes.Contains(sin))
        {
            desiredDialogue = placeholderDialogue;
        }
        else
        {
            desiredDialogue = dialogue;
        }

        void OnDialogueEnd()
        {
            h.Out("Dialogue End Event BEBRA");
            StartCoroutine(DialogueEnd());
            DialogueManager.Instance.onDialogueEnd.RemoveListener(OnDialogueEnd);
        }
        
        DialogueManager.Instance.onDialogueEnd.AddListener(OnDialogueEnd);
        DialogueManager.Instance.StartDialogue(desiredDialogue);
        
        yield return null;
        // 
    }
    
    public virtual IEnumerator DialogueEnd()
    {
        if (!TableTopCameraController.Instance) yield break;
        TableTopCameraController.Instance.SwitchToWindowView();
        yield return null;
    }
}