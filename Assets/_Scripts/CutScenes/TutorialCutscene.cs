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
            SetAdditionalWindowDissolve(false);
            DialogueManager.Instance.onDialogueEnd.RemoveListener(OnDialogueEnd);
        }
        DialogueManager.Instance.onDialogueEnd.AddListener(OnDialogueEnd);
        
        DialogueManager.Instance.onDialogueStartEvents = false;

        // Reveal the extra dialogue window used for the tutorial's second text box (dissolve in).
        SetAdditionalWindowDissolve(true);

        DialogueManager.Instance.StartDialogue(tutorialDialogue);
        DestroyCutscene();
    }

    /// <summary>
    /// Shows/hides the AdditionalDialogueWindow through its MasterMaterialController dissolve
    /// amount instead of toggling the GameObject active state: 1 = fully dissolved (hidden),
    /// 0.333 = visible.
    /// </summary>
    private void SetAdditionalWindowDissolve(bool visible)
    {
        GameObject additionalWindow = GameObject.FindGameObjectWithTag("AdditionalDialogueWindow");
        if (!additionalWindow)
        {
            h.Out("AdditionalDialogueWindow not found");
            return;
        }

        MasterMaterialController matController =
            additionalWindow.GetComponentInChildren<MasterMaterialController>(true);
        if (!matController)
        {
            h.Out("AdditionalDialogueWindow has no MasterMaterialController");
            return;
        }

        matController.SetDissolveAmount(visible ? 0.333f : 1f);
    }

}