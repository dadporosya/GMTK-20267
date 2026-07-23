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
    [Tooltip("Cards land face up on the table when true.")]
    [SerializeField] private bool placeFaceUpOnTable = true;
    [SerializeField] private float maxRayDistance = 1000f;

    private Card dragging;
    private Card.CardState dragOriginState;
    private Card hovered;

    private bool hasTablePoint;     // pointer is over an actual table collider this frame
    private Vector3 lastTablePoint; // last valid table-collider point

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

        // Update hover highlight (only hand cards react).
        Card hoverTarget = (under && under.state == Card.CardState.InHand) ? under : null;
        if (hoverTarget != hovered)
        {
            if (hovered) hovered.SetHovered(false);
            hovered = hoverTarget;
            if (hovered) hovered.SetHovered(true);
        }

        if (Mouse.current.leftButton.wasPressedThisFrame && under &&
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

        // Let the rest of the hand close the gap left by the picked-up card.
        if (dragOriginState == Card.CardState.InHand && HandManager.Instance)
            HandManager.Instance.Arrange();
    }

    // ---- dragging: follow pointer + release ---------------------------------

    private void UpdateDragging(Ray ray)
    {
        // Where is the pointer on the table? Prefer a real collider, fall back to a plane.
        hasTablePoint = Physics.Raycast(ray, out RaycastHit hit, maxRayDistance, tableMask, QueryTriggerInteraction.Ignore);
        Vector3 point;
        if (hasTablePoint)
        {
            point = hit.point;
            lastTablePoint = point;
        }
        else if (!RaycastPlane(ray, tablePlaneY, out point))
        {
            point = lastTablePoint; // pointer aimed away from the plane; keep last
        }

        Vector3 target = point + Vector3.up * dragLift;
        // Face the card's front (+Z) toward the camera, using the CAMERA's up as the
        // roll hint. Using world up here can go near-parallel to the view direction on
        // a top-down camera, which makes LookRotation snap the card 180 degrees (showing
        // the back). Camera up stays perpendicular to the view, so the front stays up.
        Quaternion rot = Quaternion.LookRotation((cam.transform.position - target).normalized, cam.transform.up);
        dragging.SetPoseImmediate(target, rot);

        if (Mouse.current.leftButton.wasReleasedThisFrame)
            EndDrag();
    }

    private void EndDrag()
    {
        Card card = dragging;
        dragging = null;
        if (card.Col) card.Col.enabled = true;

        if (hasTablePoint)
        {
            // Valid placement on the table.
            if (dragOriginState == Card.CardState.InHand && PlayerManager.Instance)
                PlayerManager.Instance.RemoveCardFromHand(card);

            card.SetState(Card.CardState.OnTable);
            card.SetFaceUp(placeFaceUpOnTable);
            card.AnimateTo(lastTablePoint, FlatTableRotation());
        }
        else if (dragOriginState == Card.CardState.OnTable)
        {
            // Dropped off the table but it was already a table card: keep it on the table.
            card.SetState(Card.CardState.OnTable);
            card.AnimateTo(lastTablePoint, FlatTableRotation());
        }
        else
        {
            // Dropped off the table from the hand: return to the hand layout.
            card.SetState(Card.CardState.InHand);
            if (HandManager.Instance) HandManager.Instance.Arrange();
        }
    }

    // ---- helpers ------------------------------------------------------------

    /// <summary>Flat on the table, front up, artwork oriented away from the camera.</summary>
    private Quaternion FlatTableRotation()
    {
        Vector3 camForwardFlat = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;
        if (camForwardFlat.sqrMagnitude < 0.0001f) camForwardFlat = Vector3.forward;
        return Quaternion.LookRotation(Vector3.up, camForwardFlat);
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
