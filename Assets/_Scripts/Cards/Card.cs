using System.Collections;
using System.Collections.Generic;
using System.Threading;
using PrimeTween;
using TMPro;
using UnityEngine;

/// <summary>
/// A single card living in 3D world space. The visual is two quads parented under
/// this root: one for the FRONT, one for the BACK, placed back-to-back.
///
/// Spatial convention (matches the project): the table is the XZ plane, Y is up.
///
/// Facing convention: the card root's local +Z axis is the FRONT normal.
///   - Front quad: visible side points along the root's +Z.
///   - Back quad:  visible side points along the root's -Z.
/// Because the quads are single-sided and back-to-back, whichever side faces the
/// viewer is shown automatically by the geometry — flipping a card just means
/// rotating it 180 degrees. (If you would rather stack both quads at the same
/// spot and toggle them on/off, enable <see cref="useFaceToggling"/>.)
///
/// While the card is InHand it eases toward the "home pose" that HandManager
/// assigns it. While Dragging or OnTable the home pose is ignored and the card is
/// positioned explicitly (by CardDragController / a one-shot tween).
/// </summary>
public class Card : MonoBehaviour
{
    public CardDataBase cardDataBase;
    [HideInInspector] public int countdown=0;
    
    public enum CardState { InHand, Dragging, OnTable }

