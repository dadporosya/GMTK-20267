using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CardManager : MonoBehaviour
{
    public static CardManager Instance;
    public List<Card> Cards;

    [Header("Turn effects")]
    [Tooltip("Tables whose placed cards receive OnTurnStart / OnTurnEnd each turn.")]
    public List<PlacingArea> targetTables = new List<PlacingArea>();
    [Tooltip("Delay (seconds) between each card's turn-effect activation.")]
    [SerializeField] private float delayBetweenTurnEffects = 0.25f;

    [SerializeField] private Card pfbTest;

    private void Awake()
    {
        h.CreateStaticInstance(this, ref Instance);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Q))
        {
            SpawnCard(pfbTest);
        }
    }

    public void SpawnCard(Card card)
    {
        SFXManager.Instance.PlayRandomClip(new List<AudioClip>()
        {
            R.PROJECT.Audio.Cards.TakeCard.takeCard1,
            R.PROJECT.Audio.Cards.TakeCard.takeCard2,
            R.PROJECT.Audio.Cards.TakeCard.takeCard3,
            // R.PROJECT.Audio.Cards.TakeCard.takeCard4,
        });
        Cards.Add(card);
        Instantiate(card);
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
