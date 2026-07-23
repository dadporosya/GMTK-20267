using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Owns the player's hand. Holds the List&lt;Card&gt; and is the single place that
/// adds/removes cards, keeping the HandManager layout in sync.
/// </summary>
public class PlayerManager : MonoBehaviour
{
    public static PlayerManager Instance;

    [Header("References")]
    [SerializeField] private HandManager handManager;
    [Tooltip("Optional parent all spawned/held cards are re-parented under. Keeps the hierarchy tidy.")]
    [SerializeField] private Transform cardsParent;

    [Header("Dealing")]
    [Tooltip("Prefab used by DrawCard(). Must have a Card component (see prefab requirements).")]
    [SerializeField] private Card cardPrefab;
    [Tooltip("Where freshly drawn cards spawn before flying into the hand (e.g. a deck transform). Falls back to this object.")]
    [SerializeField] private Transform drawOrigin;

    /// <summary>The cards currently in hand. Backed by the HandManager list (single source of truth).</summary>
    public List<Card> Hand
    {
        get
        {
            if (!handManager) handManager = HandManager.Instance;
            return handManager ? handManager.Cards : null;
        }
    }

    private void Awake()
    {
        h.CreateStaticInstance(this, ref Instance, setDontDestroy: false);
        if (!handManager) handManager = HandManager.Instance;
    }

    /// <summary>Adds an existing card to the hand (HandManager appends it and re-arranges).</summary>
    public void AddCardToHand(Card card)
    {
        if (!card) return;
        if (!handManager) handManager = HandManager.Instance;
        if (!handManager) { h.Out("PlayerManager.AddCardToHand: no HandManager in scene."); return; }

        if (cardsParent) card.transform.SetParent(cardsParent, worldPositionStays: true);

        handManager.AddCard(card);
    }

    /// <summary>Removes a card from the hand (e.g. when it is played onto the table) and re-arranges.</summary>
    public void RemoveCardFromHand(Card card)
    {
        if (!handManager) handManager = HandManager.Instance;
        if (handManager) handManager.RemoveCard(card);
    }

    /// <summary>Spawns a new card from the prefab at the draw origin and sends it to the hand.</summary>
    public Card DrawCard()
    {
        if (!cardPrefab)
        {
            h.Out("PlayerManager.DrawCard: no cardPrefab assigned.");
            return null;
        }

        Transform origin = drawOrigin ? drawOrigin : transform;
        Card card = Instantiate(cardPrefab, origin.position, origin.rotation);
        AddCardToHand(card);
        return card;
    }
}
