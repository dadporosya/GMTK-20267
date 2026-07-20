using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Drives multi-object dialogues. Holds the concrete scene objects and a set of
/// DialogueContainerWithModels assets. Works like Talkable: Talk starts a dialogue - but
/// first it binds this controller's objects into the chosen container so its per-node,
/// per-object settings drive the right scene objects, and it plays each object's
/// before-dialogue-start lifecycle before the window opens.
///
/// Objects are bound by index: container slot i is driven by objects[i].
/// </summary>
public class TalkableWModelsController : MonoBehaviour
{
    [Tooltip("The scene objects this controller drives. Index i is bound to slot i of " +
             "each dialogue's objects list.")]
    public List<TalkableWithModels> objects = new List<TalkableWithModels>();

    [Tooltip("Multi-object dialogues this controller can play.")]
    public List<DialogueContainerWithModels> dialogues = new List<DialogueContainerWithModels>();

    [SerializeField] private DialogueManager dialogueManager;

    private void Awake()
    {
        if (!dialogueManager) dialogueManager = FindFirstObjectByType<DialogueManager>();
    }

    /// <summary>
    /// Starts dialogue dialogueId (random when out of range), binding this controller's
    /// objects to it and running their before-dialogue-start lifecycle first.
    /// </summary>
    public void Talk(int dialogueId = -1)
    {
        if (dialogues == null || dialogues.Count == 0)
        {
            h.Out("TalkableWModelsController: no dialogues assigned.");
            return;
        }

        DialogueContainerWithModels dialogue =
            dialogueId >= 0 && dialogueId < dialogues.Count
                ? dialogues[dialogueId]
                : h.RandChoice(dialogues);

        PlayDialogue(dialogue);
    }

    public void PlayDialogue(DialogueContainerWithModels dialogue)
    {
        if (!dialogue)
        {
            h.Out($"TalkableWModelsController: dialogue {dialogue.name} is null.");
            return;
        }

        if (!dialogueManager) dialogueManager = FindFirstObjectByType<DialogueManager>();
        if (!dialogueManager)
        {
            h.Out("TalkableWModelsController: no DialogueManager in scene.");
            return;
        }
        h.Out($"Start talking {dialogue}");
        StartCoroutine(TalkRoutine(dialogue));
    }

    /// <summary>
    /// Binds the objects, plays each object's intro lifecycle to completion (mirrors
    /// Talkable), then hands the dialogue to the manager so the window opens afterwards.
    /// </summary>
    private IEnumerator TalkRoutine(DialogueContainerWithModels dialogue)
    {
        dialogue.AssignObjects(objects);

        yield return StartCoroutine(dialogue.RunBeforeDialogueStart(this));

        dialogueManager.StartDialogue(dialogue);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Q))
        {
            Talk();
            h.Out("Dialogue controller");
        }
    }
}
//