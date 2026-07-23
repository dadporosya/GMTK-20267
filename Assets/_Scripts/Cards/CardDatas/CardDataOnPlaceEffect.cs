using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "CardData", menuName = "Cards/CardDataOnPlaceEffect")]
public class CardDataOnPlaceEffect : CardDataBase
{

    private void OnEnable()
    {
        activation = CP.ActivateCond.OtherCardPlaced;
        targetSource = CP.TargetSource.PlacedCard;
    }
    public override int GenerateVP()
    {
        int vp = 0;

        if (CardManager.Instance.currentPlacedCard == null) return vp;

        Dictionary<CP.Suit, int> sourceSuits = new Dictionary<CP.Suit, int>();
        foreach (var suit in CardManager.Instance.currentPlacedCard.cardData.suits)
        {
            if (sourceSuits.ContainsKey(suit))
                sourceSuits[suit]++;
            else
                sourceSuits[suit] = 1;
        }
        vp = CalculateVpForSuitSets(sourceSuits);
        
        h.Out("activate place card", vp);
        
        h.Out("VP:", vp);
        return vp;
    }
    
    public override string GenerateDescription()
    {
        string result = base.GenerateDescription();

        result += $"\nON PLACED CARD";
        
        return result;
    }
}
