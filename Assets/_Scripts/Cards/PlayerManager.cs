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

    /// <summary>The cards currently in hand. Read-only from the outside.</summary>
    public List<Card> Hand { get; private set; } = new List<Card>();

    private void Awake()
    {
        h.CreateStaticInstance(this, ref Instance, setDontDestroy: false);
        if (!handManager) handManager = HandManager.Instance;
    }

    /// <summary>Adds an existing card to the hand and re-arranges.</summary>
    public void AddCardToHand(Card card)
    {
        if (!card || Hand.Contains(card)) return;

        if (cardsParent) card.transform.SetParent(cardsParent, worldPositionStays: true);

        card.handManager = handManager;
        card.SetState(Card.CardState.InHand);
        Hand.Add(card);

        RearrangeHand();
    }

    /// <summary>Removes a card from the hand (e.g. when it is played onto the table) and re-arranges.</summary>
    public void RemoveCardFromHand(Card card)
    {
        if (!card) return;
        if (Hand.Remove(card))
            RearrangeHand();
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

    private void RearrangeHand()
    {
        if (!handManager) handManager = HandManager.Instance;
        if (handManager) handManager.Arrange(Hand);
    }
}
