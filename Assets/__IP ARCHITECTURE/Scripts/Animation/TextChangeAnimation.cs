using System.Collections;
using UnityEngine;

/// <summary>
/// Plays a "the value is changing" animation on a text element while its displayed value
/// is being updated (e.g. a score counting up, or a suit count ticking).
///
/// Call <see cref="Play"/> when the change starts and <see cref="Stop"/> when it settles.
/// The actual visual is delegated to an <see cref="AnimationControllerBase"/> (a group of
/// AnimationBase components such as Shake / Scale) placed on this object or its children.
/// </summary>
public class TextChangeAnimation : MonoBehaviour
{
    [Tooltip("Animation group played while the text is changing. Auto-found on this object / its children if left empty.")]
    [SerializeField] private AnimationControllerBase animation;

    private Coroutine _loopRoutine;
    private bool _playing;

    /// <summary>True while the change animation is running.</summary>
    public bool IsPlaying => _playing;

    private void Awake()
    {
        if (animation == null)
            animation = GetComponentInChildren<AnimationControllerBase>();
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
}