    [Header("Info texts")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private TMP_Text countDownText;
    
    
    [Header("Faces (two quads)")]
    [Tooltip("Quad shown as the front. Its visible side must point along the card root's +Z.")]
    [SerializeField] private GameObject frontFace;
    [Tooltip("Quad shown as the back. Its visible side must point along the card root's -Z.")]
    [SerializeField] private GameObject backFace;

    [SerializeField] private Transform dataParent;

    [Header("Animations")]
    public AnimationControllerBase activateAnimController;

    [Header("Picking")]
    [Tooltip("Collider used for mouse picking. Leave empty to auto-find one in the children " +
             "(so you can put the collider on the front quad instead of the root).")]
    [SerializeField] private Collider pickCollider;
    [Tooltip("If ON, SetFaceUp enables/disables the two quads instead of relying on back-to-back geometry. " +
             "Use this only if both quads are stacked at the same position facing the same way.")]
    [SerializeField] private bool useFaceToggling = false;

    [Header("Facing")]
    [Tooltip("Rotation correction applied to the card's front so it faces the right way. " +
             "Set Y = 180 if your front quad faces the card's -Z (Unity's default Quad " +
             "orientation) and you see the back. Leave at 0 if the front already faces +Z. " +
             "Used by the hand, dragging and table placement so they all stay consistent.")]
    [SerializeField] private Vector3 faceRotationOffset = new Vector3(0f, 180f, 0f);

    [Header("State (runtime)")]
    public CardState state = CardState.InHand;
    [SerializeField] private bool faceUp = true;

    [Header("Hand follow")]
    [Tooltip("Seconds-ish smoothing constant for easing toward the home pose. Smaller = snappier.")]
    [SerializeField] private float followSmoothing = 0.12f;

    [Header("Hover (while in hand)")]
    [Tooltip("How far the card lifts (along its own up axis) when hovered.")]
    [SerializeField] private float hoverLift = 0.35f;
    [SerializeField] private float hoverScaleMultiplier = 1.1f;
    [SerializeField] private float hoverTweenDuration = 0.12f;

    [Header("Placement tween (drag release / dealing)")]
    [SerializeField] private float placeDuration = 0.2f;
    [SerializeField] private Ease placeEase = Ease.OutCubic;

    [Header("Burn animation")]
    [Tooltip("Renderers whose MasterMat3d material is dissolved during the burn. Auto-filled " +
             "with every renderer in the children when left empty.")]
    [SerializeField] private Renderer[] burnRenderers;
    [Tooltip("Shader property that toggles the dissolve feature on (MasterMat3d: '_Disslove').")]
    [SerializeField] private string dissolveEnableProperty = "_Disslove";
    [Tooltip("Shader property carrying the 0->1 dissolve amount (MasterMat3d: '_Dissolve_amount').")]
    [SerializeField] private string dissolveAmountProperty = "_Dissolve_amount";
    [Tooltip("Angle (around the card's local Y) of the burn flip. 180 = show the back.")]
    [SerializeField] private float burnFlipYAngle = 180f;
    [Tooltip("How long the flip takes.")]
    [SerializeField] private float burnFlipDuration = 0.35f;
    [SerializeField] private Ease burnFlipEase = Ease.InOutCubic;
    [Tooltip("How high (world up) the card hops during the flip: it rises to this height as the " +
             "flip reaches 90°, then comes back down by the time it finishes at 180°. 0 = no hop.")]
    [SerializeField] private float burnFlipHopHeight = 0.5f;
    [Tooltip("Pause after the flip (and after front/data are hidden) before the dissolve starts.")]
    [SerializeField] private float burnDissolveDelay = 0.05f;
    [Tooltip("How long the dissolve (amount 0 -> 1) takes.")]
    [SerializeField] private float burnDissolveDuration = 0.8f;
    [SerializeField] private Ease burnDissolveEase = Ease.InQuad;
    [Tooltip("Extra delay after the dissolve completes before the card is destroyed.")]
    [SerializeField] private float burnDestroyDelay = 0f;

    private bool burning;
    // Cached material instances + property id, resolved when the burn starts so the tween
    // writes straight to the materials instead of re-fetching them every frame.
    private Material[] burnMats;
    private int burnAmountId;

    // Home pose assigned by HandManager, eased toward while InHand.
    private Vector3 homePosition;
    private Quaternion homeRotation;
    private bool hasHome;

    private Vector3 baseScale;
    private bool hovered;
    private Tween scaleTween;
    private Tween placeTween;

    /// <summary>The collider used for mouse picking. May live on the root or on the front quad.</summary>
    public Collider Col { get; private set; }
    public bool FaceUp => faceUp;

    /// <summary>When true the card is pinned in place and can no longer be picked up / dragged.</summary>
    public bool Locked { get; private set; }

    /// <summary>Back-reference to the hand that owns this card (set by PlayerManager).</summary>
    [HideInInspector] public HandManager handManager;

    private void Awake()
    {
        // Prefer an explicitly assigned collider (e.g. one on the front quad),
        // otherwise grab the first collider found on the root or in the children.
        Col = pickCollider ? pickCollider : GetComponentInChildren<Collider>(true);
        if (!Col) h.Out($"Card '{name}' has no collider assigned or in its children; it cannot be picked.");

        baseScale = transform.localScale;
        if (burnRenderers == null || burnRenderers.Length == 0)
            burnRenderers = GetComponentsInChildren<Renderer>(true);
        ApplyFace();
    }

    public void Start()
    {
        if (state == CardState.InHand)
        {
            HandManager.Instance.AddCard(this);
        }
        
        if (!cardDataBase) return;
        cardDataBase = Instantiate(cardDataBase);

        titleText.text = cardDataBase.GenerateTitle();
        descriptionText.text = cardDataBase.GenerateDescription();
        countDownText.text = cardDataBase.countdown.ToString();

        // Play the activate animation. AnimationControllerBase already gathered, in its Awake,
        // every AnimationBase under it whose type matches this controller's type (targetTypes),
        // so PlayAnimations() runs exactly those matching animation bases.
        if (activateAnimController) StartCoroutine(activateAnimController.PlayAnimations());
    }
    
    

    private void Update()
    {
        // The hand is the only state that self-drives its transform.
        if (state != CardState.InHand || !hasHome) return;

        Vector3 targetPos = homePosition;
        if (hovered)
            targetPos += (homeRotation * Vector3.up) * hoverLift;

        // Frame-rate independent exponential smoothing toward the home pose.
        float t = 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(0.0001f, followSmoothing));
        transform.position = Vector3.Lerp(transform.position, targetPos, t);
        transform.rotation = Quaternion.Slerp(transform.rotation, homeRotation, t);
    }

    /// <summary>
    /// Builds a rotation that aims the card's FRONT toward <paramref name="forward"/>, using
    /// <paramref name="up"/> as the roll hint, then applies <c>faceRotationOffset</c>. Every
    /// system (hand, dragging, table) faces the card through this so a single offset fixes
    /// how the quads were authored (e.g. Y = 180 when the front quad faces the card's -Z).
    /// </summary>
    public Quaternion Face(Vector3 forward, Vector3 up)
    {
        if (forward.sqrMagnitude < 1e-6f) forward = Vector3.forward;
        return Quaternion.LookRotation(forward.normalized, up) * Quaternion.Euler(faceRotationOffset);
    }

    /// <summary>Assigns the pose the card should rest at while it is in the hand.</summary>
    public void SetHomePose(Vector3 position, Quaternion rotation, bool instant = false)
    {
        homePosition = position;
        homeRotation = rotation;
        hasHome = true;

        if (instant)
        {
            transform.SetPositionAndRotation(position, rotation);
        }
    }

    public void SetState(CardState newState) => state = newState;

    /// <summary>Face up = front quad toward the viewer. Flip via 180-degree geometry (or toggling).</summary>
    public void SetFaceUp(bool value)
    {
        faceUp = value;
        ApplyFace();
    }

