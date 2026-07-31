using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Sets the target score of every level from how much the player's own deck is actually WORTH,
/// instead of from a hand-authored arithmetic curve.
///
/// The idea
/// --------
/// Before each round the manager runs a fast, head-less Monte-Carlo simulation of the round:
/// it deals a hand, plays cards in a RANDOM order until the whole deck is spent, and adds up the
/// VP exactly the way the real game would (suits accumulate on the table, countdowns tick, cards
/// read the table / the hand / the just-placed card, mixed conditions gate, destroy-effects strip
/// suits). Averaging many such runs gives:
///
///     average = what an average player who places cards at random scores with the WHOLE deck.
///
/// The target score is then set slightly ABOVE that number, so playing randomly is never enough —
/// the player has to sequence the deck better than chance:
///
///     targetScore(level) = average(level) * (1 + boost(level)) * difficultyMultiplier
///     boost(level)       = baseBoost + boostGrowth * (level - 1)          // 10%, 15%, 20% ...
///                        or, with compoundingBoost, (1+baseBoost) * (1+boostGrowth)^(level-1) - 1
///
/// Because the simulation replays the real rules, the quadratic growth comes out on its own: a
/// bigger deck means more cards played AND more suits piled on the table for each of them to score
/// off, so the average grows roughly with the square of the deck size. Nothing here hard-codes it.
///
/// Deck size model
/// ---------------
/// Balance is measured against the deck the player would own if they only ever WON — consolation
/// cards handed out after a loss must not raise the bar:
///
///     deckSize(level) = baseDeckSize + cardsPerWin * (level - 1)      // 28, 31, 34, ...
///
/// The cards themselves are the player's real ones (CardManager.fullPile); only the COUNT is
/// normalised to the formula above. If the real pile is bigger (loss cards, extra drafts) a random
/// subset of that size is simulated; if it is smaller, it is padded from the still-unused
/// additional piles. Set <see cref="normalizeDeckSizeToWinCount"/> to false to simulate the real
/// pile as-is.
///
/// Tuning
/// ------
/// <see cref="baseBoost"/> = how far above average level 1 sits, <see cref="boostGrowth"/> = how
/// much stricter every following level gets, <see cref="maxBoost"/> = where that stops,
/// <see cref="difficultyMultiplier"/> = one global knob over everything. Use the context menu
/// "Log Progression Curve" to print the whole curve without playing.
/// </summary>
public class ProgressionManager : MonoBehaviour
{
    public static ProgressionManager Instance;

    private const int SuitCount = 7;

    [Tooltip("Current level (starts at 1).")]
    public int level = 1;

    // ------------------------------------------------------------------ difficulty curve

    [Header("Target = deck average x (1 + boost)")]
    [Tooltip("How far above the average random full-deck score level 1 sits. 0.10 = 10% above.")]
    public float baseBoost = 0.10f;
    [Tooltip("How much the boost grows per level. 0.05 = 10%, 15%, 20%, 25% ... above average.")]
    public float boostGrowth = 0.05f;
    [Tooltip("OFF: the boost keeps growing forever and maxBoost is ignored.\n" +
             "ON: the boost stops growing once it reaches maxBoost.")]
    public bool limitMaxBoost = true;
    [Tooltip("Upper clamp on the boost so very late levels stay reachable. 1.5 = never more than " +
             "250% of the average random score. Only used when limitMaxBoost is ON.")]
    public float maxBoost = 1.5f;
    [Tooltip("OFF: boost = baseBoost + boostGrowth * (level-1)  ->  10%, 15%, 20% ...\n" +
             "ON:  boost compounds -> (1+baseBoost) * (1+boostGrowth)^(level-1) - 1.")]
    public bool compoundingBoost = false;
    [Tooltip("One global difficulty knob applied to the finished target. 0.9 = 10% easier for every level.")]
    public float difficultyMultiplier = 1f;

    // ------------------------------------------------------------------ deck size model

    [Header("Deck size model (wins only - loss cards are ignored)")]
    [Tooltip("Deck size the balance of level 1 is measured against. Default 28 = 4 x 7 cards.")]
    public int baseDeckSize = 28;
    [Tooltip("How many cards a win adds. The simulated deck of level N holds " +
             "baseDeckSize + cardsPerWin * (N-1) cards.")]
    public int cardsPerWin = 3;
    [Tooltip("ON (recommended): simulate the player's real cards but force the deck to the " +
             "win-only size above, so consolation cards from losses never raise the target.\n" +
             "OFF: simulate the real pile exactly as it is, losses included.")]
    public bool normalizeDeckSizeToWinCount = true;

