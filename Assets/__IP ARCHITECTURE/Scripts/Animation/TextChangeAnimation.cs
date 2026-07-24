using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Two jobs on one component:
///
/// 1. <b>Change pulse</b> — plays a "the value is changing" animation on a text element while
///    its displayed value is being updated (e.g. a score counting up, or a suit count ticking).
///    Call <see cref="Play"/> when the change starts and <see cref="Stop"/> when it settles.
///    The visual is delegated to an <see cref="AnimationControllerBase"/> (a group of
///    AnimationBase components such as Shake / Scale) placed on this object or its children.
///
/// 2. <b>Frame animation</b> — cycles the target <see cref="TMP_Text"/>'s string through a list
///    of text "frames" on a timer, giving a flip-book effect (used to animate the two-frame suit
///    sprites on cards). Frames are usually assigned at runtime via <see cref="SetFrames"/>, but
///    can also be authored in the inspector. When <see cref="loop"/> is on it runs forever until
///    <see cref="StopFrames"/>; otherwise it plays each frame once and settles on the last.
/// </summary>
public class TextChangeAnimation : MonoBehaviour
{
    [Tooltip("Animation group played while the text is changing. Auto-found on this object / its children if left empty.")]
    [SerializeField] private AnimationControllerBase animation;

    [Header("Frame animation")]
    [Tooltip("Text element whose string is swapped between frames. Auto-found on this object / its children if left empty.")]
    [SerializeField] private TMP_Text targetText;
    [Tooltip("The text frames to cycle through. Usually assigned at runtime via SetFrames().")]
    [SerializeField] private List<string> frames = new List<string>();
    [Tooltip("If ON, the frames loop forever until StopFrames(); if OFF, they play through once and settle on the last frame.")]
    [SerializeField] private bool loop = true;
    [Tooltip("Seconds each frame stays on screen before advancing to the next.")]
    [SerializeField] private float frameInterval = 0.4f;

    private Coroutine _loopRoutine;
    private bool _playing;

    private Coroutine _frameRoutine;
    private bool _framesPlaying;

    /// <summary>True while the change-pulse animation is running.</summary>
    public bool IsPlaying => _playing;

    /// <summary>True while the frame animation is cycling.</summary>
    public bool IsFramePlaying => _framesPlaying;

    private void Awake()
    {
        if (animation == null)
            animation = GetComponentInChildren<AnimationControllerBase>();
        if (targetText == null)
            targetText = GetComponentInChildren<TMP_Text>();
    }

    /// <summary>
    /// Start the change animation. It keeps looping until <see cref="Stop"/> is called,
    /// so it stays active for the whole duration of the value change regardless of the
    /// group's own loop setting. Safe to call repeatedly (no-op while already playing).
    /// </summary>
    public void Play()
    {
        if (animation == null || _playing) return;

        _playing = true;
        _loopRoutine = StartCoroutine(LoopRoutine());
    }

    /// <summary>Play the change animation a single time (no looping, no need to call Stop).</summary>
    public void PlayOnce()
    {
        if (animation == null) return;

        StartCoroutine(animation.PlayAnimations());
    }

    /// <summary>Stop the change animation and let the target return to its resting state.</summary>
    public void Stop()
    {
        if (!_playing) return;

        _playing = false;

        if (_loopRoutine != null)
        {
            StopCoroutine(_loopRoutine);
            _loopRoutine = null;
        }

        if (animation != null)
            animation.StopAnimations();
    }

    private IEnumerator LoopRoutine()
    {
        while (_playing)
        {
            yield return animation.PlayAnimations();
            yield return null; // guarantee at least one frame per cycle (avoids a hang on empty groups)
        }
    }

    // ---- Frame animation (flip-book of text frames) --------------------------------------------

    /// <summary>
    /// Replaces the frame list and (by default) restarts the frame animation. Pass a single-element
    /// list for a static text, or two+ elements for an animated flip-book (e.g. the two suit-sprite
    /// frames on a card). <paramref name="autoStart"/> = false only stores the frames.
    /// </summary>
    public void SetFrames(List<string> newFrames, bool autoStart = true)
    {
        frames = newFrames ?? new List<string>();

        if (autoStart)
            PlayFrames();
        else if (frames.Count > 0)
            ApplyFrame(0); // show the first frame immediately so the text isn't left blank
    }

    /// <summary>
    /// Starts cycling the target text through <see cref="frames"/>. Honours <see cref="loop"/>:
    /// loops forever when on, plays once and settles on the last frame when off. Safe to call
    /// repeatedly — it restarts from the first frame.
    /// </summary>
    public void PlayFrames()
    {
        if (targetText == null)
            targetText = GetComponentInChildren<TMP_Text>();
        if (targetText == null || frames == null || frames.Count == 0) return;

        StopFrames();

        // A single frame (or a zero interval) needs no coroutine — just show it.
        if (frames.Count == 1)
        {
            ApplyFrame(0);
            return;
        }

        _framesPlaying = true;
        _frameRoutine = StartCoroutine(FrameRoutine());
    }

    /// <summary>Stops the frame animation, leaving the current frame on screen.</summary>
    public void StopFrames()
    {
        _framesPlaying = false;

        if (_frameRoutine != null)
        {
            StopCoroutine(_frameRoutine);
            _frameRoutine = null;
        }
    }

    private IEnumerator FrameRoutine()
    {
        int i = 0;
        while (_framesPlaying)
        {
            ApplyFrame(i);
            yield return frameInterval > 0f
                ? new WaitForSeconds(frameInterval)
                : null;

            i++;
            if (i >= frames.Count)
            {
                if (!loop)
                {
                    _framesPlaying = false;
                    _frameRoutine = null;
                    yield break; // settle on the last shown frame
                }
                i = 0;
            }
        }
    }

    private void ApplyFrame(int index)
    {
        if (targetText != null && frames != null && index >= 0 && index < frames.Count)
            targetText.text = frames[index];
    }
}
