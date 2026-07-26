using System.Collections;
using System.Collections.Generic;
using PrimeTween;
using UnityEngine;

public class CardManager : MonoBehaviour
{
    
    public bool startRoundOnStart = false;
    public static CardManager Instance;
    public Transform cardsParent;
    public Transform cardsSpawnPoint;
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

    [Header("Losing")]
    [Tooltip("When ON, an empty pile is NOT reshuffled: the player can no longer draw, and once the " +
             "hand is empty too the round is lost (OnLoss runs). When OFF, the pile reshuffles as before.")]
    public bool canLoose = true;
    [Tooltip("Dialogues played when the player loses. One is chosen at random in OnLoss.")]
    [SerializeField] private List<DialogueContainer> lossDialogues = new List<DialogueContainer>();
    [Tooltip("Small delay (seconds) before the loss dialogue starts.")]
    [SerializeField] private float lossDelay = 1f;

    // Set true the moment a loss is triggered so OnLoss only ever runs once per round; cleared in ResetRound.
    private bool lost = false;

    [Header("Add cards (folding extra cards in from additional piles)")]
    [Tooltip("How many cards the most-present / primary suit adds when extending the pile.")]
    [SerializeField] private int primarySuitCardAdd = 4;
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

    [Header("Add-cards preview alert")]
    [Tooltip("Uniform scale multiplier applied to every card shown in the full-screen 'cards added' " +
             "preview. The cards are first sized to cover the camera, then multiplied by this.")]
    public float CardPreviewScale = 0.8f;
    [Tooltip("Distance (world units) in front of the camera the preview cards are laid out at.")]
    [SerializeField] private float previewDistance = 2f;
    [Tooltip("Small delay between each preview card starting to burn, so they clear in a cascade.")]
    [SerializeField] private float previewBurnStagger = 0.05f;
    [Tooltip("Scale multiplier applied to a preview card while the mouse hovers over it (1 = no pop).")]
    [SerializeField] private float previewHoverScale = 1.15f;
    [Tooltip("How far (world units) a hovered preview card is pulled toward the camera so it pops " +
             "in front of its neighbours instead of z-fighting with them.")]
    [SerializeField] private float previewHoverRaise = 0.15f;
    [Tooltip("Smoothing (seconds-ish) for the hover pop. Smaller = snappier. 0 = instant.")]
    [SerializeField] private float previewHoverSmoothing = 0.08f;