    // ------------------------------------------------------------------ simulation

    [Header("Simulation")]
    [Tooltip("How many random play-throughs are averaged per level. More = steadier number, " +
             "slightly slower. 200 is plenty; anything under ~50 gets noisy.")]
    public int simulationRuns = 240;
    [Tooltip("Seed for the simulation. Reused for every level, so the curve is smooth and the " +
             "same build always produces the same targets.")]
    public int simulationSeed = 20260801;
    [Tooltip("Hand size used by the simulation. Read from PlayerManager at runtime; this is only " +
             "the fallback when no PlayerManager exists yet.")]
    public int fallbackHandSize = 6;

    [Header("Polish")]
    [Tooltip("Targets are rounded to a multiple of this so the goal reads as a round number. 0 = no rounding.")]
    public int roundScoreTo = 50;
    [Tooltip("Targets never drop below this.")]
    public int minTargetScore = 500;
    [Tooltip("ON: a level's target can never be lower than the previous level's (plus " +
             "minIncreasePerLevel), even if the simulation happens to come out lower.")]
    public bool enforceMonotonic = true;
    [Tooltip("Smallest allowed step between two consecutive levels when enforceMonotonic is ON.")]
    public int minIncreasePerLevel = 100;

    [Header("Fallback (no deck readable yet)")]
    [Tooltip("Target of level 1 when the simulation has no cards to work with (e.g. no CardManager " +
             "in the scene). The fallback grows quadratically with the deck size.")]
    public int fallbackInitialScore = 2500;

    [Header("Readout (filled after each calculation)")]
    [Tooltip("Average score a random full play-through of the simulated deck produced.")]
    public int lastAverageScore;
    [Tooltip("Boost that was applied on top of the average, as a fraction (0.15 = +15%).")]
    public float lastBoost;
    [Tooltip("Size of the deck the last calculation simulated.")]
    public int lastSimulatedDeckSize;

    // Targets already produced this session, used for the monotonic guard.
    private readonly Dictionary<int, int> _targets = new Dictionary<int, int>();

    // Scratch buffers reused across runs so the simulation allocates almost nothing.
    private readonly List<CardDataBase> _sourceDeck = new List<CardDataBase>();
    private readonly List<CardDataBase> _extraPool = new List<CardDataBase>();
    private readonly List<CardDataBase> _work = new List<CardDataBase>();
    private readonly List<CardDataBase> _deck = new List<CardDataBase>();
    private readonly List<CardDataBase> _hand = new List<CardDataBase>();
    private readonly List<SimCard> _table = new List<SimCard>();
    private readonly List<SimCard> _snapshot = new List<SimCard>();
    private readonly int[] _suits = new int[SuitCount];
    private readonly int[] _required = new int[SuitCount];
    private readonly int[] _handSuits = new int[SuitCount];

    private void Awake()
    {
        h.CreateStaticInstance(this, ref Instance);
    }

    // ------------------------------------------------------------------ public API

    /// <summary>
    /// Computes the target score for <paramref name="levelIn"/> from a simulation of the deck and
    /// pushes it onto <see cref="TableManager.targetScore"/>.
    /// </summary>
    public int SetScore(int levelIn)
    {
        level = levelIn;

        int score = CalculateTargetScore(levelIn);

        // overrideScore == true means the designer pins targetScore in the inspector, so the
        // progression only writes it when overrideScore is false.
        if (TableManager.Instance && !TableManager.Instance.overrideScore)
        {
            TableManager.Instance.targetScore = score;
            TableManager.Instance.ResetScoreForRound();
        }

        h.Out("ProgressionManager: level", levelIn,
              "| deck", lastSimulatedDeckSize,
              "| random-play average", lastAverageScore,
              "| boost", Mathf.RoundToInt(lastBoost * 100f) + "%",
              "| target", score);

        return score;
    }

    /// <summary>Advances to the next level and applies its target score.</summary>
    public int NextLevel() => SetScore(level + 1);

    /// <summary>Re-applies the current level's target score (used when a lost round is retried).</summary>
    public int CurrentLevel() => SetScore(level);

