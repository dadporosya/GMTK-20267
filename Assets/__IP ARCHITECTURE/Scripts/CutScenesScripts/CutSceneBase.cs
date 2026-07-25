using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;


public class CutSceneBase : MonoBehaviour
{
    public List<IEnumerator> cutsceneSteps = new List<IEnumerator>();
    [SerializeField] private bool runOnStart = false;

    [Tooltip("If off (default), the cutscene instance is destroyed automatically once the step sequence " +
             "finishes. If on, finishing the steps will NOT destroy it — the cutscene stays alive until " +
             "DestroyCutscene() is called (e.g. from a dialogue-end callback), so end methods that outlive " +
             "the step loop can still run.")]
    [SerializeField] protected bool customDestroy = false;

    private bool initialized=false;

    private void Awake()
    {
        Init();
    }

    void Start()
    {
        if (runOnStart) Run();
    }

    public virtual void Init()
    {
        initialized = true;
    }

    public void Run()
    {
        // if (!initialized) Init();
        CutSceneBase instance = Instantiate(this);
        instance.StartCoroutine(instance.ExecuteSequence(instance.gameObject));
    }

    public IEnumerator ExecuteSequence(GameObject instanceToDestroy = null)
    {
        // h.Out("Execute Sequence");
        if (!initialized) Init();
        // h.Out(cutsceneSteps);

        // Remember what to tear down so DestroyCutscene() can finish the job later when customDestroy is on.
        if (instanceToDestroy) this.instanceToDestroy = instanceToDestroy;

        foreach (IEnumerator step in cutsceneSteps)
        {
            // h.Out(step);
            yield return StartCoroutine(step);
        }

        h.Out("Cutscene complete.");

        // When customDestroy is on, the sequence finishing is NOT the end of the cutscene — something
        // still running (a dialogue, a delayed callback, ...) owns the lifetime and must call
        // DestroyCutscene() when it is truly done. Otherwise, clean up immediately as before.
        if (customDestroy) yield break;

        DestroyCutscene();
    }

    /// <summary>
    /// Destroys this running cutscene instance. Call this when <see cref="customDestroy"/> is on and the
    /// cutscene's real end point is later than the step sequence finishing (e.g. from a dialogue-end
    /// callback), so end methods like DialogueEnd get to run before the object is torn down.
    /// </summary>
    public void DestroyCutscene()
    {
        Destroy(instanceToDestroy ? instanceToDestroy : gameObject);
    }

    // The GameObject to destroy when the cutscene ends (set in ExecuteSequence, defaults to this GameObject).
    private GameObject instanceToDestroy;

}