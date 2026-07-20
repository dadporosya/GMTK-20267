using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Per-object settings for a single dialogue node. There is one of these for every
/// object in DialogueContainerWithModels.objects, on every node.
/// </summary>
[Serializable]
public class ModelObjectNodeSettings
{
    [Tooltip("Enable or disable this object when the node starts.")]
    public bool enableObject = true;

    [Tooltip("Which model on the object to activate (index into its models list).")]
    public int modelId = 0;

    [Tooltip("Enable the eyes of the selected model.")]
    public bool enableEyes = false;

    [Tooltip("Does this object talk on this node? When true the object plays its " +
             "onNodeStartAnimation and its Talking animation controller for the duration " +
             "of the node. When false it just stands there with the chosen model/eyes.")]
    public bool talk = true;
}

/// <summary>
/// Settings for one dialogue node across every object in the container.
/// objectSettings is kept parallel to DialogueContainerWithModels.objects
/// (one entry per object).
/// </summary>
[Serializable]
public class MultiModelNodeSettings
{
    [Tooltip("Optional: index of the object (in the container's objects list) that speaks " +
             "this node - its name/portrait/voice fill the dialogue window. -1 = leave the " +
             "node's own speaker fields untouched.")]
    public int speakerObjectIndex = -1;

    [Tooltip("One entry per object in the container. Order matches the objects list.")]
    public List<ModelObjectNodeSettings> objectSettings = new List<ModelObjectNodeSettings>();
}

/// <summary>
/// A DialogueContainer override that steers several TalkableWithModels objects at once.
///
/// The objects list defines how many objects this dialogue controls (leave the entries
/// null in the asset - a TalkableWModelsController assigns the real scene objects at
/// runtime via AssignObjects).
///
/// nodeSettings is a grid kept parallel to the base nodes list: one MultiModelNodeSettings
/// per node, each holding one ModelObjectNodeSettings per object. When the DialogueManager
/// starts a node it calls OnNodeStart, which applies that node's row to every object.
/// Dialogue-level lifecycle (RunBeforeDialogueStart / OnDialogueStart / OnDialogueEnd) is
/// forwarded to each object so their intro/start/end animations and events fire, mirroring
/// the single-object Talkable flow.
/// </summary>
[CreateAssetMenu(fileName = "DialogueContainerWithModels", menuName = "Dialogues/DialogueContainerWithModels")]
public class DialogueContainerWithModels : DialogueContainer
{
    [Header("Multi-object dialogue")]
    [Tooltip("Objects this dialogue controls. Leave null in the asset to just define the " +
             "count; a TalkableWModelsController assigns the real scene objects at runtime.")]
    public List<TalkableWithModels> objects = new List<TalkableWithModels>();

    [Tooltip("Per-node, per-object settings. Kept in sync with 'nodes' x 'objects' " +
             "automatically. Index i matches nodes[i]; each row has one entry per object.")]
    public List<MultiModelNodeSettings> nodeSettings = new List<MultiModelNodeSettings>();

    protected override void OnEnable()
    {
        base.OnEnable();
        SyncNodeSettings();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        SyncNodeSettings();
    }
#endif

    /// <summary>
    /// Resizes nodeSettings so there is exactly one row per node, and one per-object entry
    /// per row. Preserves existing authored values.
    /// </summary>
    public void SyncNodeSettings()
    {
        if (nodeSettings == null) nodeSettings = new List<MultiModelNodeSettings>();

        int nodeCount = nodes != null ? nodes.Count : 0;
        int objectCount = objects != null ? objects.Count : 0;

        while (nodeSettings.Count < nodeCount) nodeSettings.Add(new MultiModelNodeSettings());
        while (nodeSettings.Count > nodeCount) nodeSettings.RemoveAt(nodeSettings.Count - 1);

        for (int i = 0; i < nodeSettings.Count; i++)
        {
            MultiModelNodeSettings row = nodeSettings[i];
            if (row == null) { row = new MultiModelNodeSettings(); nodeSettings[i] = row; }
            if (row.objectSettings == null) row.objectSettings = new List<ModelObjectNodeSettings>();

            while (row.objectSettings.Count < objectCount) row.objectSettings.Add(new ModelObjectNodeSettings());
            while (row.objectSettings.Count > objectCount) row.objectSettings.RemoveAt(row.objectSettings.Count - 1);
        }
    }

    /// <summary>
    /// Binds the concrete scene objects this dialogue should drive. Called by the controller
    /// right before the dialogue starts. Slot i of the asset becomes runtimeObjects[i].
    /// </summary>
    public void AssignObjects(List<TalkableWithModels> runtimeObjects)
    {
        if (objects == null) objects = new List<TalkableWithModels>();
        if (runtimeObjects == null)
        {
            SyncNodeSettings();
            return;
        }

        // Grow the slot list if the controller supplies more objects than the asset declared.
        while (objects.Count < runtimeObjects.Count) objects.Add(null);
        for (int i = 0; i < runtimeObjects.Count; i++) objects[i] = runtimeObjects[i];

        // Reconcile the per-node grid in case the object count changed.
        SyncNodeSettings();
    }

    /// <summary>
    /// Stages node 0 on every object, then plays each active object's intro animation and
    /// beforeDialogueStart events, waiting for them all. Call this before StartDialogue so
    /// the intro plays before the window opens (mirrors Talkable's before-dialogue step).
    /// </summary>
    public IEnumerator RunBeforeDialogueStart(MonoBehaviour runner)
    {
        // Stage the first node so intro animations have the right model to play on.
        StageNode(0);

        if (objects == null) yield break;

        List<Coroutine> running = new List<Coroutine>();
        for (int i = 0; i < objects.Count; i++)
        {
            TalkableWithModels obj = objects[i];
            if (obj && obj.isActiveAndEnabled)
                running.Add(runner.StartCoroutine(obj.PlayBeforeDialogueStart()));
        }
        for (int i = 0; i < running.Count; i++) yield return running[i];
    }

