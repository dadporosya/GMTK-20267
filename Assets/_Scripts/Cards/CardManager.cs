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
    [SerializeField] private List<ScriptableObjectContainer> rawAdditionalPiles;
    [SerializeField] private List<CP.Suit> pileSuits =  new List<CP.Suit>();
    public Dictionary<CP.Suit, ScriptableObjectContainer> additionalPiles = new Dictionary<CP.Suit, ScriptableObjectContainer>();

    [Header("Turn effects")]
    [Tooltip("Tables whose placed cards receive OnTurnStart / OnTurnEnd each turn.")]
    public List<PlacingArea> targetTables = new List<PlacingArea>();
    [Tooltip("Delay (seconds) between each card's turn-effect activation.")]
    [SerializeField] private float delayBetweenTurnEffects = 0.25f;

    [SerializeField] private Card pfbTest;

    [HideInInspector] public Card currentPlacedCard;

    [SerializeField] private List<CardDataBase> startCards = new List<CardDataBase>();

    private void Awake()
    {
        h.CreateStaticInstance(this, ref Instance);

        // Work on runtime copies so drawing / editing never mutates the original SO assets on disk.
        if (fullPile) fullPile = Instantiate(fullPile);
        for (int i = 0; i < additionalPiles.Count; i++)
        {
            additionalPiles.Add(pileSuits[i], Instantiate(rawAdditionalPiles[i]));
        }
    }

    private void Start()
    {
        foreach (var card in startCards)
        {
            SpawnCard(pfbTest, card);
        }

        foreach (var table in GameObject.FindGameObjectsWithTag("Table"))
        {
            if (!table.TryGetComponent(out PlacingArea placingArea)) return;
            if (!targetTables.Contains(placingArea)) targetTables.Add(placingArea);
        }
        
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
    /// When the pile is empty it is rebuilt from <see cref="fullPile"/> (a fresh copy, so the
    /// source asset is never touched) and the draw proceeds. Returns null only when there is
    /// nothing to draw even after refilling (i.e. <see cref="fullPile"/> is empty/unset too).
    /// </summary>
    public Card DrawCard()
    {
        if (!pile || pile.scriptableObjects.Count == 0)
        {
            // Pile ran out — reshuffle: create a new pile from the full pile.
            RoundStart();

            if (!pile || pile.scriptableObjects.Count == 0)
            {
                h.Out("CardManager: pile and full pile are both empty, nothing to draw.");
                return null;
            }
        }

        CardDataBase dataBase = h.RandChoice(pile.scriptableObjects) as CardDataBase;
        pile.scriptableObjects.Remove(dataBase);

        return SpawnCard(pfbTest, dataBase);
    }

    public Card SpawnCard(Card cardPrefab, CardDataBase dataBase = null)
    {
        SFXManager.Instance.PlayRandomClip(new List<AudioClip>()
        {
            R.PROJECT.Audio.Cards.TakeCard.takeCard1,
            R.PROJECT.Audio.Cards.TakeCard.takeCard2,
            R.PROJECT.Audio.Cards.TakeCard.takeCard3,
            // R.PROJECT.Audio.Cards.TakeCard.takeCard4,
        });

        Card card = Instantiate(cardPrefab);
        if (dataBase) card.cardData = dataBase;
        Cards.Add(card);
        return card;
    }

    /// <summary>
    /// Called by <see cref="Card.OnPlace"/> every time a card lands on the table. Records the
    /// placed card, queues every reacting card and resolves the whole effect queue one card at
    /// a time, then advances the turn (see <see cref="OnCardPlacedCoroutine"/>).
    /// </summary>
    public void OnCardPlaced(Card placedCard)
    {
        StartCoroutine(OnCardPlacedCoroutine(placedCard));
    }

    /// <summary>
    /// Records <paramref name="placedCard"/> as <see cref="currentPlacedCard"/> (so reacting
    /// cards can read what was just played), then queues every OTHER card on the tracked tables
    /// whose activation is <see cref="CP.ActivateCond.OtherCardPlaced"/> and whose
    /// <see cref="Card.countdown"/> is still &gt; 0. The placed card is skipped so it never
    /// triggers itself (it already queued its own effect first, in <see cref="Card.OnPlace"/>).
    /// Once every effect has resolved consecutively, the turn ends and the next one starts.
    /// </summary>
    private IEnumerator OnCardPlacedCoroutine(Card placedCard)
    {
        currentPlacedCard = placedCard;

        h.Out("current placed card", placedCard);

        // Queue every OTHER reacting card so it responds to the freshly placed card. The placed
        // card sits first in the queue (queued in Card.OnPlace), so it resolves before these.
        foreach (Card card in CardsOnTargetTables())
        {
            if (!card || card == placedCard) continue;   // don't self-trigger the placed card
            if (card.countdown <= 0) continue;            // only cards still counting down react
            if (card.cardData && card.cardData.activation == CP.ActivateCond.OtherCardPlaced)
                card.PrepareForActivation();
        }

        // Resolve the whole queue one effect at a time.
        if (EffectResolverManager.Instance)
            yield return EffectResolverManager.Instance.EffectResolveCoroutine();

        // End the current turn, then start the next.
        yield return OnTurnEndCoroutine();
        yield return OnTurnStartCoroutine();
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

        // Queue every card on the tracked tables whose activation is OnTurnStart so its effect
        // resolves at the start of the turn (mirrors how OnCardPlaced queues reacting cards).
        foreach (Card card in CardsOnTargetTables())
        {
            if (!card) continue;
            if (card.countdown <= 0) continue;            // expired cards are being destroyed already
            if (card.cardData && card.cardData.activation == CP.ActivateCond.OnTurnStart)
                card.PrepareForActivation();
        }

        // Resolve the whole queue one effect at a time, after all cards have been looked through.
        if (EffectResolverManager.Instance)
            yield return EffectResolverManager.Instance.EffectResolveCoroutine();
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
