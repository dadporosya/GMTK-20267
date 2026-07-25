using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lays out the cards that are currently in the player's hand as real 3D objects
/// anchored to the camera, so the hand always sits in the lower part of the
/// player's view no matter where the camera looks.
///
/// The anchor is built in camera space:
///   anchor = camPos + camForward*distanceInFront + camUp*heightOffset + camRight*horizontalOffset
/// Cards are then spread along the camera's right axis, optionally bowed into an
/// arc and fanned/tilted so they read like a hand of cards.
///
/// It only positions cards whose state is InHand — a card being dragged is
/// skipped so the remaining cards close the gap, and it re-joins the layout when
/// it returns to the hand.
/// </summary>
public class HandManager : MonoBehaviour
{
    public static HandManager Instance;

    [Header("References")]
    [Tooltip("Camera the hand is anchored to. Falls back to Camera.main.")]
    [SerializeField] private Camera targetCamera;

    [Header("Anchor (camera-relative, world units)")]
    [Tooltip("How far in front of the camera the hand sits.")]
    [SerializeField] private float distanceInFront = 2.2f;
    [Tooltip("Vertical offset from the camera centre. Negative pushes the hand toward the bottom of the screen.")]
    [SerializeField] private float heightOffset = -1.0f;
    [Tooltip("Horizontal offset from the camera centre.")]
    [SerializeField] private float horizontalOffset = 0f;

    [Header("Spread")]
    [Tooltip("Horizontal gap between neighbouring cards.")]
    [SerializeField] private float cardGap = 0.6f;
    [Tooltip("Clamps the total width of the hand; cards overlap more as the hand grows past this. 0 = no clamp.")]
    [SerializeField] private float maxHandWidth = 6f;

    [Header("Shape")]
    [Tooltip("How much the row bows toward the player. 0 = flat row.")]
    [SerializeField] private float arcHeight = 0.25f;
    [Tooltip("Total fan angle across the whole hand, in degrees (rolls each card around its facing axis).")]
    [SerializeField] private float fanAngle = 12f;
    [Tooltip("Pitch (degrees) tilting the tops of the cards back toward the player.")]
    [SerializeField] private float cardPitch = 15f;

    [Header("Behaviour")]
    [Tooltip("Recompute the layout every frame so the hand follows a moving camera. Turn off to only arrange on demand.")]
    [SerializeField] private bool arrangeContinuously = true;

    /// <summary>The cards currently in the hand. This is the layout's source of truth.</summary>
    public List<Card> Cards { get; } = new List<Card>();

    // Hover-focus state, pushed in from CardDragController. Read by Arrange to raise the focused
    // card toward the camera (so it renders on top) and/or dip the others so it stands clear.
    private Card focusedCard;
    private bool focusRaiseSelected;
    private bool focusLowerOthers;
    private float focusRaiseAmount;
    private float focusLowerAmount;
    private float focusLowerRotationX;   // degrees around the X axis applied to the lowered cards

    // Reused each Arrange() call so the layout does not allocate.
    public List<Card> slotted = new List<Card>();

    private void Awake()
    {
        h.CreateStaticInstance(this, ref Instance, setDontDestroy: false);
        if (!targetCamera) targetCamera = Camera.main;
    }

    private void LateUpdate()
    {
        // Optional: keep the hand glued to a moving camera. Add/remove already
        // re-arrange on their own, so this can be turned off for a static camera.
        if (arrangeContinuously) Arrange();
    }

    /// <summary>Adds a card to the hand, marks it InHand, and re-arranges immediately.</summary>
    public void AddCard(Card card)
    {
        if (!card || Cards.Contains(card)) return;
        card.handManager = this;
        card.SetState(Card.CardState.InHand);
        Cards.Add(card);
        Arrange();
        // h.Out(Cards);
    }

    /// <summary>Removes a card from the hand and re-arranges immediately.</summary>
    public void RemoveCard(Card card)
    {
        if (card && Cards.Remove(card))
            Arrange();
    }

    /// <summary>
    /// Swaps the hand positions of two cards (their order in <see cref="Cards"/> is what the
    /// layout reads), then re-arranges. Used when a dragged hand card is released over another
    /// hand card. No-op if either card is missing or not in the hand.
    /// </summary>
    public void SwapCards(Card a, Card b)
    {
        if (!a || !b || a == b) return;

        int ia = Cards.IndexOf(a);
        int ib = Cards.IndexOf(b);
        if (ia < 0 || ib < 0) return;

        Cards[ia] = b;
        Cards[ib] = a;
        Arrange();
    }

