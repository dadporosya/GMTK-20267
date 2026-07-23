using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A surface a card can be dropped onto. When the player drags a card and releases
/// the mouse over this area's collider, <see cref="CardDragController"/> routes the
/// drop here and calls <see cref="Place"/>.
///
/// Placing a card:
///   - snaps it flat onto this area's surface (under where it was released),
///   - fires the card's <see cref="Card.OnPlace"/> hook,
///   - removes it from the player's hand,
///   - and locks it so it can no longer be moved.
///
/// Spatial convention (matches the project): the table is the XZ plane, Y is up.
/// </summary>
[RequireComponent(typeof(Collider))]
public class PlacingArea : MonoBehaviour
{
    [Header("Contents")]
    [Tooltip("Cards currently placed on this area. Filled by Place().")]
    public List<Card> cards = new List<Card>();

    [Header("Placement")]
    [Tooltip("How far above the area surface a placed card rests (world units, along +Y). " +
             "Keeps the card from being coplanar with the surface (avoids z-fighting) and " +
             "gives it a visible lift over the table. 0 = flat on the surface.")]
    [SerializeField] private float liftingOverTable = 0.05f;

    [Header("References")]
    [Tooltip("Surface collider a card is dropped onto. Falls back to the collider on this object.")]
    [SerializeField] private Collider areaCollider;
    [Tooltip("Camera used to orient placed cards face-up toward the viewer. Falls back to Camera.main.")]
    [SerializeField] private Camera cam;

    /// <summary>The collider that defines this drop area (used by CardDragController for hit-testing).</summary>
    public Collider Area => areaCollider;

    private void Awake()
    {
        if (!areaCollider) areaCollider = GetComponent<Collider>();
        if (!cam) cam = Camera.main;
    }

    /// <summary>
    /// Places <paramref name="card"/> on this area: snaps it to the surface, calls its
    /// OnPlace hook, removes it from the hand and locks it in place. No-op if the card is
    /// null or already placed here.
    /// </summary>
    public void Place(Card card)
    {
        if (!card || cards.Contains(card)) return;

        // Leave the player's hand (HandManager re-arranges the remaining cards).
        if (PlayerManager.Instance) PlayerManager.Instance.RemoveCardFromHand(card);

        // Land flat on the surface, lifted slightly above it, face up, then pin it.
        Vector3 pos = SurfacePointUnder(card.transform.position) + Vector3.up * liftingOverTable;
        card.SetState(Card.CardState.OnTable);
        card.SetFaceUp(true);
        card.AnimateTo(pos, FlatRotation(card));
        card.Lock();

        cards.Add(card);

        // Notify the card it has been placed.
        card.OnPlace();
    }

    /// <summary>
    /// Projects <paramref name="from"/> straight down onto this area's collider so the card
    /// rests on the actual surface. Falls back to the collider's top if the ray misses.
    /// </summary>
    private Vector3 SurfacePointUnder(Vector3 from)
    {
        if (!areaCollider) return from;

        Ray down = new Ray(from + Vector3.up * 100f, Vector3.down);
        if (areaCollider.Raycast(down, out RaycastHit hit, 200f))
            return hit.point;

        // Ray missed (card released slightly off the collider footprint): use the top face.
        Bounds b = areaCollider.bounds;
        return new Vector3(from.x, b.max.y, from.z);
    }

    /// <summary>Flat on the surface, front up, artwork oriented away from the camera.</summary>
    private Quaternion FlatRotation(Card card)
    {
        if (!cam) cam = Camera.main;
        Vector3 camForwardFlat = cam
            ? Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized
            : Vector3.forward;
        if (camForwardFlat.sqrMagnitude < 0.0001f) camForwardFlat = Vector3.forward;
        return card.Face(Vector3.up, camForwardFlat);
    }
}
