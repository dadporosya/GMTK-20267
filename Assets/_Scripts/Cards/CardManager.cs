using System;
using System.Collections.Generic;
using UnityEngine;

public class CardManager : MonoBehaviour
{
    public static CardManager Instance;
    public List<Card> Cards;

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
}
