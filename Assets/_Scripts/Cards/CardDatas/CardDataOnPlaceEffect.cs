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

        if (condition == CP.Condition.SuitSet)
        {
            vp = CalculateForSuitSets();
        } else if (condition == CP.Condition.SuitCount)
        {
            vp = CalculateForSuitCount();
        } else if (condition == CP.Condition.Multiple)
        {
            
            int tempVp = CalculateForSuitCount();

            if (tempVp > 0)
            {
                vp = CalculateForSuitSets();
            }
        }
        
        
        h.Out("activate place card", vp);
        
        h.Out("VP:", vp);
        return vp;
    }

    private int CalculateForSuitSets()
    {
        Dictionary<CP.Suit, int> sourceSuits = new Dictionary<CP.Suit, int>();
        foreach (var suit in CardManager.Instance.currentPlacedCard.cardData.suits)
        {
            if (sourceSuits.ContainsKey(suit))
                sourceSuits[suit]++;
            else
                sourceSuits[suit] = 1;
        }
        return CalculateVpForSuitSets(sourceSuits);
    }

    private int CalculateForSuitCount()
    {
        return CalculateVpForSuitCount(new List<Card>()
        {
            CardManager.Instance.currentPlacedCard
        });
    }
    
    public override string GenerateDescription()
    {
        string result = base.GenerateDescription();

        result += $"\nON PLACED CARD";
        
        return result;
    }
}