    private void ApplyFace()
    {
        if (!useFaceToggling) return;      // Geometry handles it; nothing to toggle.
        if (frontFace) frontFace.SetActive(faceUp);
        if (backFace) backFace.SetActive(!faceUp);
    }

    /// <summary>Positions the card directly (used by the drag controller every frame).</summary>
    public void SetPoseImmediate(Vector3 position, Quaternion rotation)
    {
        transform.SetPositionAndRotation(position, rotation);
    }

    /// <summary>One-shot eased move to a resting pose (e.g. landing on the table or being dealt).</summary>
    public void AnimateTo(Vector3 position, Quaternion rotation)
    {
        if (placeTween.isAlive) placeTween.Stop();
        Tween.Position(transform, position, placeDuration, placeEase);
        placeTween = Tween.Rotation(transform, rotation, placeDuration, placeEase);
    }

    public void SetHovered(bool value)
    {
        if (hovered == value) return;
        hovered = value;

        if (scaleTween.isAlive) scaleTween.Stop();
        Vector3 target = hovered ? baseScale * hoverScaleMultiplier : baseScale;
        scaleTween = Tween.Scale(transform, target, hoverTweenDuration, Ease.OutQuad);
    }

    /// <summary>Pins the card so it can no longer be picked up or dragged (see CardDragController).</summary>
    public void Lock() => Locked = true;

    /// <summary>Releases a previously locked card so it can be dragged again.</summary>
    public void Unlock() => Locked = false;

    /// <summary>
    /// Called by <see cref="PlacingArea"/> right after the card is placed on its surface.
    /// Empty by design — override / extend this to trigger the card's "on play" behaviour.
    /// </summary>
    public virtual void OnPlace()
    {
        SFXManager.Instance.PlayRandomClip(new List<AudioClip>()
        {
            R.PROJECT.Audio.Cards.dropCard,
        });
        
        StartCoroutine(OnPlaceCoroutine());
    }

    public IEnumerator OnPlaceCoroutine()
    {
        yield return new WaitForSeconds(0.67f);
        ActivateCard();
    }

    public virtual void ActivateCard()
    {
        StartCoroutine(ActivateCardCoroutine());
    }

    public IEnumerator ActivateCardCoroutine()
    {
        yield return null;
        if (!cardDataBase) yield break;
        
        SFXManager.Instance.PlayRandomClip(new List<AudioClip>()
        {
            R.PROJECT.Audio.Cards.Activate.activate1,
            R.PROJECT.Audio.Cards.Activate.activate2,
            R.PROJECT.Audio.Cards.Activate.activate3,
        });
        StartCoroutine(activateAnimController.PlayAnimations());
        
        TableManager.Instance.AddSuits(cardDataBase.suits);

        int vp = cardDataBase.GenerateVP();
        TableManager.Instance.AddScore(vp);
        TextAlertManager.Instance.CreateDamageAlert(vp, transform);

        if (countdown <= 0)
        {
            StartCoroutine(DestroyCoroutine());
        }
    }

    public IEnumerator DestroyCoroutine()
    {
        yield return 0;
        yield return new WaitForSeconds(1f);
        yield return Burn();
    }

    /// <summary>
    /// Plays the burn animation and destroys the card, as a coroutine you can drive yourself
    /// (yield on it, interleave your own steps, or call the phase helpers below in your own order):
    ///   1. <see cref="BurnFlip"/> — flip 180° around local Y while hopping up in world space,
    ///      then hide the data parent and the front mesh,
    ///   2. <see cref="BurnDissolve"/> — drive the MasterMat3d dissolve 0 -> 1,
    ///   3. destroy the GameObject.
    /// All timings/eases are the "Burn animation" inspector fields. Idempotent: a second call
    /// while already burning is ignored. <see cref="OnBurnStarted"/> fires the instant burning
    /// begins — that's where the burn SFX is played. <paramref name="onComplete"/> (optional)
    /// runs just before the card is destroyed.
    /// </summary>
    public IEnumerator Burn(System.Action onComplete = null)
    {
        if (burning) yield break;
        burning = true;

        // The card is leaving play: stop it self-driving toward the hand pose, kill any
        // in-flight tweens and make sure it can no longer be picked up or dragged.
        state = CardState.OnTable;
        Lock();
        if (scaleTween.isAlive) scaleTween.Stop();
        if (placeTween.isAlive) placeTween.Stop();
        if (handManager) handManager.RemoveCard(this);

        PrepareBurnMaterials();

        // The card has just started to burn — play sounds / spawn VFX here.
        OnBurnStarted();

        // 1. flip + hop, then hide the front + data.
        yield return BurnFlip();

        // optional gap between the flip and the dissolve.
        if (burnDissolveDelay > 0f) yield return new WaitForSeconds(burnDissolveDelay);

        // 2. dissolve away.
        yield return BurnDissolve();

        if (burnDestroyDelay > 0f) yield return new WaitForSeconds(burnDestroyDelay);

        // 3. destroy.
        onComplete?.Invoke();
        Destroy(gameObject);
    }