    /// <summary>
    /// The finished target score of a level: the simulated random-play average, lifted by the
    /// level's boost, scaled by <see cref="difficultyMultiplier"/>, rounded and floored. Does not
    /// touch <see cref="TableManager"/>, so it is safe to call for previewing a curve.
    /// </summary>
    public int CalculateTargetScore(int levelIn)
    {
        float average = SimulateAverageScore(levelIn);
        float boost = BoostForLevel(levelIn);

        lastAverageScore = Mathf.RoundToInt(average);
        lastBoost = boost;

        float raw = average * (1f + boost) * Mathf.Max(0.01f, difficultyMultiplier);

        int score = RoundTo(raw, roundScoreTo);
        score = Mathf.Max(score, minTargetScore);

        // Never let a level be easier than the one before it, whatever the simulation says.
        if (enforceMonotonic && _targets.TryGetValue(levelIn - 1, out int previous))
            score = Mathf.Max(score, previous + Mathf.Max(0, minIncreasePerLevel));

        _targets[levelIn] = score;
        return score;
    }

    /// <summary>
    /// How far above the random-play average this level's target sits, as a fraction
    /// (0.15 = +15%). Level 1 gets <see cref="baseBoost"/>; every level after that adds
    /// <see cref="boostGrowth"/> (added, or compounded when <see cref="compoundingBoost"/> is on).
    /// The result is capped at <see cref="maxBoost"/> only while <see cref="limitMaxBoost"/> is on —
    /// with it off the boost keeps climbing for as long as the player survives.
    /// </summary>
    public float BoostForLevel(int levelIn)
    {
        int steps = Mathf.Max(0, levelIn - 1);

        float boost = compoundingBoost
            ? (1f + baseBoost) * Mathf.Pow(1f + boostGrowth, steps) - 1f
            : baseBoost + boostGrowth * steps;

        boost = Mathf.Max(0f, boost);

        return limitMaxBoost ? Mathf.Min(boost, maxBoost) : boost;
    }

    /// <summary>Deck size the balance of <paramref name="levelIn"/> is measured against (wins only).</summary>
    public int DeckSizeForLevel(int levelIn) =>
        Mathf.Max(1, baseDeckSize + cardsPerWin * Mathf.Max(0, levelIn - 1));

    /// <summary>
    /// Average score of a random full play-through of the level's deck, over
    /// <see cref="simulationRuns"/> simulated rounds. Falls back to a quadratic estimate when
    /// there are no cards to read.
    /// </summary>
    public float SimulateAverageScore(int levelIn)
    {
        GatherCardSources();

        int deckSize = normalizeDeckSizeToWinCount || _sourceDeck.Count == 0
            ? DeckSizeForLevel(levelIn)
            : _sourceDeck.Count;
        lastSimulatedDeckSize = deckSize;

        if (_sourceDeck.Count == 0)
            return FallbackAverage(deckSize);

        int handSize = PlayerManager.Instance ? PlayerManager.Instance.handSize : fallbackHandSize;
        handSize = Mathf.Max(1, handSize);

        // The same seed for every level keeps the curve smooth: consecutive levels see the same
        // random stream, so the difference between them is the deck, not the noise.
        System.Random rng = new System.Random(simulationSeed);

        int runs = Mathf.Max(1, simulationRuns);
        double total = 0;
        for (int i = 0; i < runs; i++)
        {
            BuildSimulatedDeck(deckSize, rng);
            total += SimulateOneRound(handSize, rng);
        }

        return (float)(total / runs);
    }

    // ------------------------------------------------------------------ deck building

    /// <summary>
    /// Reads the player's real cards off <see cref="CardManager"/>: <see cref="_sourceDeck"/> is
    /// the deck they play with, <see cref="_extraPool"/> is everything still sitting in the
    /// additional piles (used only to pad an undersized deck up to the win-only size).
    /// </summary>
    private void GatherCardSources()
    {
        _sourceDeck.Clear();
        _extraPool.Clear();

        CardManager manager = CardManager.Instance;
        if (!manager) return;

        if (manager.fullPile && manager.fullPile.scriptableObjects != null)
            foreach (ScriptableObject so in manager.fullPile.scriptableObjects)
                if (so is CardDataBase card) _sourceDeck.Add(card);

        if (manager.additionalPiles != null)
            foreach (var kv in manager.additionalPiles)
                if (kv.Value && kv.Value.scriptableObjects != null)
                    foreach (ScriptableObject so in kv.Value.scriptableObjects)
                        if (so is CardDataBase card) _extraPool.Add(card);
    }