    [Header("Card drafting")]
    [Tooltip("When ON, ExtendPileAccordingToSins lets the player CLICK draftCardCount cards to keep " +
             "(the rest burn) instead of folding every card straight into the piles. When OFF, the " +
             "old behaviour runs unchanged.")]
    [SerializeField] private bool cardDrafting = true;
    [Tooltip("How many cards the player picks during a draft before the remaining candidates burn.")]
    [SerializeField] private int draftCardCount = 5;
    [Tooltip("Duration (seconds) of the slide a chosen card makes toward the spawn point before it " +
             "is destroyed at once (no burn). 0 = snap instantly.")]
    [SerializeField] private float draftPickMoveDuration = 0.25f;
    [Tooltip("Dialogue played right before the player is offered cards to choose in " +
             "ExtendPileAccordingToSins. The cards are only shown once this dialogue finishes.")]
    [SerializeField] private DialogueContainer Pick5Container;

    
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
            // h.Out(additionalPiles);
            foreach (var additionalPile in additionalPiles.Values)
            {
                if (!additionalPile || additionalPile.scriptableObjects == null) continue;
                fullPile.scriptableObjects.AddRange(additionalPile.scriptableObjects);
            }
        }

        AddNewCards(
            new Dictionary<CP.Suit, int>()
            {
                {CP.Suit.Envy, 1},
                {CP.Suit.Pride, 1},
                {CP.Suit.Lust, 1},
                {CP.Suit.Sloth, 1},
                {CP.Suit.Greed, 1},
                {CP.Suit.Gluttony, 1},
                {CP.Suit.Wrath, 1},
            },
            alert:false
            );
        
        if (startRoundOnStart) RoundStart();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.R))
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
        TableManager.Instance.ResetSuits();
        pile = fullPile ? Instantiate(fullPile) : null;
        if (!gameStarted)
        {
            ProgressionManager.Instance.level -= 1;
            gameStarted = true;
        }
        h.Out(gameStarted);
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
            // Losing enabled: an empty pile is NEVER reshuffled. The player simply can't draw any
            // more; the loss itself is detected once the hand is empty too (see CheckForLoss).
            if (canLoose)
            {
                h.Out("CardManager: pile empty and canLoose is on — no reshuffle, cannot draw.");
                return null;
            }

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

        Card card = Instantiate(cardPrefab, cardsSpawnPoint.transform.position, Quaternion.identity,cardsParent);
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

        // With losing enabled, the round is lost once the pile is empty and the hand couldn't be
        // refilled — i.e. the player has no cards left to draw or play.
        CheckForLoss();
    }

    /// <summary>
    /// When <see cref="canLoose"/> is on, triggers the loss (<see cref="OnLoss"/>) if the player is
    /// out of cards: the draw <see cref="pile"/> is empty AND the hand is empty. Fires at most once
    /// per round (guarded by <see cref="lost"/>, cleared in <see cref="ResetRound"/>).
    /// </summary>
    private void CheckForLoss()
    {
        if (!canLoose || lost) return;
        if (pile && pile.scriptableObjects.Count > 0) return;   // still cards left to draw
        if (PlayerManager.Instance && PlayerManager.Instance.Hand != null
            && PlayerManager.Instance.Hand.Count > 0) return;   // still cards left to play

        lost = true;
        StartCoroutine(OnLoss());
    }

    /// <summary>
    /// Loss flow: waits a small <see cref="lossDelay"/>, plays a random dialogue from
    /// <see cref="lossDialogues"/> (same StartDialogue / onDialogueEnd pattern as
    /// <see cref="SinCutsceneBase.DialogueStart"/>), waits for it to close, then restarts the SAME
    /// level via <see cref="ResetRound"/>. Card dragging is locked out while the dialogue plays.
    /// </summary>
    private IEnumerator OnLoss()
    {
        h.Out("CardManager: player lost — no cards left to draw or play.");

        // Lock out card interaction while the loss dialogue plays (re-enabled before the reset).
        if (CardDragController.Instance) CardDragController.Instance.SetDraggingEnabled(false);

        yield return new WaitForSeconds(lossDelay);

        if (lossDialogues != null && lossDialogues.Count > 0 && DialogueManager.Instance)
        {
            DialogueContainer chosen = h.RandChoice(lossDialogues);

            bool dialogueDone = false;
            void OnDialogueEnd()
            {
                dialogueDone = true;
                DialogueManager.Instance.onDialogueEnd.RemoveListener(OnDialogueEnd);
            }

            DialogueManager.Instance.onDialogueEnd.AddListener(OnDialogueEnd);
            DialogueManager.Instance.StartDialogue(chosen);

            // Wait for the dialogue to finish before resetting the round.
            while (!dialogueDone) yield return null;
        }

        // Hand card interaction back to the player, then replay the current level.
        if (CardDragController.Instance) CardDragController.Instance.SetDraggingEnabled(true);

        ResetRound();
    }

    /// <summary>
    /// Restarts the CURRENT round after a loss. Works exactly like <see cref="RoundStart"/> —
    /// rebuilding <see cref="pile"/> as a fresh copy of <see cref="fullPile"/> and dealing a full
    /// hand — except it re-applies the current level (<see cref="ProgressionManager.CurrentLevel"/>)
    /// instead of advancing to the next one (<see cref="ProgressionManager.NextLevel"/>).
    /// </summary>
    public void ResetRound()
    {
        h.Out("Deal cards (reset round after loss)");
        TableManager.Instance.ResetSuits();
        pile = fullPile ? Instantiate(fullPile) : null;
        lost = false;
        if (!gameStarted)
        {
            ProgressionManager.Instance.level -= 1;
            gameStarted = true;
        }
        h.Out(gameStarted);
        ProgressionManager.Instance.CurrentLevel();
        DealFullHand();
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
    public IEnumerator AddNewCards(Dictionary<CP.Suit, int> cardsToAdd, bool alert = true)
    {
        if (cardsToAdd == null) yield break;

        // Remember exactly which cards were folded in this call so the preview can show them.
        List<CardDataBase> addedCards = new List<CardDataBase>();

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

                if (card is CardDataBase cardData) addedCards.Add(cardData);

                h.Out(card);
            }
        }

        // When alerting, show every added card filling the screen and wait for the player to
        // dismiss them (click / Enter / Space), then burn them all away.
        if (alert && addedCards.Count > 0)
            yield return ShowAddedCardsAlert(addedCards);
    }

    /// <summary>
    /// Instantiates one preview card per newly added card, lays them out in a grid that completely
    /// covers the camera view, and scales each by <see cref="CardPreviewScale"/>. Waits for the
    /// player to click / press Enter / press Space, then burns every preview card and waits for the
    /// burns to finish before returning. These preview cards are display-only: they are NOT added
    /// to the hand, to <see cref="Cards"/>, or to any pile.
    /// </summary>
    private IEnumerator ShowAddedCardsAlert(List<CardDataBase> addedCards)
    {
        Camera cam = Camera.main ? Camera.main : FindFirstObjectByType<Camera>();
        if (!cam || !pfbTest) yield break;

        // Grid dimensions: roughly square, biased by the camera aspect so it fills the frame.
        int count = addedCards.Count;
        float aspect = cam.aspect <= 0f ? 1.7778f : cam.aspect;
        int cols = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(count * aspect)));
        int rows = Mathf.Max(1, Mathf.CeilToInt((float)count / cols));

        // Size of the camera frustum at previewDistance, so the grid spans the whole view.
        float frustumHeight = 2f * previewDistance * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float frustumWidth = frustumHeight * aspect;
        float cellWidth = frustumWidth / cols;
        float cellHeight = frustumHeight / rows;

        Transform camT = cam.transform;
        Vector3 gridCenter = camT.position + camT.forward * previewDistance;
        // Card front normal is +Z: aim it back toward the camera so the face is visible.
        Quaternion faceRot = Quaternion.LookRotation(camT.forward, camT.up);

        List<Card> previewCards = new List<Card>();
        List<Transform> previewHolders = new List<Transform>();
        // The resting scale / position of each holder, so the hover pop can ease back to them.
        List<float> holderBaseScale = new List<float>();
        List<Vector3> holderBasePos = new List<Vector3>();
        for (int i = 0; i < count; i++)
        {
            int row = i / cols;
            int col = i % cols;

            // How many cards on this (possibly last, partially filled) row, so it stays centered.
            int cardsInRow = Mathf.Min(cols, count - row * cols);

            float x = (col - (cardsInRow - 1) * 0.5f) * cellWidth;
            float y = ((rows - 1) * 0.5f - row) * cellHeight;
            Vector3 pos = gridCenter + camT.right * x + camT.up * y;

            // The card is parented under a holder that carries the position, facing AND the preview
            // scale. Scaling the holder (not the card root) is essential: the card prefab runs its own
            // ScalingAnimation/SquishAnimation on Start, which capture and overwrite the card root's
            // localScale every frame — so any scale set on the card itself is immediately reset (the
            // "local scale doesn't change" symptom). The holder is above that, so it always applies.
            Transform holder = new GameObject("CardPreviewHolder").transform;
            holder.SetParent(cardsParent, false);
            holder.SetPositionAndRotation(pos, faceRot);

            Card card = Instantiate(pfbTest, holder, false);
            card.transform.localPosition = Vector3.zero;
            card.transform.localRotation = Quaternion.identity;
            // Display-only: keep it off the table/hand logic and unpickable.
            card.SetState(Card.CardState.OnTable);
            card.Lock();
            card.cardData = addedCards[i];
            if (cardTextures != null && cardTextures.Count > 0)
                card.SetCardTexture(h.RandChoice(cardTextures));

            // Fit the card to its cell so the grid covers the camera, then shrink by CardPreviewScale.
            float fit = FitScaleToCell(card, cellWidth, cellHeight, camT.right, camT.up);
            float baseScale = fit * Mathf.Max(0f, CardPreviewScale);
            holder.localScale = Vector3.one * baseScale;

            previewCards.Add(card);
            previewHolders.Add(holder);
            holderBaseScale.Add(baseScale);
            holderBasePos.Add(pos);
        }

        // Wait for the player to dismiss the preview. While waiting, pop whichever card the mouse is
        // hovering (scale it up and pull it toward the camera so it reads on top of its neighbours).
        yield return new WaitForEndOfFrame();   // ignore the click/press that opened this frame
        while (!DismissPreviewPressed())
        {
            int hoveredIndex = PreviewCardUnderMouse(cam, previewCards);

            for (int i = 0; i < previewHolders.Count; i++)
            {
                Transform holder = previewHolders[i];
                if (!holder) continue;

                bool isHovered = i == hoveredIndex;
                float targetScale = holderBaseScale[i] * (isHovered ? previewHoverScale : 1f);
                Vector3 targetPos = isHovered
                    ? holderBasePos[i] - camT.forward * previewHoverRaise   // toward the camera
                    : holderBasePos[i];

                if (previewHoverSmoothing <= 0f)
                {
                    holder.localScale = Vector3.one * targetScale;
                    holder.position = targetPos;
                }
                else
                {
                    float t = 1f - Mathf.Exp(-Time.deltaTime / previewHoverSmoothing);
                    holder.localScale = Vector3.Lerp(holder.localScale, Vector3.one * targetScale, t);
                    holder.position = Vector3.Lerp(holder.position, targetPos, t);
                }
            }

            yield return null;
        }

        // Burn them all, staggered, and wait for every burn to finish.
        int running = 0;
        foreach (Card card in previewCards)
        {
            if (!card) continue;
            running++;
            card.StartCoroutine(card.Burn(() => running--));
            if (previewBurnStagger > 0f) yield return new WaitForSeconds(previewBurnStagger);
        }
        while (running > 0) yield return null;

        // Clean up the now-empty holders left behind after each card destroyed itself.
        foreach (Transform holder in previewHolders)
            if (holder) Destroy(holder.gameObject);
    }

    /// <summary>
    /// Raycasts from <paramref name="cam"/> through the mouse and returns the index (into
    /// <paramref name="previewCards"/>) of the nearest preview card under the pointer, or -1 if none.
    /// Uses RaycastAll so a card behind another still resolves to the closest one, and matches hits
    /// back to the preview list (ignoring any other colliders in the scene).
    /// </summary>
    private static int PreviewCardUnderMouse(Camera cam, List<Card> previewCards)
    {
        if (!cam) return -1;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        RaycastHit[] hits = Physics.RaycastAll(ray, 1000f, ~0, QueryTriggerInteraction.Ignore);
        if (hits.Length == 0) return -1;

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (RaycastHit hit in hits)
        {
            Card card = hit.collider.GetComponentInParent<Card>();
            if (!card) continue;
            int idx = previewCards.IndexOf(card);
            if (idx >= 0) return idx;   // nearest preview card wins
        }
        return -1;
    }

    /// <summary>True the moment the player clicks or presses Enter / Space to dismiss the preview.</summary>
    private static bool DismissPreviewPressed()
    {
        return Input.GetMouseButtonDown(0)
               || Input.GetKeyDown(KeyCode.Return)
               || Input.GetKeyDown(KeyCode.KeypadEnter)
               || Input.GetKeyDown(KeyCode.Space);
    }

    /// <summary>
    /// Returns the uniform scale factor that makes <paramref name="card"/>'s rendered size fit inside
    /// a <paramref name="cellWidth"/> x <paramref name="cellHeight"/> cell (keeping aspect). Width and
    /// height are measured by projecting the card's combined world bounds onto the camera's right / up
    /// axes, so the fit stays correct no matter how the camera is oriented. Applied to the card's
    /// holder rather than the card itself (the card animates its own localScale). Returns 1 if the
    /// card can't be measured.
    /// </summary>
    private static float FitScaleToCell(Card card, float cellWidth, float cellHeight,
                                        Vector3 camRight, Vector3 camUp)
    {
        if (!card) return 1f;

        Bounds? combined = null;
        foreach (Renderer r in card.GetComponentsInChildren<Renderer>(true))
        {
            if (!r) continue;
            if (combined == null) combined = r.bounds;
            else { Bounds b = combined.Value; b.Encapsulate(r.bounds); combined = b; }
        }
        if (combined == null) return 1f;

        // Project the world AABB extents onto the camera axes to get the card's on-screen size.
        Vector3 e = combined.Value.extents;
        float width  = 2f * (Mathf.Abs(e.x * camRight.x) + Mathf.Abs(e.y * camRight.y) + Mathf.Abs(e.z * camRight.z));
        float height = 2f * (Mathf.Abs(e.x * camUp.x)    + Mathf.Abs(e.y * camUp.y)    + Mathf.Abs(e.z * camUp.z));
        if (width <= 1e-5f || height <= 1e-5f) return 1f;

        return Mathf.Min(cellWidth / width, cellHeight / height);
    }

    /// <summary>
    /// Convenience overload of <see cref="AddNewCards(Dictionary{CP.Suit,int},bool)"/> that adds
    /// <see cref="primarySuitCardAdd"/> / <see cref="secondarySuitCardAdd"/> /
    /// <see cref="thirdSuitCardAdd"/> cards for the given suits. Any suit left null is skipped.
    /// </summary>
    public IEnumerator AddNewCards(CP.Suit? primarySuit = null, CP.Suit? secondarySuit = null,
                            CP.Suit? thirdSuit = null, bool alert = true)
    {
        var cardsToAdd = new Dictionary<CP.Suit, int>();
        if (primarySuit.HasValue)   cardsToAdd[primarySuit.Value]   = primarySuitCardAdd;
        if (secondarySuit.HasValue) cardsToAdd[secondarySuit.Value] = secondarySuitCardAdd;
        if (thirdSuit.HasValue)     cardsToAdd[thirdSuit.Value]     = thirdSuitCardAdd;

        yield return AddNewCards(cardsToAdd, alert);
    }

    /// <summary>
    /// Extends the pile based on which sins are most present on the table
    /// (<see cref="TableManager.suits"/>): the most-present suit adds <see cref="primarySuitCardAdd"/>
    /// cards, the second <see cref="secondarySuitCardAdd"/>, the third <see cref="thirdSuitCardAdd"/>.
    /// Ties are broken randomly, and suits that aren't present at all are ignored.
    /// </summary>
    public IEnumerator ExtendPileAccordingToSins(bool alert = true)
    {
        if (!TableManager.Instance) yield break;

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

        // Play the pick-cards dialogue first and wait for it to close before offering any cards
        // (same StartDialogue / onDialogueEnd wait pattern as OnLoss).
        if (Pick5Container != null && DialogueManager.Instance)
        {
            bool dialogueDone = false;
            void OnDialogueEnd()
            {
                dialogueDone = true;
                DialogueManager.Instance.onDialogueEnd.RemoveListener(OnDialogueEnd);
            }

            DialogueManager.Instance.onDialogueEnd.AddListener(OnDialogueEnd);
            DialogueManager.Instance.StartDialogue(Pick5Container);

            while (!dialogueDone) yield return null;
        }

        if (cardDrafting)
        {
            // Drafting: gather the cards the suits would have contributed and let the player pick
            // draftCardCount of them by clicking. Only the chosen cards are folded into the piles;
            // the rest burn away.
            var cardsToDraft = new Dictionary<CP.Suit, int>();
            if (primary.HasValue)   cardsToDraft[primary.Value]   = primarySuitCardAdd;
            if (secondary.HasValue) cardsToDraft[secondary.Value] = secondarySuitCardAdd;
            if (third.HasValue)     cardsToDraft[third.Value]     = thirdSuitCardAdd;

            List<CardDataBase> candidates = CollectCardsToDraft(cardsToDraft);
            yield return DraftCardsCoroutine(candidates);
        }
        else
        {
            // Old behaviour: fold every card straight into the piles and preview them.
            yield return AddNewCards(primary, secondary, third, alert);
        }
    }

    /// <summary>
    /// Pulls the candidate cards for a draft out of the <see cref="additionalPiles"/> (the same
    /// suit-weighted selection <see cref="AddNewCards(Dictionary{CP.Suit,int},bool)"/> would fold
    /// in) WITHOUT adding them to any pile. Each pulled card is removed from its additional pile so
    /// it is only ever offered once; whether it ends up drafted or burned, it does not return.
    /// </summary>
    private List<CardDataBase> CollectCardsToDraft(Dictionary<CP.Suit, int> cardsToAdd)
    {
        var collected = new List<CardDataBase>();
        if (cardsToAdd == null) return collected;

        foreach (var kv in cardsToAdd)
        {
            for (int i = 0; i < kv.Value; i++)
            {
                ScriptableObjectContainer source = PickSourcePile(kv.Key);
                if (source == null) break;   // every additional pile is empty — nothing left to offer

                ScriptableObject card = h.RandChoice(source.scriptableObjects);
                source.scriptableObjects.Remove(card);

                if (card is CardDataBase cardData) collected.Add(cardData);
            }
        }
        return collected;
    }

    /// <summary>
    /// Card-drafting flow. Lays every candidate card out across the screen (the same grid the
    /// added-cards preview uses) and lets the player CLICK to keep cards. Each clicked card slides
    /// to <see cref="cardsSpawnPoint"/> and is destroyed at once — no burn animation — while its
    /// data is folded into both the current <see cref="pile"/> and the <see cref="fullPile"/>. Once
    /// <see cref="draftCardCount"/> cards have been picked (or every candidate has been taken), the
    /// remaining candidates burn away. These draft cards are display-only: they never enter the hand,
    /// <see cref="Cards"/>, or any pile except through a deliberate pick.
    /// </summary>
    private IEnumerator DraftCardsCoroutine(List<CardDataBase> candidates)
    {
        if (candidates == null || candidates.Count == 0) yield break;

        Camera cam = Camera.main ? Camera.main : FindFirstObjectByType<Camera>();
        if (!cam || !pfbTest) yield break;

        // --- Grid layout (matches ShowAddedCardsAlert so the draft reads like the preview). ---
        int count = candidates.Count;
        float aspect = cam.aspect <= 0f ? 1.7778f : cam.aspect;
        int cols = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(count * aspect)));
        int rows = Mathf.Max(1, Mathf.CeilToInt((float)count / cols));

        float frustumHeight = 2f * previewDistance * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float frustumWidth = frustumHeight * aspect;
        float cellWidth = frustumWidth / cols;
        float cellHeight = frustumHeight / rows;

        Transform camT = cam.transform;
        Vector3 gridCenter = camT.position + camT.forward * previewDistance;
        Quaternion faceRot = Quaternion.LookRotation(camT.forward, camT.up);

        // Parallel lists, kept in lock-step. When a card is picked its entry is removed from ALL of
        // them, so the hover loop and the final burn only ever touch cards still on offer.
        List<Card> draftCards = new List<Card>();
        List<CardDataBase> draftData = new List<CardDataBase>();
        List<Transform> holders = new List<Transform>();
        List<float> holderBaseScale = new List<float>();
        List<Vector3> holderBasePos = new List<Vector3>();
        for (int i = 0; i < count; i++)
        {
            int row = i / cols;
            int col = i % cols;
            int cardsInRow = Mathf.Min(cols, count - row * cols);

            float x = (col - (cardsInRow - 1) * 0.5f) * cellWidth;
            float y = ((rows - 1) * 0.5f - row) * cellHeight;
            Vector3 pos = gridCenter + camT.right * x + camT.up * y;

            // The card is parented under a holder that carries position, facing AND the preview
            // scale (the card animates its own root localScale, so scaling the holder is the only
            // reliable way to size it — see ShowAddedCardsAlert).
            Transform holder = new GameObject("CardDraftHolder").transform;
            holder.SetParent(cardsParent, false);
            holder.SetPositionAndRotation(pos, faceRot);

            Card card = Instantiate(pfbTest, holder, false);
            card.transform.localPosition = Vector3.zero;
            card.transform.localRotation = Quaternion.identity;
            card.SetState(Card.CardState.OnTable);
            card.Lock();
            card.cardData = candidates[i];
            if (cardTextures != null && cardTextures.Count > 0)
                card.SetCardTexture(h.RandChoice(cardTextures));

            float fit = FitScaleToCell(card, cellWidth, cellHeight, camT.right, camT.up);
            float baseScale = fit * Mathf.Max(0f, CardPreviewScale);
            holder.localScale = Vector3.one * baseScale;

            draftCards.Add(card);
            draftData.Add(candidates[i]);
            holders.Add(holder);
            holderBaseScale.Add(baseScale);
            holderBasePos.Add(pos);
        }

        // Never ask for more picks than there are cards on offer.
        int picksTarget = Mathf.Min(draftCardCount, count);
        int picked = 0;

        yield return new WaitForEndOfFrame();   // swallow the click/press that opened the draft
        while (picked < picksTarget)
        {
            int hoveredIndex = PreviewCardUnderMouse(cam, draftCards);

            // Hover pop, identical to the preview: scale the hovered card up and pull it toward the
            // camera so it reads on top of its neighbours.
            for (int i = 0; i < holders.Count; i++)
            {
                Transform holder = holders[i];
                if (!holder) continue;

                bool isHovered = i == hoveredIndex;
                float targetScale = holderBaseScale[i] * (isHovered ? previewHoverScale : 1f);
                Vector3 targetPos = isHovered
                    ? holderBasePos[i] - camT.forward * previewHoverRaise
                    : holderBasePos[i];

                if (previewHoverSmoothing <= 0f)
                {
                    holder.localScale = Vector3.one * targetScale;
                    holder.position = targetPos;
                }
                else
                {
                    float t = 1f - Mathf.Exp(-Time.deltaTime / previewHoverSmoothing);
                    holder.localScale = Vector3.Lerp(holder.localScale, Vector3.one * targetScale, t);
                    holder.position = Vector3.Lerp(holder.position, targetPos, t);
                }
            }

            // Click keeps the hovered card.
            if (Input.GetMouseButtonDown(0) && hoveredIndex >= 0)
            {
                CardDataBase chosenData = draftData[hoveredIndex];
                Transform chosenHolder = holders[hoveredIndex];

                // Drop it from every tracking list so it is neither hovered nor burned later.
                draftCards.RemoveAt(hoveredIndex);
                draftData.RemoveAt(hoveredIndex);
                holders.RemoveAt(hoveredIndex);
                holderBaseScale.RemoveAt(hoveredIndex);
                holderBasePos.RemoveAt(hoveredIndex);

                // Fold the chosen card's data into both piles.
                if (chosenData)
                {
                    if (fullPile) fullPile.scriptableObjects.Add(chosenData);
                    if (pile) pile.scriptableObjects.Add(chosenData);
                }

                picked++;

                SFXManager.Instance.PlayRandomClip(new List<AudioClip>()
                {
                    R.PROJECT.Audio.Cards.TakeCard.takeCard1,
                    R.PROJECT.Audio.Cards.TakeCard.takeCard2,
                    R.PROJECT.Audio.Cards.TakeCard.takeCard3,
                });

                // Slide to the spawn point, then destroy at once (no burn). Runs concurrently so the
                // draft keeps going while the card flies off.
                StartCoroutine(MoveDraftPickToSpawnAndDestroy(chosenHolder));
            }

            yield return null;
        }

        // Every card the player didn't pick burns away, staggered.
        int running = 0;
        foreach (Card card in draftCards)
        {
            if (!card) continue;
            running++;
            card.StartCoroutine(card.Burn(() => running--));
            if (previewBurnStagger > 0f) yield return new WaitForSeconds(previewBurnStagger);
        }
        while (running > 0) yield return null;

        // Clean up the holders left behind after each card burned itself away.
        foreach (Transform holder in holders)
            if (holder) Destroy(holder.gameObject);
    }

    /// <summary>
    /// Slides a chosen draft card (via its holder, so the preview scale is preserved) to
    /// <see cref="cardsSpawnPoint"/> and then destroys the holder — and with it the card — at once.
    /// No burn animation: the card simply vanishes when it reaches the spawn point.
    /// </summary>
    private IEnumerator MoveDraftPickToSpawnAndDestroy(Transform holder)
    {
        if (!holder) yield break;

        Vector3 target = cardsSpawnPoint ? cardsSpawnPoint.position : holder.position;

        if (draftPickMoveDuration > 0f)
            yield return Tween.Position(holder, target, draftPickMoveDuration, Ease.InOutCubic)
                .ToYieldInstruction();
        else
            holder.position = target;

        Destroy(holder.gameObject);   // silently destroys the card too — no burn
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