    /// <summary>
    /// Called the moment <see cref="Burn"/> begins (materials ready, before any animation).
    /// Plays the burn SFX; override to change / extend what happens when the card starts burning.
    /// </summary>
    protected virtual void OnBurnStarted()
    {
        
    }

    /// <summary>
    /// Grabs per-renderer material instances (via <c>.material</c>, never touching the asset on
    /// disk), enables the dissolve feature and pins the amount at 0 so the flip shows the intact
    /// card before it burns away. Call once before <see cref="BurnDissolve"/>.
    /// </summary>
    public void PrepareBurnMaterials()
    {
        int enableId = Shader.PropertyToID(dissolveEnableProperty);
        burnAmountId = Shader.PropertyToID(dissolveAmountProperty);
        var mats = new System.Collections.Generic.List<Material>();
        if (burnRenderers != null)
            foreach (var r in burnRenderers)
            {
                if (!r) continue;
                var m = r.material;
                if (!m) continue;
                m.SetFloat(enableId, 1f);
                m.SetFloat(burnAmountId, 0f);
                mats.Add(m);
            }
        burnMats = mats.ToArray();
    }

    /// <summary>
    /// Phase 1: flips the card 180° around its local Y while hopping up in WORLD space (the card
    /// lies flat, so its local up is not world up — the hop must use world Y). sin(pi*t) rises to
    /// <see cref="burnFlipHopHeight"/> at the flip's 90° midpoint and returns to 0 by 180°. When
    /// the flip finishes it hides the data parent and the front mesh.
    /// </summary>
    public IEnumerator BurnFlip()
    {
        Quaternion flipTarget = transform.localRotation * Quaternion.Euler(0f, burnFlipYAngle, 0f);
        float flipBaseWorldY = transform.position.y;
        
        SFXManager.Instance.PlayRandomClip(new List<AudioClip>()
        {
            R.PROJECT.Audio.Cards.TakeCard.takeCard4
        });
        
        yield return Sequence.Create(Tween.LocalRotation(transform, flipTarget, burnFlipDuration, burnFlipEase))
            .Group(Tween.Custom(0f, 1f, burnFlipDuration, t =>
            {
                Vector3 p = transform.position;
                p.y = flipBaseWorldY + burnFlipHopHeight * Mathf.Sin(Mathf.PI * t);
                transform.position = p;
            }))
            .ToYieldInstruction();

        if (dataParent) dataParent.gameObject.SetActive(false);
        if (frontFace) frontFace.SetActive(false);
    }

    /// <summary>
    /// Phase 2: drives the MasterMat3d dissolve amount 0 -> 1, written straight to each cached
    /// material. Call <see cref="PrepareBurnMaterials"/> first.
    /// </summary>
    public IEnumerator BurnDissolve()
    {
        SFXManager.Instance.PlayRandomClip(new List<AudioClip>()
        {
            R.PROJECT.Audio.Fire.burn1,
            R.PROJECT.Audio.Fire.burn2,
        });
        yield return Tween.Custom(0f, 1f, burnDissolveDuration, SetBurnAmount, burnDissolveEase)
            .ToYieldInstruction();
    }

    private void SetBurnAmount(float value)
    {
        if (burnMats == null) return;
        foreach (var m in burnMats)
            if (m) m.SetFloat(burnAmountId, value);
    }

    /// <summary>
    /// Called by <see cref="CardManager.OnTurnEnd"/> for every card on the tracked tables,
    /// staggered by CardManager's delay between turn effects. Empty by design — override /
    /// extend to trigger the card's end-of-turn behaviour.
    /// </summary>
    public virtual void OnTurnEnd()
    {

    }

    public void OnTurnStart()
    {
        countdown--;
        SFXManager.Instance.PlayRandomClip(new List<AudioClip>()
        {
            R.PROJECT.Audio.Clock.Tick.clockTick1,
            R.PROJECT.Audio.Clock.Tick.clockTick2,
            R.PROJECT.Audio.Clock.Tick.clockTick3,
            R.PROJECT.Audio.Clock.Tick.clockTick4
        });
        countDownText.text = countdown.ToString();
        // TODO squish anim
        if (countdown <= 0f)
        {
            StartCoroutine(DestroyCoroutine());
        }
        
        

        
    }
}