    /// <summary>
    /// Fills <see cref="_deck"/> with <paramref name="deckSize"/> shuffled cards: a random subset
    /// of the real deck when it is big enough, otherwise the whole real deck padded out from the
    /// unused additional piles (and, as a last resort, by repeating real cards).
    /// </summary>
    private void BuildSimulatedDeck(int deckSize, System.Random rng)
    {
        _deck.Clear();

        _work.Clear();
        _work.AddRange(_sourceDeck);
        Shuffle(_work, rng);

        if (_work.Count >= deckSize)
        {
            for (int i = 0; i < deckSize; i++) _deck.Add(_work[i]);
            return;
        }

        _deck.AddRange(_work);

        // Not enough real cards yet: pad with cards the player could still be offered.
        if (_extraPool.Count > 0)
        {
            _work.Clear();
            _work.AddRange(_extraPool);
            Shuffle(_work, rng);
            for (int i = 0; i < _work.Count && _deck.Count < deckSize; i++)
                _deck.Add(_work[i]);
        }

        // Every pile exhausted — repeat cards so the deck still reaches the modelled size.
        while (_deck.Count < deckSize && _sourceDeck.Count > 0)
            _deck.Add(_sourceDeck[rng.Next(_sourceDeck.Count)]);
    }

    private static void Shuffle(List<CardDataBase> list, System.Random rng)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    /// <summary>
    /// Rough stand-in used when no cards can be read at all. Grows with the square of the deck
    /// size, matching the way the real average behaves (more cards played x more suits to score off).
    /// </summary>
    private float FallbackAverage(int deckSize)
    {
        float ratio = (float)deckSize / Mathf.Max(1, baseDeckSize);
        return fallbackInitialScore * ratio * ratio;
    }

    private static int RoundTo(float value, int step)
    {
        if (step <= 1) return Mathf.RoundToInt(value);
        return Mathf.RoundToInt(value / step) * step;
    }

    // ------------------------------------------------------------------ the simulation itself

    /// <summary>A card sitting on the table during a simulated round, with its live countdown.</summary>
    private class SimCard
    {
        public CardDataBase data;
        public int countdown;
    }

    /// <summary>
    /// Plays one whole round at random and returns the score. Mirrors the real turn exactly:
    /// place a card -> its suits land on the table -> the hand refills -> effects resolve in the
    /// order CardManager uses (cards reacting to a placed card, then the placed card's own burn
    /// effect, then every per-turn card) -> countdowns tick and finished cards leave.
    /// </summary>
    private int SimulateOneRound(int handSize, System.Random rng)
    {
        _hand.Clear();
        _table.Clear();
        for (int i = 0; i < SuitCount; i++) _suits[i] = 0;

        int score = 0;
        int deckIndex = 0;

        deckIndex = Refill(_hand, deckIndex, handSize);

        // Hard stop: a round can never last more than one turn per card in the deck plus the hand.
        int safety = _deck.Count + handSize + 8;

        while (_hand.Count > 0 && safety-- > 0)
        {
            // --- play a random card out of the hand ---
            int index = rng.Next(_hand.Count);
            CardDataBase placed = _hand[index];
            _hand.RemoveAt(index);

            SimCard placedCard = new SimCard { data = placed, countdown = placed ? placed.countdown : 0 };
            _table.Add(placedCard);

            // Suits are contributed once, at placement (Card.OnPlace).
            if (placed != null && placed.suits != null)
                foreach (CP.Suit suit in placed.suits)
                    _suits[(int)suit]++;

            // CardManager.OnCardPlaced refills the hand before the effects resolve.
            deckIndex = Refill(_hand, deckIndex, handSize);

            bool burnPlaced = false;

            // 1. Cards reacting to another card being placed (never the placed card itself).
            _snapshot.Clear();
            _snapshot.AddRange(_table);
            foreach (SimCard card in _snapshot)
            {
                if (card == placedCard || card.countdown <= 0 || card.data == null) continue;
                if (!_table.Contains(card)) continue;
                if (ActivationOf(card.data) == CP.ActivateCond.OtherCardPlaced)
                    score += ResolveVp(card.data, placed);
            }

            // 2. The placed card's own effect, when it resolves on placement.
            if (placed != null && ActivationOf(placed) == CP.ActivateCond.Burn)
            {
                score += ResolveVp(placed, placed);
                burnPlaced = true;   // a Burn card always burns right after it resolves
            }

            // 3. Every card whose effect fires each turn (OnTurnEnd and OnTurnStart are one phase).
            _snapshot.Clear();
            _snapshot.AddRange(_table);
            foreach (SimCard card in _snapshot)
            {
                if (card.countdown <= 0 || card.data == null) continue;
                if (!_table.Contains(card)) continue;
                CP.ActivateCond activation = ActivationOf(card.data);
                if (activation == CP.ActivateCond.OnTurnEnd || activation == CP.ActivateCond.OnTurnStart)
                    score += ResolveVp(card.data, placed);
            }

            // --- countdown phase ---
            // The placed card does not tick on the turn it was placed: it either burns at once
            // (countdown 0 / burn effect) or starts counting from the next turn.
            if (burnPlaced || placedCard.countdown <= 0)
                _table.Remove(placedCard);

            for (int i = _table.Count - 1; i >= 0; i--)
            {
                if (_table[i] == placedCard) continue;
                _table[i].countdown--;
                if (_table[i].countdown <= 0) _table.RemoveAt(i);
            }
        }

        return score;
    }

