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
        h.Out(Cards);
    }

    /// <summary>Removes a card from the hand and re-arranges immediately.</summary>
    public void RemoveCard(Card card)
    {
        if (card && Cards.Remove(card))
            Arrange();
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

            slotted[i].SetHomePose(pos, rot, instant);
            slotted[i].transform.SetSiblingIndex(i); // keep a stable draw order
        }
    }
}
