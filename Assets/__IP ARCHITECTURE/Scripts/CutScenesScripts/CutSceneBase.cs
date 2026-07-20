using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;


public class CutSceneBase : MonoBehaviour
{
    public List<IEnumerator> cutsceneSteps = new List<IEnumerator>();
    [SerializeField] private bool runOnStart = false;

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
        
        foreach (IEnumerator step in cutsceneSteps)
        {
            // h.Out(step);
            yield return StartCoroutine(step);
        }

        h.Out("Cutscene complete.");
    
        if (instanceToDestroy) Destroy(instanceToDestroy);
    }

}