    /// <summary>
    /// Sets which hand card is currently focused (hovered), and how the layout should react.
    /// Called by <see cref="CardDragController"/>; pass <paramref name="card"/> = null to clear.
    /// <paramref name="raiseSelected"/> pulls the focused card toward the camera so it renders on
    /// top; <paramref name="lowerOthers"/> dips every other hand card so the focused one stands out.
    /// Each is gated by its matching flag in the drag controller.
    /// </summary>
    public void SetHoverFocus(Card card, bool raiseSelected, bool lowerOthers,
                              float raiseAmount, float lowerAmount, float lowerRotationX)
    {
        focusedCard = card;
        focusRaiseSelected = raiseSelected;
        focusLowerOthers = lowerOthers;
        focusRaiseAmount = raiseAmount;
        focusLowerAmount = lowerAmount;
        focusLowerRotationX = lowerRotationX;
    }

    /// <summary>Whether the hand currently re-anchors to the camera every frame (cards "follow" the camera).</summary>
    public bool FollowCamera => arrangeContinuously;

    /// <summary>
    /// Turns the per-frame camera-anchored layout on/off. When ON the hand glues to the camera and
    /// follows it every frame; when OFF the cards keep their last pose and stay put while the camera
    /// moves. Re-enabling snaps the hand back in front of the camera immediately. Used by
    /// <see cref="TableTopCameraController"/> to make the hand follow only in the hand view.
    /// </summary>
    public void SetFollowCamera(bool follow)
    {
        arrangeContinuously = follow;
        if (follow) Arrange();
    }

    /// <summary>
    /// Recomputes and assigns the home pose for every InHand card. Runs automatically
    /// whenever a card is added or removed, and each frame if arrangeContinuously is on.
    /// </summary>
    public void Arrange(bool instant = false)
    {
        if (!targetCamera) targetCamera = Camera.main;
        if (!targetCamera) return;

        // Only the cards actually sitting in the hand take up a slot (a card being
        // dragged is skipped so the rest close the gap).
        slotted.Clear();
        for (int i = 0; i < Cards.Count; i++)
            if (Cards[i] && Cards[i].state == Card.CardState.InHand)
                slotted.Add(Cards[i]);

        int n = slotted.Count;
        if (n == 0) return;

        Transform cam = targetCamera.transform;
        Vector3 camRight = cam.right;
        Vector3 camUp = cam.up;
        Vector3 camForward = cam.forward;

        Vector3 anchor = cam.position
                         + camForward * distanceInFront
                         + camUp * heightOffset
                         + camRight * horizontalOffset;

        // Effective gap: shrink so the whole hand fits inside maxHandWidth.
        float gap = cardGap;
        if (maxHandWidth > 0f && n > 1)
        {
            float wanted = cardGap * (n - 1);
            if (wanted > maxHandWidth) gap = maxHandWidth / (n - 1);
        }

        float mid = (n - 1) * 0.5f;

        for (int i = 0; i < n; i++)
        {
            float offset = i - mid;                       // ...-1, 0, +1...
            float normalized = mid > 0f ? offset / mid : 0f; // -1..+1 across the hand

            // Spread sideways, bow toward the player, and nudge lower at the ends.
            Vector3 pos = anchor
                          + camRight * (offset * gap)
                          + camForward * (-Mathf.Abs(normalized) * arcHeight)  // ends closer to player
                          + camUp * (-normalized * normalized * arcHeight * 0.5f);

            // Base rotation: card front faces the camera (respecting the card's faceRotationOffset).
            Quaternion faceRot = slotted[i].Face(cam.position - pos, camUp);
            // Tilt tops back toward the player, then fan-roll around the facing axis.
            Quaternion pitch = Quaternion.AngleAxis(cardPitch, camRight);
            Quaternion roll = Quaternion.AngleAxis(-normalized * (fanAngle * 0.5f), (cam.position - pos).normalized);
            Quaternion rot = roll * pitch * faceRot;

            // Hover focus: raise the selected card toward the camera so it reads on top, and/or
            // dip the other cards so the selected one stands clear. Both effects are opt-in and
            // driven by CardDragController via SetHoverFocus; when nothing is focused this is a no-op.
            if (focusedCard)
            {
                if (slotted[i] == focusedCard)
                {
                    if (focusRaiseSelected)
                    {
                        Vector3 toCam = (cam.position - pos).normalized;
                        pos += toCam * focusRaiseAmount;   // closer to camera => rendered on top
                    }
                }
                else if (focusLowerOthers)
                {
                    // Drop the card and push it slightly back, then tilt its top further away so
                    // it reads as being "laid down" behind the selected card. The tilt is a
                    // rotation around the X axis (camera right), controlled by lowerOthersRotationX.
                    pos += camUp * -focusLowerAmount + camForward * (focusLowerAmount * 0.4f);
                    rot = Quaternion.AngleAxis(focusLowerRotationX, camRight) * rot;
                }
            }

            slotted[i].SetHomePose(pos, rot, instant);
            slotted[i].transform.SetSiblingIndex(i); // keep a stable draw order
        }
    }
}
