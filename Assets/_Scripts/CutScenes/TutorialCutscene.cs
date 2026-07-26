using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using PrimeTween;
using Unity.VisualScripting;


public class TutorialCutscene : CutSceneBase
{
    [SerializeField] private float delayBeforeDialogue;
    [SerializeField] private DialogueContainer introDialogue;
    [SerializeField] private DialogueContainer tutorialDialogue;
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
        
        float timeElapsed = 0f;
        while (timeElapsed < delayBeforeDialogue)
        {
            timeElapsed += Time.deltaTime;
            if (Input.GetKeyDown(KeyCode.Space)
                || Input.GetKeyDown(KeyCode.Mouse0)
                || Input.GetKeyDown(KeyCode.Return))
            {
                timeElapsed = delayBeforeDialogue - 1f;
            }
            yield return null;
        }
        
        yield return StartIntroDialogue();
    }
    
    public virtual IEnumerator StartIntroDialogue()
    {
        yield return null;

        void OnDialogueEnd()
        {
            StartCoroutine(StartTutorialDialogue());
            DialogueManager.Instance.onDialogueEnd.RemoveListener(OnDialogueEnd);
        }
        DialogueManager.Instance.onDialogueEnd.AddListener(OnDialogueEnd);
        
        DialogueManager.Instance.StartDialogue(introDialogue);
    }
    
    public virtual IEnumerator StartTutorialDialogue()
    {
        yield return null;
        yield return new WaitForSeconds(1f);
        CardManager.Instance.RoundStart();
        yield return new WaitForSeconds(2f);
        DialogueManager.Instance.dialogueTextId=1;
        
        void OnDialogueEnd()
        {
            DialogueManager.Instance.onDialogueStartEvents = true;
            DialogueManager.Instance.dialogueTextId=0;
            DialogueManager.Instance.onDialogueEnd.RemoveListener(OnDialogueEnd);
        }
        DialogueManager.Instance.onDialogueEnd.AddListener(OnDialogueEnd);
        
        DialogueManager.Instance.onDialogueStartEvents = false;
        DialogueManager.Instance.StartDialogue(tutorialDialogue);
        DestroyCutscene();
    }
}