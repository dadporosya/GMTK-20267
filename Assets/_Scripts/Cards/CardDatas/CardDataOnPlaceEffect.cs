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
        
        
        
        h.Out("VP:", vp);
        return vp;
    }
}
