using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AnimationControllerBase : MonoBehaviour
{
    public AnimationPreferences.Type type;
    [SerializeField] private bool filterByTypes = true;
    public List<AnimationPreferences.Type> targetTypes = new List<AnimationPreferences.Type>();
    public List<AnimationBase> animations = new List<AnimationBase>();
    
    [SerializeField] private bool searchForAnimationInChildren = true;
    [SerializeField] private bool searchForAnimationInAllChildren = true;
    [SerializeField] private bool loop;
    [SerializeField] private bool playOnStart;

    public virtual void Awake()
    {
        if (!targetTypes.Contains(type))  targetTypes.Add(type);
        targetTypes.RemoveAll(t => t == AnimationPreferences.Type.None);
        if (targetTypes.Count == 0) filterByTypes = false;
        
        
        
        void AddAnimations(Transform t)
        {
            foreach (AnimationBase anim in t.GetComponents<AnimationBase>())
            {
                if (filterByTypes && !targetTypes.Contains(anim.type)) continue;
                anim.loop = loop;
                if (!animations.Contains(anim)) animations.Add(anim);
            }
        }

        AddAnimations(transform);

        if (searchForAnimationInChildren)
        {
            void AddAnimationsFromChildren(Transform t)
            {
                foreach (Transform child in t)
                {
                    AddAnimations(child);
                    if (searchForAnimationInAllChildren) AddAnimationsFromChildren(child);
                }
            }

            AddAnimationsFromChildren(transform);
        }
    }

    public virtual void Start()
    {
        if (playOnStart) StartCoroutine(PlayAnimations());
    }

    public IEnumerator PlayAnimations(Action onAnimationEnd = null)
    {
        if (animations.Count == 0) { onAnimationEnd?.Invoke(); yield break; }

        int remaining = animations.Count;
        foreach (var animation in animations)
            StartCoroutine(TrackCoroutine(animation.Play(), () => remaining--));

        yield return new WaitUntil(() => remaining == 0);
        onAnimationEnd?.Invoke();
    }

    private IEnumerator TrackCoroutine(IEnumerator routine, Action onDone)
    {
        yield return routine;
        onDone();
    }

    public void StopAnimations()
    {
        foreach (var animation in animations)
            animation.Stop();
    }
}