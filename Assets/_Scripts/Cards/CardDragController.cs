using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Handles mouse picking and dragging of 3D cards using the new Input System.
///
/// Flow:
///   - Hover a card in the hand  -> it lifts (Card.SetHovered).
///   - Press on a card           -> begins dragging it (InHand or OnTable cards).
///   - While held (off a surface) -> the card free-floats at a FIXED distance in front
///                                  of the camera along the cursor ray, so it keeps a
///                                  constant size (no scaling into the camera, no horizon
///                                  stretch) and always follows the cursor.
///   - Cursor over a drop surface -> the card magnetizes down onto it (see <see cref="dragLift"/>).
///   - Release over a surface     -> the card lands flat / is placed (and leaves the hand).
///   - Release off any surface    -> the card snaps back where it came from.
///
/// A "drop surface" is any collider on <see cref="tableMask"/>; a release only counts as a
/// valid placement when an actual surface collider is under the pointer. Movement is eased
/// (<see cref="dragFollowSmoothing"/>) for an OutQuad-like glide.
/// </summary>
public class CardDragController : MonoBehaviour
{
    public static CardDragController Instance;

    [Header("References")]
    [SerializeField] private Camera cam;

    [Header("Raycast layers")]
    [Tooltip("Layer(s) the card colliders live on. Used for picking/hovering cards.")]
    [SerializeField] private LayerMask cardMask = ~0;
    [Tooltip("Layer(s) of the table surface collider. Defines where a card may be dropped.")]
    [SerializeField] private LayerMask tableMask = ~0;

    [Header("Drag feel")]
    [Tooltip("Fixed distance in front of the camera the card floats at while being dragged. " +
             "Keeping the depth constant keeps the card a constant size — it never scales up into " +
             "the camera and there is no horizon stretch. 0 = use the distance it had when picked up.")]
    [SerializeField] private float dragDistance = 0f;
    [Tooltip("How high above a drop surface the card floats once it magnetizes onto it.")]
    [SerializeField] private float dragLift = 0.15f;
    [Tooltip("How far above the table the card sits once it's actually placed. A small lift " +
             "keeps the card from being coplanar with the surface, avoiding z-fighting.")]
    [SerializeField] private float liftingOverTable = 0.001f;
    [Tooltip("Follow smoothing while dragging (seconds-ish). Smaller = snappier, larger = floatier. " +
             "Produces an ease-out (OutQuad-like) glide toward the target. 0 = instant.")]
    [SerializeField] private float dragFollowSmoothing = 0.08f;
    [Tooltip("Cards land face up on the table when true.")]
    [SerializeField] private bool placeFaceUpOnTable = true;
    [SerializeField] private float maxRayDistance = 1000f;

    private Card dragging;
    private Card.CardState dragOriginState;
    private Card hovered;

    private Card hoverHandCard;      // another hand card under the pointer this frame (swap target)
    private bool hasTablePoint;      // pointer is over an actual drop collider this frame
    private Vector3 lastTablePoint;  // last valid drop-collider point
    private Vector3 lastTableNormal = Vector3.up; // surface normal at that point
    private PlacingArea lastPlacingArea; // PlacingArea under the pointer this frame (if any)

    private float activeDragDistance; // fixed camera distance the card floats at this drag
    private Vector3 dragTargetPos;    // pose the card is currently easing toward
    private Quaternion dragTargetRot;

    // Every collider on the card being dragged, disabled for the whole drag so the card
    // can never block the drop-surface raycast (including on the release frame).
    private readonly List<Collider> dragColliders = new List<Collider>();

    private void Awake()
    {
        h.CreateStaticInstance(this, ref Instance, setDontDestroy: false);
        if (!cam) cam = Camera.main;
    }

    private void Update()
    {
        if (Mouse.current == null) return;
        if (!cam) { cam = Camera.main; if (!cam) return; }

        Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());

