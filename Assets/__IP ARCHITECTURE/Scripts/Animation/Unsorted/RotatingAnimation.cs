using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PrimeTween;

public class RotatingAnimation : AnimationBase
{
    [Header("Rotating Settings")]
    public List<float> angleFrames = new();
    public float defaultGap = 0.2f;
    public List<float> gapsBetweenFrames = new();
    public bool randomPhase;
    public Ease ease = Ease.Default;
    [Tooltip("If enabled, the object is rotated to this Z angle before the animation starts.")]
    public bool useStartRotation;
    public float startRotation;
    [Tooltip("Z angle the animation returns to. When customInitialState is off, this is captured from the current rotation on Awake.")]
    public float initialState;

    private bool _phaseConsumed;
    private bool _comingFromLoop;
    // The rotation tween currently driving the transform. Tracked so it can be stopped
    // on return/stop — otherwise it keeps writing localEulerAngles after the coroutine
    // is stopped and the object sticks on the last frame.
    private Tween _rotationTween;
    

    public override void Awake()
    {
        if (useStartRotation)
            transform.localEulerAngles = new Vector3(0f, 0f, startRotation);
        if (ShouldCaptureInitialState())
            initialState = transform.localEulerAngles.z;
    }

    public override IEnumerator Play()
    {
        if (!_comingFromLoop)
            _phaseConsumed = false;
        _comingFromLoop = false;
        yield return base.Play();
    }

    public override void ReturnToInitialState()
    {
        if (_rotationTween.isAlive) _rotationTween.Stop();
        transform.localEulerAngles = new Vector3(0f, 0f, initialState);
    }

    // Eases the rotation back to the initial angle over returningTime using returningEasing.
    public override IEnumerator ReturnToInitialStateAnimated()
    {
        if (_rotationTween.isAlive) _rotationTween.Stop();

        float fromAngle = transform.localEulerAngles.z;
        float toAngle = fromAngle + Mathf.DeltaAngle(fromAngle, initialState);

        if (returningTime <= 0f)
        {
            ReturnToInitialState();
            yield break;
        }

        _rotationTween = Tween.Custom(fromAngle, toAngle, returningTime,
            val => transform.localEulerAngles = new Vector3(0f, 0f, val), returningEasing);
        while (_rotationTween.isAlive) yield return null;

        transform.localEulerAngles = new Vector3(0f, 0f, initialState);
    }

    public override IEnumerator AnimationCoroutine()
    {
        if (_rotationTween.isAlive) _rotationTween.Stop();

        if (angleFrames.Count == 0)
        {
            _comingFromLoop = true;
            yield return base.AnimationCoroutine();
            yield break;
        }

        bool applyPhase = !_phaseConsumed && randomPhase && loop;
        float phase = applyPhase ? h.Range(0f, 1f) : 0f;
        _phaseConsumed = true;

        for (int i = 0; i < angleFrames.Count; i++)
        {
            float targetAngle = angleFrames[i];
            float gap = i < gapsBetweenFrames.Count ? gapsBetweenFrames[i] : defaultGap;
            float fromAngle = transform.localEulerAngles.z;
            float duration = gap;

            if (i == 0 && applyPhase)
            {
                fromAngle += Mathf.DeltaAngle(fromAngle, targetAngle) * phase;
                duration = gap * (1f - phase);
                transform.localEulerAngles = new Vector3(0f, 0f, fromAngle);
            }

            if (duration > 0f)
            {
                float tweenTo = fromAngle + Mathf.DeltaAngle(fromAngle, targetAngle);
                _rotationTween = Tween.Custom(fromAngle, tweenTo, duration,
                    val => transform.localEulerAngles = new Vector3(0f, 0f, val), ease);
                while (_rotationTween.isAlive) yield return null;
            }

            transform.localEulerAngles = new Vector3(0f, 0f, targetAngle);
        }

        _comingFromLoop = true;
        yield return base.AnimationCoroutine();
    }
}
