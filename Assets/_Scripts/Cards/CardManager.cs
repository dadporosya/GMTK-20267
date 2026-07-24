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
    [Tooltip("Tables whose placed cards receive turn effects and count down each time a card is played.")]
    public List<PlacingArea> targetTables = new List<PlacingArea>();
    [Tooltip("Very small delay (seconds) before each OTHER card ticks its countdown down, so " +
             "the countdowns reduce one after another instead of all at once.")]
    [SerializeField] private float countdownTickDelay = 0.1f;

    [SerializeField] private Card pfbTest;

    [HideInInspector] public Card currentPlacedCard;

    [SerializeField] private List<CardDataBase> startCards = new List<CardDataBase>();
    
    
    [Header("Card Appearance")]
    public List<Texture2D> cardTextures = new List<Texture2D>();
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
        
        /// TASK: Acces card's front and back model's material and assign random texture from cardTextures to CardTexture property
        
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
    /// cards can read what was just played), then resolves every card's effect in a fixed order
    /// and finally ticks the table's countdowns.
    ///
    /// Effect resolution order (one card at a time, via <see cref="EffectResolverManager"/>):
    ///   1. cards that react to another card being placed (<see cref="CP.ActivateCond.OtherCardPlaced"/>),
    ///   2. the freshly placed card itself, if it activates on placement (<see cref="CP.ActivateCond.Burn"/>),
    ///   3. every card whose effect fires each turn (<see cref="CP.ActivateCond.OnTurnEnd"/> /
    ///      <see cref="CP.ActivateCond.OnTurnStart"/> — now one and the same phase).
    ///
    /// Then the countdown phase runs (see <see cref="ReduceCountdownsCoroutine"/>): the placed
    /// card does NOT tick on the turn it was placed (it burns immediately if its countdown is 0,
    /// otherwise it is left untouched), and every OTHER card counts down one step, staggered.
    /// </summary>
    private IEnumerator OnCardPlacedCoroutine(Card placedCard)
    {
        currentPlacedCard = placedCard;

        h.Out("current placed card", placedCard);

        // --- Effect resolution, queued in order so the resolver plays them 1 -> 2 -> 3. ---

        // 1. Cards reacting to another card being placed (never the placed card itself).
        foreach (Card card in CardsOnTargetTables())
        {
            if (!card || card == placedCard) continue;   // the placed card never triggers itself here
            if (card.countdown <= 0) continue;            // cards on their way out don't react
            if (card.cardData && card.cardData.activation == CP.ActivateCond.OtherCardPlaced)
                card.PrepareForActivation();
        }

        // 2. The placed card's own effect, if it resolves on placement ("burn" effect).
        if (placedCard && placedCard.cardData
            && placedCard.cardData.activation == CP.ActivateCond.Burn)
            placedCard.PrepareForActivation();

        // 3. Every card whose effect fires each turn. OnTurnEnd and OnTurnStart are the same
        //    phase now — both resolve here, after each card was played.
        foreach (Card card in CardsOnTargetTables())
        {
            if (!card) continue;
            if (card.countdown <= 0) continue;
            if (card.cardData &&
                (card.cardData.activation == CP.ActivateCond.OnTurnEnd
                 || card.cardData.activation == CP.ActivateCond.OnTurnStart))
                card.PrepareForActivation();
        }

        // Resolve the whole queue one effect at a time (order preserved: 1 -> 2 -> 3).
        if (EffectResolverManager.Instance)
            yield return EffectResolverManager.Instance.EffectResolveCoroutine();

        // --- Countdown phase. ---
        yield return ReduceCountdownsCoroutine(placedCard);
    }

    /// <summary>
    /// Ticks the table's countdowns after the placed card's effects have resolved.
    ///
    /// The freshly placed card is handled first and does NOT tick on the turn it was placed:
    ///   - countdown 0  -> it was a "resolve once" card: burn it immediately, with no tick SFX/anim,
    ///   - countdown > 0 -> leave its countdown untouched this turn (it starts counting next turn).
    ///
    /// Then every OTHER card on the tracked tables counts down by one, one after another with a
    /// very small <see cref="countdownTickDelay"/> before each, and burns the moment it hits 0.
    /// </summary>
    private IEnumerator ReduceCountdownsCoroutine(Card placedCard)
    {
        if (placedCard)
        {
            if (placedCard.countdown <= 0)
                placedCard.BurnNow();   // burn at once — no tick SFX/animation
            // else: its countdown is left alone on the turn it was placed.
        }

        var wait = new WaitForSeconds(countdownTickDelay);
        foreach (Card card in CardsOnTargetTables())
        {
            if (!card || card == placedCard) continue;
            yield return wait;         // small beat so countdowns reduce one after another
            card.TickCountdown();      // reduce by 1, tick SFX/anim, and burn if it reaches 0
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