        if (dragging) UpdateDragging(ray);
        else UpdateHoverAndPick(ray);
    }

    // ---- not dragging: hover + start drag -----------------------------------

    private void UpdateHoverAndPick(Ray ray)
    {
        Card under = null;
        if (Physics.Raycast(ray, out RaycastHit hit, maxRayDistance, cardMask, QueryTriggerInteraction.Ignore))
            under = hit.collider.GetComponentInParent<Card>();

        // Locked cards (placed on a PlacingArea) can never be hovered or picked up.
        if (under && under.Locked) under = null;

        // Update hover highlight (only hand cards react).
        Card hoverTarget = (under && under.state == Card.CardState.InHand) ? under : null;
        if (hoverTarget != hovered)
        {
            if (hovered) hovered.SetHovered(false);
            hovered = hoverTarget;
            if (hovered) hovered.SetHovered(true);
        }

        if (Mouse.current.leftButton.wasPressedThisFrame && under && !under.Locked &&
            (under.state == Card.CardState.InHand || under.state == Card.CardState.OnTable))
        {
            // Picking up a card that was hovered in hand: play the card-slide sound.
            if (under.state == Card.CardState.InHand)
                SFXManager.Instance.PlayRandomClip(new List<AudioClip>()
                {
                    R.PROJECT.Audio.Cards.Slide.cardSlide1,
                    R.PROJECT.Audio.Cards.Slide.cardSlide2,
                });

            BeginDrag(under);
        }
    }

    private void BeginDrag(Card card)
    {
        if (hovered) { hovered.SetHovered(false); hovered = null; }

        dragging = card;
        dragOriginState = card.state;
        card.SetHovered(false);
        card.SetState(Card.CardState.Dragging);
        // Ignore the dragged card in raycasts so the drop-surface ray can't hit the card
        // itself. Disable ALL of its colliders (a card often has more than one, e.g. a quad
        // per face); leaving any enabled lets the card sit between the camera and the
        // placing area and steal the hit — so releases would never register a placement.
        dragColliders.Clear();
        card.GetComponentsInChildren(true, dragColliders);
        foreach (Collider c in dragColliders) if (c) c.enabled = false;
        hasTablePoint = false;
        lastPlacingArea = null;
        hoverHandCard = null;

        // Lock in the depth the card floats at for this whole drag. Using a fixed distance
        // (rather than projecting the cursor onto a ground plane) keeps the card the same
        // size and stops it flying into the camera / stretching toward the horizon.
        activeDragDistance = dragDistance > 0f
            ? dragDistance
            : Vector3.Distance(cam.transform.position, card.transform.position);

        // Seed the follow target at the card's current pose so it eases out, not from origin.
        dragTargetPos = card.transform.position;
        dragTargetRot = card.transform.rotation;

        // Let the rest of the hand close the gap left by the picked-up card.
        if (dragOriginState == Card.CardState.InHand && HandManager.Instance)
            HandManager.Instance.Arrange();
    }

    // ---- dragging: follow pointer + release ---------------------------------

    private void UpdateDragging(Ray ray)
    {
        // Another hand card under the pointer? If so we free-float over it (and, on release,
        // swap the two cards' places in the hand) instead of magnetizing to any table that
        // happens to sit behind the cards.
        hoverHandCard = FindHandCardUnder(ray);

        // Is the cursor over a drop surface (table / PlacingArea)? Only then does the card
        // magnetize down onto it; this is also what makes a release a valid placement.
        // Other cards are NOT drop surfaces: releasing over another card must return the
        // dragged card to the hand (or swap, above), not fling it onto the card. We therefore
        // skip any hit that belongs to a Card and use the nearest real surface behind it (if
        // any). This also guards against tableMask being left as "Everything" in the scene,
        // which would otherwise make every card collider count as a table.
        RaycastHit hit = default;
        hasTablePoint = hoverHandCard == null && TryGetDropSurface(ray, out hit);

        if (hasTablePoint)
        {
            lastTablePoint = hit.point;
            lastTableNormal = hit.normal;
            lastPlacingArea = hit.collider.GetComponentInParent<PlacingArea>();

            // Magnetize: lie the card flat (face up, parallel to the surface) and float it
            // above the plane by dragLift so it hovers over the area instead of clipping into it.
            dragTargetPos = hit.point + hit.normal * dragLift;
            dragTargetRot = FlatTableRotation(dragging);
        }
        else
        {
            lastPlacingArea = null;

            // Free-float at a FIXED distance in front of the camera along the cursor ray.
            // Constant depth => constant apparent size: the card never scales up into the
            // camera and there is no horizon stretch when the cursor nears the screen edge.
            dragTargetPos = ray.origin + ray.direction.normalized * activeDragDistance;
            // Front faces straight back at the camera; camera-up is the roll hint so the
            // orientation stays stable instead of snapping as the cursor moves.
            dragTargetRot = dragging.Face(-cam.transform.forward, cam.transform.up);
        }

        // Ease toward the target every frame (frame-rate independent, OutQuad-like glide).
        // The manager owns the motion, so the card keeps following until the button is released.
        if (dragFollowSmoothing <= 0f)
        {
            dragging.SetPoseImmediate(dragTargetPos, dragTargetRot);
        }
        else
        {
            float t = 1f - Mathf.Exp(-Time.deltaTime / dragFollowSmoothing);
            Vector3 pos = Vector3.Lerp(dragging.transform.position, dragTargetPos, t);
            Quaternion rot = Quaternion.Slerp(dragging.transform.rotation, dragTargetRot, t);
            dragging.SetPoseImmediate(pos, rot);
        }

        if (Mouse.current.leftButton.wasReleasedThisFrame)
            EndDrag();
    }

    private void EndDrag()
    {
        Card card = dragging;
        dragging = null;
        // Restore the colliders we disabled at pickup. Safe to do now: the placement
        // decision below relies on the raycast already taken this frame in UpdateDragging
        // (while the card was still non-blocking), not on a fresh cast.
        foreach (Collider c in dragColliders) if (c) c.enabled = true;
        dragColliders.Clear();

        // Released over another hand card: swap the two cards' places in the hand instead of
        // placing. Checked before the table branches so a table behind the cards can't hijack it.
        if (dragOriginState == Card.CardState.InHand && hoverHandCard &&
            hoverHandCard.state == Card.CardState.InHand)
        {
            card.SetState(Card.CardState.InHand);
            if (HandManager.Instance) HandManager.Instance.SwapCards(card, hoverHandCard);
            else card.AnimateTo(card.transform.position, card.transform.rotation);
            return;
        }

        // Released over a drop area: hand the card off to it (places, locks, leaves hand).
        if (hasTablePoint && lastPlacingArea)
        {
            lastPlacingArea.Place(card);
            return;
        }

        if (hasTablePoint)
        {
            // Valid placement on the table.
            if (dragOriginState == Card.CardState.InHand && PlayerManager.Instance)
                PlayerManager.Instance.RemoveCardFromHand(card);

            card.SetState(Card.CardState.OnTable);
            card.SetFaceUp(placeFaceUpOnTable);
            card.AnimateTo(lastTablePoint + lastTableNormal * liftingOverTable, FlatTableRotation(card));
        }
        else if (dragOriginState == Card.CardState.OnTable)
        {
            // Dropped off the table but it was already a table card: keep it on the table.
            card.SetState(Card.CardState.OnTable);
            card.AnimateTo(lastTablePoint + lastTableNormal * liftingOverTable, FlatTableRotation(card));
        }
        else
        {
            // Dropped off the table from the hand: return to the hand layout.
            card.SetState(Card.CardState.InHand);
            if (HandManager.Instance) HandManager.Instance.Arrange();
        }
    }

    // ---- helpers ------------------------------------------------------------

    /// <summary>
    /// Casts <paramref name="ray"/> against <see cref="tableMask"/> and returns the nearest hit
    /// that is an actual drop surface — i.e. one that does NOT belong to a <see cref="Card"/>.
    /// Cards are skipped so hovering/releasing over another card is never treated as a placement
    /// (the dragged card returns to the hand instead). Returns false when only cards (or nothing)
    /// are under the pointer.
    /// </summary>
    private bool TryGetDropSurface(Ray ray, out RaycastHit surfaceHit)
    {
        surfaceHit = default;

        RaycastHit[] hits = Physics.RaycastAll(ray, maxRayDistance, tableMask, QueryTriggerInteraction.Ignore);
        if (hits.Length == 0) return false;

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        for (int i = 0; i < hits.Length; i++)
        {
            // Skip cards: they are not drop surfaces. (The dragged card's own colliders are
            // already disabled for the drag, so this only filters OTHER cards.)
            if (hits[i].collider.GetComponentInParent<Card>()) continue;

            surfaceHit = hits[i];
            return true;
        }

        return false;
    }

    /// <summary>
    /// Casts <paramref name="ray"/> against <see cref="cardMask"/> and returns the nearest
    /// hand card under the pointer that can be swapped with — i.e. an unlocked <see cref="Card"/>
    /// in the <see cref="Card.CardState.InHand"/> state that is not the card being dragged.
    /// Returns null when no such card is under the pointer. (The dragged card's own colliders
    /// are disabled during the drag, so it is never returned.)
    /// </summary>
    private Card FindHandCardUnder(Ray ray)
    {
        RaycastHit[] hits = Physics.RaycastAll(ray, maxRayDistance, cardMask, QueryTriggerInteraction.Ignore);
        if (hits.Length == 0) return null;

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        for (int i = 0; i < hits.Length; i++)
        {
            Card c = hits[i].collider.GetComponentInParent<Card>();
            if (!c) continue;                                  // non-card collider: look past it
            if (c == dragging) continue;                       // ignore the dragged card itself
            if (c.Locked) continue;                            // placed/locked cards can't swap
            if (c.state != Card.CardState.InHand) continue;    // only hand cards swap
            return c;
        }

        return null;
    }

    /// <summary>Flat on the table, front up, artwork oriented away from the camera (respects the card's offset).</summary>
    private Quaternion FlatTableRotation(Card card)
    {
        Vector3 camForwardFlat = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;
        if (camForwardFlat.sqrMagnitude < 0.0001f) camForwardFlat = Vector3.forward;
        return card.Face(Vector3.up, camForwardFlat);
    }
}
