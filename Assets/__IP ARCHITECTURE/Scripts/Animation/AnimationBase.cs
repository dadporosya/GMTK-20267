using System.Collections;
using PrimeTween;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Events;

public class AnimationBase : MonoBehaviour
{
    [SerializeField] private bool disable=false;
    public AnimationPreferences.Type type;
    public bool loop = false;
    public bool playOnStart = false;
    [Tooltip("Easing used when the animation returns to its initial state, both at its natural end and when stopped (see ReturnToInitialStateAnimated).")]
    public Ease returningEasing = Ease.Default;
    [Tooltip("Duration of the return-to-initial transition. 0 = snap instantly.")]
    public float returningTime = 0.3f;
    [Tooltip("If true, use the initialState value set in the inspector. If false, the current state is captured as the initial state on Awake.")]
    public bool customInitialState = false;
    [Tooltip("If true, the object eases/snaps back to its initial state when the animation ends or is stopped. If false, it stays at its final animated state.")]
    public bool returnToTheInittialState = true;

    // True once the initial state has been assigned — either from the inspector
    // (customInitialState) or captured once at runtime — so it is never overwritten again.
    [System.NonSerialized] public bool initialStateAssigned;

    private Coroutine animationCoroutine;
    private Coroutine returnCoroutine;

    // Tracks whether Unity's Start() has already run for this component. OnEnable fires
    // before the first Start (and again on every re-enable), so we use this to only kick
    // off the playOnStart animation from OnEnable AFTER the initial Start, avoiding a
    // double-play on first activation while still restarting on subsequent re-enables.
    [System.NonSerialized] private bool hasStarted;
    [Tooltip("Invoked when the animation reaches its natural (non-looping) end.")]
    public UnityEvent onAnimationEnd = new UnityEvent();

    [SerializeField] private float animationSpeed=1f;
    
    public virtual void Awake() { }

    // Subclasses call this in Awake before capturing their initial state. It returns true
    // only the first time and only when the state was not assigned in the inspector, so an
    // already assigned/captured initial state is never overwritten.
    protected bool ShouldCaptureInitialState()
    {
        if (customInitialState || initialStateAssigned) return false;
        initialStateAssigned = true;
        return true;
    }

    public virtual void Start()
    {
        hasStarted = true;
        if (playOnStart) StartCoroutine(Play());
    }

    // Restart the playOnStart animation whenever the object is re-enabled. Disabling a
    // GameObject stops its running coroutines and Unity does NOT call Start() again on
    // re-enable, so looping idle animations would otherwise never resume. We skip the
    // first OnEnable (before Start) because Start() itself handles the initial play.
    public virtual void OnEnable()
    {
        if (hasStarted && playOnStart) StartCoroutine(Play());
    }

    public virtual void PlayInstantly()
    {
        StartCoroutine(Play());
    }
    public virtual IEnumerator Play()
    {
        if (disable) yield break;

        if (returnCoroutine != null) { StopCoroutine(returnCoroutine); returnCoroutine = null; }
        if (animationCoroutine != null) StopCoroutine(animationCoroutine);
        animationCoroutine = StartCoroutine(AnimationCoroutine());
        yield return animationCoroutine;
    }

    public virtual void Stop()
    {
        if (animationCoroutine != null) StopCoroutine(animationCoroutine);
        if (returnCoroutine != null) StopCoroutine(returnCoroutine);
        returnCoroutine = StartCoroutine(StopRoutine());
    }

    // Smoothly returns to the initial state when the animation is stopped, using the
    // subclass's eased ReturnToInitialStateAnimated (which honours returningEasing).
    // The final ReturnToInitialState guarantees the exact state and also serves as the
    // instant fallback for animations that don't implement an animated return.
    private IEnumerator StopRoutine()
    {
        if (returnToTheInittialState)
        {
            yield return ReturnToInitialStateAnimated();
            ReturnToInitialState();
        }
        returnCoroutine = null;
    }

    public virtual void ReturnToInitialState() { }

    // Re-syncs any internal "current offset / pose" bookkeeping to the object's resting
    // transform WITHOUT moving it. Call this after externally snapping the transform back
    // to its initial state (e.g. TalkableWithModels.ResetTransform) so the next Play()
    // starts from the correct place. Without this, an animation with
    // returnToTheInittialState = false leaves stale offset state and its next play becomes
    // a no-op (from == target).
    public virtual void SyncToRestingState() { }

    // Animated counterpart of ReturnToInitialState, played when the animation reaches its
    // natural (non-looping) end and when it is stopped, so the object eases back to its
    // resting pose instead of snapping. Subclasses (Floating, Rotating) override this to
    // ease over their own default duration/gap using returningEasing. Base default is a
    // no-op; Stop() then falls back to an instant ReturnToInitialState for such animations.
    public virtual IEnumerator ReturnToInitialStateAnimated()
    {
        yield break;
    }

    // Evaluates returningEasing at t. Easing.Evaluate can't resolve Ease.Default (a placeholder
    // for "the configured default", not a real curve) or Ease.Custom, so map those: Default falls
    // back to a concrete eased curve (OutQuad) so the return still eases, and Custom stays linear.
    protected float EvaluateReturningEasing(float t)
    {
        Ease ease = returningEasing;
        if (ease == Ease.Custom) return t;
        if (ease == Ease.Default) ease = Ease.OutQuad;
        return Easing.Evaluate(t, ease);
    }

    public virtual IEnumerator AnimationCoroutine()
    {
        // it suppoused to be in the end of Animation
        if (disable) yield break;

        yield return null;
        if (loop)
        {
            StartCoroutine(Play());
        }
        else
        {
            if (returnToTheInittialState) yield return ReturnToInitialStateAnimated();
            onAnimationEnd?.Invoke();
        }
        yield return null;
    }
}