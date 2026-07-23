using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Handles mouse picking and dragging of 3D cards using the new Input System.
///
/// Flow:
///   - Hover a card in the hand  -> it lifts (Card.SetHovered).
///   - Press on a card           -> begins dragging it (InHand or OnTable cards).
///   - While held                -> the card follows the pointer projected onto
///                                  the table, lifted by <see cref="dragLift"/>.
///   - Release over the table    -> the card lands flat, face up, on the table
///                                  (and leaves the hand).
///   - Release off the table     -> the card snaps back where it came from.
///
/// The "table" is any collider on <see cref="tableMask"/>. If no collider is hit,
/// the pointer is projected onto the horizontal plane at <see cref="tablePlaneY"/>
/// so dragging still feels right past the table edges — but a release only counts
/// as a valid placement when an actual table collider is under the pointer.
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
    [Tooltip("How high above the table the card floats while being dragged.")]
    [SerializeField] private float dragLift = 0.4f;
    [Tooltip("Fallback horizontal plane height used when no table collider is under the pointer.")]
    [SerializeField] private float tablePlaneY = 0f;
    [Tooltip("Follow smoothing while dragging (seconds-ish). Smaller = snappier, larger = floatier. " +
             "Produces an ease-out (OutQuad-like) glide toward the cursor. 0 = instant.")]
    [SerializeField] private float dragFollowSmoothing = 0.08f;
    [Tooltip("Cards land face up on the table when true.")]
    [SerializeField] private bool placeFaceUpOnTable = true;
    [SerializeField] private float maxRayDistance = 1000f;

    private Card dragging;
    private Card.CardState dragOriginState;
    private Card hovered;

    private bool hasTablePoint;     // pointer is over an actual table collider this frame
    private Vector3 lastTablePoint; // last valid table-collider point
    private PlacingArea lastPlacingArea; // PlacingArea under the pointer this frame (if any)

    private float dragPlaneY;       // horizontal plane height the cursor is projected onto while dragging
    private Vector3 dragTargetPos;  // pose the card is currently easing toward
    private Quaternion dragTargetRot;

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
        // Ignore the dragged card in raycasts so the table ray can't hit the card
        // itself (matters when card/table share a layer or masks overlap).
        if (card.Col) card.Col.enabled = false;
        hasTablePoint = false;
        lastPlacingArea = null;

        // Pick the horizontal plane the cursor will be projected onto for the whole drag.
        // Prefer the table surface directly under the card; otherwise use the configured
        // fallback height. Keeping this fixed for the drag means the card keeps following
        // the cursor smoothly even when the pointer wanders off the table mesh.
        Ray downFromCard = new Ray(card.transform.position + Vector3.up * maxRayDistance, Vector3.down);
        if (Physics.Raycast(downFromCard, out RaycastHit surf, maxRayDistance * 2f, tableMask, QueryTriggerInteraction.Ignore))
        {
            dragPlaneY = surf.point.y;
            lastTablePoint = surf.point;
        }
        else
        {
            dragPlaneY = tablePlaneY;
            lastTablePoint = new Vector3(card.transform.position.x, tablePlaneY, card.transform.position.z);
        }

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
        // Is a real table / drop collider under the pointer? This only decides whether a
        // release counts as a valid placement (and remembers the drop area / snap point).
        hasTablePoint = Physics.Raycast(ray, out RaycastHit hit, maxRayDistance, tableMask, QueryTriggerInteraction.Ignore);
        if (hasTablePoint)
        {
            lastTablePoint = hit.point;
            lastPlacingArea = hit.collider.GetComponentInParent<PlacingArea>();
        }

        // Follow target: project the cursor onto the fixed drag plane so the card keeps
        // tracking the mouse even when the pointer is nowhere near the table mesh. Only a
        // ray parallel to the plane fails, in which case we keep the previous target.
        if (RaycastPlane(ray, dragPlaneY, out Vector3 point))
        {
            dragTargetPos = point + Vector3.up * dragLift;
            // Face the card's front toward the camera (via Card.Face, so faceRotationOffset
            // applies). Camera-up is the roll hint: world up can go near-parallel to the view
            // on a top-down camera and make the rotation snap 180 degrees; camera-up stays
            // perpendicular to the view, so the front stays put.
            dragTargetRot = dragging.Face(cam.transform.position - dragTargetPos, cam.transform.up);
        }

        // Ease toward the target every frame (frame-rate independent, OutQuad-like glide).
        // The manager owns the motion now, so the card follows continuously until release.
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
        if (card.Col) card.Col.enabled = true;

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
            card.AnimateTo(lastTablePoint, FlatTableRotation(card));
        }
        else if (dragOriginState == Card.CardState.OnTable)
        {
            // Dropped off the table but it was already a table card: keep it on the table.
            card.SetState(Card.CardState.OnTable);
            card.AnimateTo(lastTablePoint, FlatTableRotation(card));
        }
        else
        {
            // Dropped off the table from the hand: return to the hand layout.
            card.SetState(Card.CardState.InHand);
            if (HandManager.Instance) HandManager.Instance.Arrange();
        }
    }

    // ---- helpers ------------------------------------------------------------

    /// <summary>Flat on the table, front up, artwork oriented away from the camera (respects the card's offset).</summary>
    private Quaternion FlatTableRotation(Card card)
    {
        Vector3 camForwardFlat = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;
        if (camForwardFlat.sqrMagnitude < 0.0001f) camForwardFlat = Vector3.forward;
        return card.Face(Vector3.up, camForwardFlat);
    }

    /// <summary>Intersects a ray with the horizontal plane at height y. False if parallel/behind.</summary>
    private static bool RaycastPlane(Ray ray, float y, out Vector3 point)
    {
        point = default;
        if (Mathf.Abs(ray.direction.y) < 0.0001f) return false;
        float t = (y - ray.origin.y) / ray.direction.y;
        if (t < 0f) return false;
        point = ray.origin + ray.direction * t;
        return true;
    }
}
