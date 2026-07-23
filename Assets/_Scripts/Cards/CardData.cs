using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "CardData", menuName = "Cards/CardData")]
public class CardData : ScriptableObject
{
    public string title;
    public List<CP.Suits> suits = new List<CP.Suits>();
    private int countdown = 0; // if 0 -> player at once
    
    [Header("Effect")]
    /// Target suits -> amount of vp
    public CP.Condition condition = CP.Condition.SuitSet;
    public int vpPerSet = 0;
    public CP.ActivationCondition activation = CP.ActivationCondition.Burn;
    
    
    [Header("Suits set condition")]
    public List<CP.Suits> suitSet = new List<CP.Suits>();

    [Header("Suits count condition")]
    public bool fixedCount = true; // if not, would be min count
    public int suitCount = 0;
}
