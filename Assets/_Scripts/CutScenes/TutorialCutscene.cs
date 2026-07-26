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

    [Header("Additional dialogue window dissolve")]
    [SerializeField] private float dissolveAnimDuration = 0.4f;
    [SerializeField] private Ease dissolveAnimEase = Ease.Default;

    // Cached once resolved (while the window is still active/findable) so the dissolve-out path
    // can still reach it after we deactivate the object.
    private MasterMaterialController additionalWindowMat;
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
            DialogueManager.Instance.skipByMouse = true;
            DialogueManager.Instance.onDialogueEnd.RemoveListener(OnDialogueEnd);
        }
        DialogueManager.Instance.onDialogueEnd.AddListener(OnDialogueEnd);
        
        DialogueManager.Instance.onDialogueStartEvents = false;
        DialogueManager.Instance.skipByMouse = false;

        // Reveal the extra dialogue window used for the tutorial's second text box (dissolve in).
        SetAdditionalWindowDissolve(true);

        DialogueManager.Instance.StartDialogue(tutorialDialogue);
        DestroyCutscene();
    }

    /// <summary>
    /// Shows/hides the AdditionalDialogueWindow by animating its MasterMaterialController dissolve
    /// amount (1 = fully dissolved/hidden, 0.333 = visible) instead of snapping the GameObject
    /// active state. When showing, the object is enabled before the dissolve-in; when hiding, it
    /// is disabled once the dissolve-out finishes.
    /// </summary>
    private void SetAdditionalWindowDissolve(bool visible)
    {
        MasterMaterialController matController = ResolveAdditionalWindowMat();
        if (!matController) return;

        GameObject go = matController.gameObject;
        float from = matController.GetDissolveAmount();
        float target = visible ? 0.333f : 1f;

        if (visible)
        {
            // Enable the window and turn the dissolve effect on, then dissolve in.
            go.SetActive(true);
            matController.SetDissolve(true);
            Tween.Custom(from, target, dissolveAnimDuration,
                val => matController.SetDissolveAmount(val), dissolveAnimEase);
        }
        else
        {
            // Dissolve out, then disable the window once it is fully dissolved.
            Tween.Custom(from, target, dissolveAnimDuration,
                    val => matController.SetDissolveAmount(val), dissolveAnimEase)
                .OnComplete(() => go.SetActive(false));
        }
    }

    /// <summary>
    /// Resolves (and caches) the MasterMaterialController on the AdditionalDialogueWindow. The
    /// lookup relies on GameObject.FindGameObjectWithTag, which only sees active objects, so it is
    /// cached the first time the window is found while still active.
    /// </summary>
    private MasterMaterialController ResolveAdditionalWindowMat()
    {
        if (additionalWindowMat) return additionalWindowMat;

        GameObject additionalWindow = GameObject.FindGameObjectWithTag("AdditionalDialogueWindow");
        if (!additionalWindow)
        {
            h.Out("AdditionalDialogueWindow not found");
            return null;
        }

        additionalWindowMat = additionalWindow.GetComponentInChildren<MasterMaterialController>(true);
        if (!additionalWindowMat)
            h.Out("AdditionalDialogueWindow has no MasterMaterialController");

        return additionalWindowMat;
    }

}