    /// <summary>Draws from the deck into the hand until it is full or the deck runs out.</summary>
    private int Refill(List<CardDataBase> hand, int deckIndex, int handSize)
    {
        while (hand.Count < handSize && deckIndex < _deck.Count)
            hand.Add(_deck[deckIndex++]);
        return deckIndex;
    }

    /// <summary>
    /// CardDataOnPlaceEffect forces its activation to OtherCardPlaced in OnEnable, so the field on
    /// the asset is not always what actually runs. This returns what the game really uses.
    /// </summary>
    private static CP.ActivateCond ActivationOf(CardDataBase data)
    {
        if (data == null) return CP.ActivateCond.Burn;
        return data is CardDataOnPlaceEffect ? CP.ActivateCond.OtherCardPlaced : data.activation;
    }

    /// <summary>
    /// The simulation's copy of <see cref="CardDataBase.GenerateVP"/> /
    /// <see cref="CardDataOnPlaceEffect.GenerateVP"/> — same branches, same quirks (a base card
    /// with condition Multiple scores nothing, an on-place card with FixedVp scores nothing, only
    /// base cards run their destroy list).
    /// </summary>
    private int ResolveVp(CardDataBase data, CardDataBase placed)
    {
        if (data == null) return 0;

        if (data is CardDataOnPlaceEffect)
        {
            if (placed == null) return 0;

            switch (data.condition)
            {
                case CP.Condition.SuitSet:
                    return VpForSuitSets(data, SourceSuitsForOnPlace(data, placed));

                case CP.Condition.SuitCount:
                    return VpForSuitCountOfPlaced(data, placed);

                case CP.Condition.Multiple:
                    // The suit-count part is the gate; the suit-set part is the payout.
                    return VpForSuitCountOfPlaced(data, placed) > 0
                        ? VpForSuitSets(data, SourceSuitsForOnPlace(data, placed))
                        : 0;

                default:
                    return 0;
            }
        }

        int vp = 0;

        switch (data.condition)
        {
            case CP.Condition.SuitSet:
                vp = VpForSuitSets(data, GatherSourceSuits(data));
                break;

            case CP.Condition.FixedVp:
                vp = data.vpPerSet;
                break;

            case CP.Condition.SuitCount:
                vp = VpForSuitCountOfSource(data);
                break;
        }

        // Base cards strip the suits they destroy off the table (the on-place subclass does not).
        if (data.suitsToDestroy != null)
            foreach (CP.Suit suit in data.suitsToDestroy)
                _suits[(int)suit] = Mathf.Max(0, _suits[(int)suit] - 1);

        return vp;
    }

    /// <summary>Suit counts a normal card reads: the table, the hand, or nothing.</summary>
    private int[] GatherSourceSuits(CardDataBase data)
    {
        if (data.targetSource == CP.TargetSource.Table)
            return _suits;

        for (int i = 0; i < SuitCount; i++) _handSuits[i] = 0;

        // TargetSource.PlacedCard is not handled by the base class, so it reads as all-zero.
        if (data.targetSource == CP.TargetSource.Hand)
            foreach (CardDataBase card in _hand)
                if (card != null && card.suits != null)
                    foreach (CP.Suit suit in card.suits)
                        _handSuits[(int)suit]++;

        return _handSuits;
    }

