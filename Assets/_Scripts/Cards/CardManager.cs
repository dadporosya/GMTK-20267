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
    [SerializeField] private bool shuffleAllPiles = false;

    [Header("Add cards (folding extra cards in from additional piles)")]
    [Tooltip("How many cards the most-present / primary suit adds when extending the pile.")]
    [SerializeField] private int primarySuitCardAdd = 5;
    [Tooltip("How many cards the second-most-present / secondary suit adds.")]
    [SerializeField] private int secondarySuitCardAdd = 3;
    [Tooltip("How many cards the third-most-present / third suit adds.")]
    [SerializeField] private int thirdSuitCardAdd = 1;

    [Header("Turn effects")]
    [Tooltip("Tables whose placed cards receive turn effects and count down each time a card is played.")]
    public List<PlacingArea> targetTables = new List<PlacingArea>();
    [Tooltip("Very small delay (seconds) before each OTHER card ticks its countdown down, so " +
             "the countdowns reduce one after another instead of all at once.")]
    [SerializeField] private float countdownTickDelay = 0.1f;
    [Tooltip("Very small delay (seconds) between dealing each card into the hand, so cards are " +
             "dealt one after another instead of all at once.")]
    [SerializeField] private float dealDelay = 0.02f;

    [SerializeField] private Card pfbTest;

    [HideInInspector] public Card currentPlacedCard;

    [SerializeField] private List<CardDataBase> startCards = new List<CardDataBase>();

    private bool gameStarted = false;
    
    [Header("Card Appearance")]
    public List<Texture2D> cardTextures = new List<Texture2D>();
    private void Awake()
    {
        h.CreateStaticInstance(this, ref Instance);

        // Work on runtime copies so drawing / editing never mutates the original SO assets on disk.
        if (fullPile) fullPile = Instantiate(fullPile);
        for (int i = 0; i < rawAdditionalPiles.Count; i++)
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
        
        if (shuffleAllPiles)
        {
            h.Out(additionalPiles);
            foreach (var additionalPile in additionalPiles.Values)
            {
                if (!additionalPile || additionalPile.scriptableObjects == null) continue;
                fullPile.scriptableObjects.AddRange(additionalPile.scriptableObjects);
            }
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
        h.Out("Deal cards");
        pile = fullPile ? Instantiate(fullPile) : null;
        if (gameStarted) ProgressionManager.Instance.level -= 1;
        else gameStarted = true;
        ProgressionManager.Instance.NextLevel();
        DealFullHand();
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
            // Pile ran out mid-round — just rebuild the draw pile from the full pile.
            // Do NOT call RoundStart() here: that would advance the level and reset the score
            // in the middle of a round. Reshuffling only refills the pile.
            pile = fullPile ? Instantiate(fullPile) : null;

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

        // Put the freshly spawned card into the player's hand. Without this the card only ever
        // lands in CardManager.Cards, so PlayerManager.Hand.Count never grows and DealFullHand's
        // while-loop spins forever (the "endless loop" freeze on RoundStart).
        if (PlayerManager.Instance) PlayerManager.Instance.AddCardToHand(card);

        // Give the card a random look: pick one texture from cardTextures and assign it to the
        // "_CardTexture" slot of both the front and back face materials.
        if (cardTextures != null && cardTextures.Count > 0)
            card.SetCardTexture(h.RandChoice(cardTextures));

        return card;
    }

    /// <summary>
    /// Called by <see cref="Card.OnPlace"/> every time a card lands on the table. Records the
    /// placed card, queues every reacting card and resolves the whole effect queue one card at
    /// a time, then advances the turn (see <see cref="OnCardPlacedCoroutine"/>).
    /// </summary>
    public void OnCardPlaced(Card placedCard)
    {
        DealFullHand();
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

    public void DealFullHand()
    {
        if (!PlayerManager.Instance) return;
        StartCoroutine(DealFullHandCoroutine());
    }

    /// <summary>
    /// Deals cards into the hand one at a time, waiting a very small <see cref="dealDelay"/>
    /// between each so they arrive sequentially instead of all at once.
    /// </summary>
    private IEnumerator DealFullHandCoroutine()
    {
        var wait = new WaitForSeconds(dealDelay);

        // Safety guard: if the hand can't be read/grown (e.g. no HandManager in scene), bail out
        // instead of spinning forever. Also cap iterations to handSize as a hard stop.
        for (int i = 0; i < PlayerManager.Instance.handSize; i++)
        {
            if (PlayerManager.Instance.Hand == null) yield break;
            if (PlayerManager.Instance.handSize - PlayerManager.Instance.Hand.Count <= 0) break;

            int before = PlayerManager.Instance.Hand.Count;
            DrawCard();
            // If a draw failed to add a card to the hand, stop rather than loop endlessly.
            if (PlayerManager.Instance.Hand.Count == before) break;

            yield return wait;   // small beat so cards are dealt one after another
        }
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

    /// <summary>
    /// Folds extra cards from the <see cref="additionalPiles"/> into both <see cref="fullPile"/>
    /// and the current <see cref="pile"/>. For each (suit -> count) entry, <c>count</c> random
    /// cards are pulled from the additional pile matching that suit and moved into play (removed
    /// from the additional pile so each extra card is only ever added once).
    ///
    /// If the suit's own additional pile is empty (or missing), a random non-empty additional
    /// pile is used instead. When every additional pile is empty, nothing happens.
    /// </summary>
    public void AddNewCards(Dictionary<CP.Suit, int> cardsToAdd, bool alert = true)
    {
        if (cardsToAdd == null) return;

        foreach (var kv in cardsToAdd)
        {
            for (int i = 0; i < kv.Value; i++)
            {
                ScriptableObjectContainer source = PickSourcePile(kv.Key);
                if (source == null) break;   // all additional piles empty — nothing left to add

                ScriptableObject card = h.RandChoice(source.scriptableObjects);
                source.scriptableObjects.Remove(card);

                if (fullPile) fullPile.scriptableObjects.Add(card);
                if (pile) pile.scriptableObjects.Add(card);
                
                h.Out(card);
            }
        }

        // bool alert functionality will be added later
    }

    /// <summary>
    /// Convenience overload of <see cref="AddNewCards(Dictionary{CP.Suit,int},bool)"/> that adds
    /// <see cref="primarySuitCardAdd"/> / <see cref="secondarySuitCardAdd"/> /
    /// <see cref="thirdSuitCardAdd"/> cards for the given suits. Any suit left null is skipped.
    /// </summary>
    public void AddNewCards(CP.Suit? primarySuit = null, CP.Suit? secondarySuit = null,
                            CP.Suit? thirdSuit = null, bool alert = true)
    {
        var cardsToAdd = new Dictionary<CP.Suit, int>();
        if (primarySuit.HasValue)   cardsToAdd[primarySuit.Value]   = primarySuitCardAdd;
        if (secondarySuit.HasValue) cardsToAdd[secondarySuit.Value] = secondarySuitCardAdd;
        if (thirdSuit.HasValue)     cardsToAdd[thirdSuit.Value]     = thirdSuitCardAdd;

        AddNewCards(cardsToAdd, alert);
    }

    /// <summary>
    /// Extends the pile based on which sins are most present on the table
    /// (<see cref="TableManager.suits"/>): the most-present suit adds <see cref="primarySuitCardAdd"/>
    /// cards, the second <see cref="secondarySuitCardAdd"/>, the third <see cref="thirdSuitCardAdd"/>.
    /// Ties are broken randomly, and suits that aren't present at all are ignored.
    /// </summary>
    public void ExtendPileAccordingToSins()
    {
        if (!TableManager.Instance) return;

        // Only rank suits that are actually present.
        var ranked = new List<KeyValuePair<CP.Suit, int>>();
        foreach (var kv in TableManager.Instance.suits)
            if (kv.Value > 0) ranked.Add(kv);

        // Shuffle first so equal counts end up in a random relative order, then sort by count
        // descending — that way ties are resolved randomly.
        for (int i = ranked.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (ranked[i], ranked[j]) = (ranked[j], ranked[i]);
        }
        ranked.Sort((a, b) => b.Value.CompareTo(a.Value));

        CP.Suit? primary   = ranked.Count > 0 ? ranked[0].Key : (CP.Suit?)null;
        CP.Suit? secondary = ranked.Count > 1 ? ranked[1].Key : (CP.Suit?)null;
        CP.Suit? third     = ranked.Count > 2 ? ranked[2].Key : (CP.Suit?)null;

        AddNewCards(primary, secondary, third);
    }

    /// <summary>
    /// Picks the additional pile to draw an extra card from: the pile matching <paramref name="suit"/>
    /// when it still has cards, otherwise a random non-empty additional pile. Returns null when
    /// every additional pile is empty.
    /// </summary>
    private ScriptableObjectContainer PickSourcePile(CP.Suit suit)
    {
        if (additionalPiles.TryGetValue(suit, out var matching)
            && matching && matching.scriptableObjects.Count > 0)
            return matching;

        var nonEmpty = new List<ScriptableObjectContainer>();
        foreach (var p in additionalPiles.Values)
            if (p && p.scriptableObjects.Count > 0) nonEmpty.Add(p);

        if (nonEmpty.Count == 0) return null;
        return h.RandChoice(nonEmpty);
    }
}
