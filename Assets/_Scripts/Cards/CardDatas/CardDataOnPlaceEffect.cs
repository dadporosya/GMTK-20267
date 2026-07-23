using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "CardData", menuName = "Cards/CardData")]
public class CardDataOnPlaceEffect : CardDataBase
{
    [Header("CardDataOnPlaceEffect Settings")]
    public Card placedCard;

    private void OnEnable()
    {
        activation = CP.ActivateCond.Place;
    }
    public override int GenerateVP()
    {
        int vp = 0;

        if (placedCard == null) return vp;

        Dictionary<CP.Suit, int> sourceSuits = new Dictionary<CP.Suit, int>();
        foreach (var suit in placedCard.cardDataBase.suits)
        {
            if (sourceSuits.ContainsKey(suit))
                sourceSuits[suit]++;
            else
                sourceSuits[suit] = 1;
        }
        vp = CalculateVpForSuitSets(sourceSuits);
        
        h.Out("VP:", vp);
        return vp;
    }

    public void ChangePlacedCard(Card card)
    {
        placedCard = card;
    }
    
    public override string GenerateDescription()
    {
        string result = base.GenerateDescription();

        result += $"\nON PLACED CARD";
        
        return result;
    }
}