    /// <summary>Suit counts an on-place card reads: the just-placed card, or the normal sources.</summary>
    private int[] SourceSuitsForOnPlace(CardDataBase data, CardDataBase placed)
    {
        if (data.targetSource != CP.TargetSource.PlacedCard)
            return GatherSourceSuits(data);

        for (int i = 0; i < SuitCount; i++) _handSuits[i] = 0;
        if (placed != null && placed.suits != null)
            foreach (CP.Suit suit in placed.suits)
                _handSuits[(int)suit]++;

        return _handSuits;
    }

    /// <summary>
    /// How many complete copies of the card's suit set the source holds, times vpPerSet — the
    /// simulation's copy of <see cref="CardDataBase.CalculateVpForSuitSets"/>.
    /// </summary>
    private int VpForSuitSets(CardDataBase data, int[] sourceSuits)
    {
        if (data.suitSet == null || data.suitSet.Count == 0) return 0;

        for (int i = 0; i < SuitCount; i++) _required[i] = 0;
        foreach (CP.Suit suit in data.suitSet) _required[(int)suit]++;

        int sets = int.MaxValue;
        for (int i = 0; i < SuitCount; i++)
        {
            if (_required[i] == 0) continue;
            int possible = sourceSuits[i] / _required[i];
            if (possible < sets) sets = possible;
        }

        return sets == int.MaxValue ? 0 : sets * data.vpPerSet;
    }

    /// <summary>Suit-count payout read off the table or the hand (the card's target source).</summary>
    private int VpForSuitCountOfSource(CardDataBase data)
    {
        int matches = 0;

        if (data.targetSource == CP.TargetSource.Table)
        {
            if (_table.Count == 0) return 0;
            foreach (SimCard card in _table)
                if (MatchesSuitCount(data, card.data)) matches++;
        }
        else if (data.targetSource == CP.TargetSource.Hand)
        {
            if (_hand.Count == 0) return 0;
            foreach (CardDataBase card in _hand)
                if (MatchesSuitCount(data, card)) matches++;
        }
        else
        {
            // PlacedCard is not gathered by the base class: no source cards, no points.
            return 0;
        }

        return data.vpPerSet * matches;
    }

    /// <summary>Suit-count payout read off the single just-placed card (on-place cards).</summary>
    private int VpForSuitCountOfPlaced(CardDataBase data, CardDataBase placed)
    {
        if (placed == null) return 0;

        return MatchesSuitCount(data, placed) ? data.vpPerSet : 0;
    }

    /// <summary>
    /// Whether <paramref name="card"/> satisfies <paramref name="data"/>'s suit-count condition:
    /// exactly suitCount suits when fixedCount is on, at most suitCount when it is off (matching
    /// <see cref="CardDataBase.CalculateVpForSuitCount"/>).
    /// </summary>
    private static bool MatchesSuitCount(CardDataBase data, CardDataBase card)
    {
        if (card == null || card.suits == null) return false;

        int delta = data.suitCount - card.suits.Count;
        return data.fixedCount ? delta == 0 : delta >= 0;
    }

    // ------------------------------------------------------------------ tooling

    /// <summary>
    /// Prints the whole curve — deck size, simulated random-play average, boost and final target —
    /// for the first levels, without playing a single round. Run it from the component's context
    /// menu while in Play mode (the deck is only readable once CardManager is awake).
    /// </summary>
    [ContextMenu("Log Progression Curve")]
    public void LogProgressionCurve()
    {
        var previous = new Dictionary<int, int>(_targets);
        _targets.Clear();

        for (int levelIn = 1; levelIn <= 12; levelIn++)
        {
            int target = CalculateTargetScore(levelIn);
            h.Out("level", levelIn,
                  "| deck", lastSimulatedDeckSize,
                  "| average", lastAverageScore,
                  "| boost", Mathf.RoundToInt(lastBoost * 100f) + "%",
                  "| target", target);
        }

        _targets.Clear();
        foreach (var kv in previous) _targets[kv.Key] = kv.Value;
    }

    /// <summary>Forgets every target computed so far, so the monotonic guard starts clean.</summary>
    public void ResetProgression()
    {
        _targets.Clear();
        level = 1;
    }
}
