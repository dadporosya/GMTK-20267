using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Resolves card effects one at a time instead of letting every card fire at once — which
/// reads as a more polished, staggered sequence.
///
/// When a card is placed, the placed card and every reacting card register themselves here
/// via <see cref="Card.PrepareForActivation"/> (see CardManager.OnCardPlaced for the ordering:
/// the freshly placed card first, then the cards reacting to it). <see cref="EffectResolveCoroutine"/>
/// then walks the queue in order, running each card's activation coroutine to completion and
/// pausing <see cref="delayBeforeActivations"/> before each so the effects play consecutively
/// rather than in a single simultaneous burst.
/// </summary>
public class EffectResolverManager : MonoBehaviour
{
    public static EffectResolverManager Instance;

    [Tooltip("Cards waiting to have their effect resolved, in the order they were queued " +
             "(the freshly placed card first, then the cards reacting to it).")]
    public Queue<Card> cardsToResolve = new Queue<Card>();

    [Tooltip("Pause (seconds) before each card's effect resolves, so effects play one after " +
             "another instead of all at once.")]
    [SerializeField] private float delayBeforeActivations = 0.2f;
    [SerializeField] private float delayBeforeFirstActivation = 0.4f;

    private void Awake()
    {
        h.CreateStaticInstance(this, ref Instance);
    }

    /// <summary>Registers <paramref name="card"/> to have its effect resolved in turn.</summary>
    public void PrepareCard(Card card)
    {
        if (card) cardsToResolve.Enqueue(card);
    }

    /// <summary>Fire-and-forget entry point: resolves the whole queue on this manager.</summary>
    public void ResolveEffects()
    {
        StartCoroutine(EffectResolveCoroutine());
    }

    /// <summary>
    /// Resolves every queued card consecutively: wait <see cref="delayBeforeActivations"/>,
    /// run the card's activation coroutine to completion, then move on to the next. Yield on
    /// this from a caller (e.g. CardManager) so turn-end / turn-start run only once the whole
    /// queue has finished resolving.
    /// </summary>
    public IEnumerator EffectResolveCoroutine()
    {
        var wait = new WaitForSeconds(delayBeforeActivations);
        yield return new WaitForSeconds(delayBeforeFirstActivation);
        while (cardsToResolve.Count > 0)
        {
            Card card = cardsToResolve.Dequeue();
            if (!card || !card.cardData) continue;   // skip destroyed / data-less cards

            // A small beat before the effect so cards resolve one at a time.
            yield return wait;

            // Run this card's activation to the end before starting the next one.
            yield return card.ActivateCardCoroutine();
        }
    }
}
