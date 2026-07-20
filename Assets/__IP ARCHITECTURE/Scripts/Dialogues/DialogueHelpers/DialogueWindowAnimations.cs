using System.Collections;
using UnityEngine;
using PrimeTween;

// Open/close animation for the dialogue window. The window scales up from zero in two
// beats (X first with a sliver of Y, then Y to full) on start, and reverses on end.
// DialogueManager drives these: it waits for OnDialogueStart before showing text, and
// runs OnDialogueEnd alongside tearing the dialogue down (see DialogueManager).
public class DialogueWindowAnimations : MonoBehaviour
{
    public DialogueManager dialogueManager;

    [Header("Timing")]
    [Tooltip("Duration of the X open/close beat.")]
    [SerializeField] private float xDuration = 0.18f;
    [Tooltip("Duration of the Y open/close beat.")]
    [SerializeField] private float yDuration = 0.18f;
    [Tooltip("How far Y opens while X expands, before the full Y beat. 0..1.")]
    [SerializeField] private float ySliver = 0.1f;
    [SerializeField] private Ease openEase = Ease.OutBack;
    [SerializeField] private Ease closeEase = Ease.InBack;
    [SerializeField] private float delayBeforeStart = 0.5f;

    private Transform Window =>
        dialogueManager && dialogueManager.dialogueWindow
            ? dialogueManager.dialogueWindow.transform
            : null;

    private void Start()
    {
        if (!dialogueManager) dialogueManager = GetComponent<DialogueManager>();
        if (!dialogueManager) dialogueManager = FindObjectOfType<DialogueManager>();
    }

    // Grow from zero: X to 1 with a sliver of Y, then Y to full.
    public IEnumerator OnDialogueStart()
    {
        Transform w = Window;
        if (!w) yield break;

        Tween.StopAll(w);
        w.localScale = Vector3.zero;

        yield return Tween.Scale(w, new Vector3(1f, ySliver, 1f), xDuration, openEase)
            .ToYieldInstruction();
        yield return Tween.Scale(w, Vector3.one, yDuration, openEase)
            .ToYieldInstruction();

        yield return new WaitForSeconds(delayBeforeStart);
        
        w.localScale = Vector3.one;
    }

    // Opposite of the open: Y collapses to a sliver, then everything to zero.
    public IEnumerator OnDialogueEnd()
    {
        Transform w = Window;
        if (!w) yield break;

        Tween.StopAll(w);

        yield return Tween.Scale(w, new Vector3(1f, ySliver, 1f), yDuration, closeEase)
            .ToYieldInstruction();
        yield return Tween.Scale(w, Vector3.zero, xDuration, closeEase)
            .ToYieldInstruction();
        
        w.localScale = Vector3.zero;
    }
}