    /// <summary>Runs each active object's on-dialogue-start lifecycle (animation + events).</summary>
    public void OnDialogueStart()
    {
        if (objects == null) return;
        for (int i = 0; i < objects.Count; i++)
        {
            TalkableWithModels obj = objects[i];
            if (obj && obj.isActiveAndEnabled) obj.OnDialogueStart();
        }
    }

    /// <summary>Applies a node's enable/model/eyes to every object WITHOUT playing per-node anims.</summary>
    private void StageNode(int nodeIdx)
    {
        if (objects == null) return;
        if (nodeIdx < 0 || nodeIdx >= nodeSettings.Count) return;

        MultiModelNodeSettings row = nodeSettings[nodeIdx];
        if (row == null) return;

        int count = Mathf.Min(objects.Count, row.objectSettings.Count);
        for (int i = 0; i < count; i++)
        {
            TalkableWithModels obj = objects[i];
            if (!obj) continue;

            ModelObjectNodeSettings s = row.objectSettings[i];
            if (s == null) continue;

            obj.SetupForNode(s.enableObject, s.modelId, s.enableEyes);
        }
    }

    /// <summary>
    /// Applies a node's row to every object, in two phases.
    ///
    /// Phase 1 - transitions. Objects whose enabled state differs from the previous node play a
    /// cutscene: newly enabled objects are staged (model + eyes) and play their appearing
    /// cutscene; newly disabled objects play their end cutscene and then deactivate. Objects
    /// enabled on the first node are handled earlier by RunBeforeDialogueStart (their cutscene
    /// runs before the window opens); objects still enabled on the last node run their end
    /// cutscene when the window closes (OnDialogueEnd) - so node 0 skips the enable case and the
    /// last node's still-enabled objects are left untouched until dialogue end.
    ///
    /// This routine WAITS for every appearing cutscene to finish.
    ///
    /// Phase 2 - the node itself. Only once everything has appeared do the objects start the
    /// node (talking animation + per-node animation). The DialogueManager yields on this routine,
    /// so the node's text does not start typing until the appearing animations are done.
    /// </summary>
    public IEnumerator OnNodeStart(DialogueNode node, MonoBehaviour runner)
    {
        if (node == null) yield break;

        int idx = node.nodeId;
        if (idx < 0 || idx >= nodeSettings.Count) yield break;

        MultiModelNodeSettings row = nodeSettings[idx];
        if (row == null) yield break;

        // Optional: let one of the objects be the speaker shown in the window.
        if (row.speakerObjectIndex >= 0
            && row.speakerObjectIndex < objects.Count
            && objects[row.speakerObjectIndex])
        {
            node.InitFromGameObject(objects[row.speakerObjectIndex].gameObject);
        }

        int count = Mathf.Min(objects.Count, row.objectSettings.Count);

        // ---- Phase 1: enable/disable transitions ----
        List<Coroutine> appearing = new List<Coroutine>();
        for (int i = 0; i < count; i++)
        {
            TalkableWithModels obj = objects[i];
            if (!obj) continue;

            ModelObjectNodeSettings s = row.objectSettings[i];
            if (s == null) continue;

            // The object's active state from the previous node vs. what this node wants.
            bool wasEnabled = obj.gameObject.activeSelf;
            bool willEnable = s.enableObject;

            if (willEnable && !wasEnabled && idx != 0)
            {
                // Enabled mid-dialogue: appear first, the node starts after (phase 2).
                appearing.Add(runner.StartCoroutine(obj.EnterDuringDialogueRoutine(s.modelId, s.enableEyes)));
            }
            else if (!willEnable && wasEnabled)
            {
                // Disabled mid-dialogue: end cutscene runs alongside this node, then the
                // object deactivates. (Objects still enabled on the last node run their end
                // cutscene when the window closes, in OnDialogueEnd.)
                obj.DisableDuringDialogue();
            }
        }

        // Wait for every appearing animation before the node actually starts.
        for (int i = 0; i < appearing.Count; i++) yield return appearing[i];

        // ---- Phase 2: start the node on every object that stays on screen ----
        for (int i = 0; i < count; i++)
        {
            TalkableWithModels obj = objects[i];
            if (!obj) continue;

            ModelObjectNodeSettings s = row.objectSettings[i];
            if (s == null) continue;

            // Objects this node disables are mid-end-cutscene (or already gone) - leave them be,
            // deactivating them here would cut their end animation short.
            if (!s.enableObject) continue;

            obj.PlayDialogueNode(true, s.modelId, s.enableEyes, s.talk);
        }
    }

    /// <summary>Ends the node on every driven object (stops their per-node animations).</summary>
    public void OnNodeEnd(DialogueNode node)
    {
        if (objects == null) return;
        for (int i = 0; i < objects.Count; i++)
            if (objects[i]) objects[i].EndDialogueNode();
    }

    /// <summary>
    /// Runs each object's dialogue-end lifecycle: active objects play their end animation
    /// and then deactivate; inactive ones are reset synchronously.
    /// </summary>
    public void OnDialogueEnd()
    {
        if (objects == null) return;
        for (int i = 0; i < objects.Count; i++)
        {
            TalkableWithModels obj = objects[i];
            if (!obj) continue;

            if (obj.isActiveAndEnabled) obj.PlayOnDialogueEnd(true);
            else obj.ResetAfterDialogue();
        }
    }
}
