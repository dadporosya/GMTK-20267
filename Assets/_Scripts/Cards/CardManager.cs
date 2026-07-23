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
        Cards.Add(card);
        Instantiate(card);
    }
}
