using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CardManager : MonoBehaviour
{
    public static CardManager Instance;
    public List<Card> Cards;

    [Header("Piles")]
    [Tooltip("The draw pile for the current round. Filled from fullPile at RoundStart and " +
             "drained as cards are drawn. This is a runtime instance — the asset is never touched.")]
    public ScriptableObjectContainer pile;
    [Tooltip("The complete pool a round starts with. pile is (re)built as a copy of this at RoundStart.")]
    public ScriptableObjectContainer fullPile;
    [Tooltip("Extra cards that can be folded into the pile during a round.")]
    public ScriptableObjectContainer additionalCards;

    [Header("Turn effects")]
    [Tooltip("Tables whose placed cards receive OnTurnStart / OnTurnEnd each turn.")]
    public List<PlacingArea> targetTables = new List<PlacingArea>();
    [Tooltip("Delay (seconds) between each card's turn-effect activation.")]
    [SerializeField] private float delayBetweenTurnEffects = 0.25f;

    [SerializeField] private Card pfbTest;

    private void Awake()
    {
        h.CreateStaticInstance(this, ref Instance);

        // Work on runtime copies so drawing / editing never mutates the original SO assets on disk.
        if (fullPile) fullPile = Instantiate(fullPile);
        if (additionalCards) additionalCards = Instantiate(additionalCards);
    }

    private void Start()
    {
        RoundStart();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Q))
        {
            DrawCard();
        }
    }

    /// <summary>
    /// Resets the round: rebuilds <see cref="pile"/> as a fresh copy of <see cref="fullPile"/> so
    /// the round starts with the full pool and draining the pile never affects the source asset.
    /// </summary>
    public void RoundStart()
    {
        pile = fullPile ? Instantiate(fullPile) : null;
    }

    /// <summary>
    /// Draws a random card from <see cref="pile"/>, removes it from the pile and spawns it.
    /// Returns the spawned card, or null when the pile is empty.
    /// </summary>
    public Card DrawCard()
    {
        if (!pile || pile.scriptableObjects.Count == 0)
        {
            h.Out("CardManager: pile is empty, nothing to draw.");
            return null;
        }

        CardData data = h.RandChoice(pile.scriptableObjects) as CardData;
        pile.scriptableObjects.Remove(data);

        return SpawnCard(pfbTest, data);
    }

    public Card SpawnCard(Card cardPrefab, CardData data = null)
    {
        SFXManager.Instance.PlayRandomClip(new List<AudioClip>()
        {
            R.PROJECT.Audio.Cards.TakeCard.takeCard1,
            R.PROJECT.Audio.Cards.TakeCard.takeCard2,
            R.PROJECT.Audio.Cards.TakeCard.takeCard3,
            // R.PROJECT.Audio.Cards.TakeCard.takeCard4,
        });

        Card card = Instantiate(cardPrefab);
        if (data) card.cardData = data;
        Cards.Add(card);
        return card;
    }

    /// <summary>
    /// Fires <see cref="Card.OnTurnStart"/> on every card placed on <see cref="targetTables"/>,
    /// waiting <see cref="delayBetweenTurnEffects"/> between each activation.
    /// </summary>
    public void OnTurnStart()
    {
        StartCoroutine(OnTurnStartCoroutine());
    }

    /// <summary>
    /// Fires <see cref="Card.OnTurnEnd"/> on every card placed on <see cref="targetTables"/>,
    /// waiting <see cref="delayBetweenTurnEffects"/> between each activation.
    /// </summary>
    public void OnTurnEnd()
    {
        StartCoroutine(OnTurnEndCoroutine());
    }

    private IEnumerator OnTurnStartCoroutine()
    {
        var wait = new WaitForSeconds(delayBetweenTurnEffects);
        foreach (Card card in CardsOnTargetTables())
        {
            card.OnTurnStart();
            yield return wait;
        }
    }

    private IEnumerator OnTurnEndCoroutine()
    {
        var wait = new WaitForSeconds(delayBetweenTurnEffects);
        foreach (Card card in CardsOnTargetTables())
        {
            card.OnTurnEnd();
            yield return wait;
        }
    }

    /// <summary>
    /// Snapshots every card currently placed on the tracked tables (copied so cards
    /// leaving play mid-iteration — e.g. burning — don't disturb the enumeration).
    /// </summary>
    private List<Card> CardsOnTargetTables()
    {
        var result = new List<Card>();
        foreach (PlacingArea table in targetTables)
        {
            if (!table) continue;
            foreach (Card card in table.cards)
                if (card) result.Add(card);
        }
        return result;
    }
}
