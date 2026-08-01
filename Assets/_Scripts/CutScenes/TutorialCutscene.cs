using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using PrimeTween;
using Unity.VisualScripting;


public class TutorialCutscene : CutSceneBase
{
    [SerializeField] private bool skipIntro = false;
    
    [SerializeField] private float delayBeforeDialogue;
    [SerializeField] float delayBeforeIntro;
    [SerializeField] private DialogueContainer introDialogue;
    [SerializeField] private DialogueContainer tutorialDialogue;

    [Header("Additional dialogue window scale")]
    [SerializeField] private float scaleAnimDuration = 0.4f;
    [SerializeField] private Ease scaleAnimEase = Ease.Default;

    // Cached once resolved so the scale-out path can still reach the window after we
    // deactivate the object.
    private Transform additionalWindow;
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
        if (skipIntro)
        {
            CardManager.Instance.RoundStart();
            yield break;
        }
        
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

        yield return new WaitForSeconds(delayBeforeIntro);
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
            SetAdditionalWindowVisible(false);
            DialogueManager.Instance.skipByMouse = true;
            DialogueManager.Instance.onDialogueEnd.RemoveListener(OnDialogueEnd);
            DestroyCutscene();
        }
        DialogueManager.Instance.onDialogueEnd.AddListener(OnDialogueEnd);
        
        DialogueManager.Instance.onDialogueStartEvents = false;
        // DialogueManager.Instance.skipByMouse = false;

        // Reveal the extra dialogue window used for the tutorial's second text box (scale in).
        SetAdditionalWindowVisible(true);

        DialogueManager.Instance.StartDialogue(tutorialDialogue);
        
    }

    /// <summary>
    /// Shows/hides the AdditionalDialogueWindow by animating its scale: 0 -> 1 when showing,
    /// 1 -> 0 when hiding. The object is enabled before scaling in, and disabled once it has
    /// finished scaling out.
    /// </summary>
    private void SetAdditionalWindowVisible(bool visible)
    {
        if (!ResolveAdditionalWindow()) return;

        if (visible)
        {
            // Enable the window, start from zero, then scale in.
            additionalWindow.localScale = Vector3.zero;
            additionalWindow.gameObject.SetActive(true);
            Tween.Scale(additionalWindow, Vector3.one, scaleAnimDuration, scaleAnimEase);
        }
        else
        {
            // Scale out, then disable the window once it reaches zero.
            Tween.Scale(additionalWindow, Vector3.zero, scaleAnimDuration, scaleAnimEase)
                .OnComplete(() => additionalWindow.gameObject.SetActive(false));
        }
    }

    /// <summary>
    /// Resolves (and caches) the AdditionalDialogueWindow transform. Finds it even when it is
    /// disabled (GameObject.FindGameObjectWithTag only sees active objects).
    /// </summary>
    private bool ResolveAdditionalWindow()
    {
        if (additionalWindow) return true;

        GameObject go = FindSceneObjectByTag("AdditionalDialogueWindow");
        if (!go)
        {
            h.Out("AdditionalDialogueWindow not found");
            return false;
        }

        additionalWindow = go.transform;
        return true;
    }

    /// <summary>
    /// Finds a scene GameObject by tag, including inactive/disabled ones (unlike
    /// GameObject.FindGameObjectWithTag). Prefab/asset objects that aren't part of a loaded scene
    /// are skipped.
    /// </summary>
    private GameObject FindSceneObjectByTag(string tag)
    {
        foreach (Transform t in Resources.FindObjectsOfTypeAll<Transform>())
        {
            if (!t.CompareTag(tag)) continue;
            if (!t.gameObject.scene.IsValid()) continue; // skip prefab/asset objects
            return t.gameObject;
        }
        return null;
    }

}