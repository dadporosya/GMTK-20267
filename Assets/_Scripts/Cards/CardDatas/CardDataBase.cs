using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "CardData", menuName = "Cards/CardData")]
public class CardDataBase : ScriptableObject
{
    public string title;
    public List<CP.Suit> suits = new List<CP.Suit>();
    public int countdown = 0; // if 0 -> player at once
    
    [Header("Effect")]
    /// Target suits -> amount of vp
    public CP.Condition condition = CP.Condition.SuitSet;
    public int vpPerSet = 0;
    public CP.ActivateCond activation = CP.ActivateCond.Burn;
    public CP.TargetSource targetSource = CP.TargetSource.Table; // where card will take the values
    
    [Header("Suits set condition")]
    public List<CP.Suit> suitSet = new List<CP.Suit>();

    [Header("Suits count condition")]//
    public bool fixedCount = true; // if not, would be min count
    public int suitCount = 0;

    public virtual int GenerateVP()
    {
        int vp = 0;
        if (condition == CP.Condition.SuitSet)
        {
            Dictionary<CP.Suit, int> sourceSuits = new Dictionary<CP.Suit, int>();
            if (targetSource == CP.TargetSource.Table)
            {
                foreach (var kvp in TableManager.Instance.suits)
                    sourceSuits[kvp.Key] = kvp.Value;
            } else if (targetSource == CP.TargetSource.Hand)
            {
                foreach (CP.Suit suit in System.Enum.GetValues(typeof(CP.Suit)))
                {
                    sourceSuits[suit] = 0;
                }

                foreach (Card card in HandManager.Instance.Cards)
                {
                    if (!card.cardData) continue;
                    foreach (CP.Suit suit in card.cardData.suits)
                    {
                        sourceSuits[suit]++;
                    }
                }
            }

           vp = CalculateVpForSuitSets(sourceSuits);
        } else if (condition == CP.Condition.FixedVp)
        {
            vp = vpPerSet;
        } else if (condition == CP.Condition.SuitCount)
        {
            List<Card>  sourceCards = new List<Card>();
            if (targetSource == CP.TargetSource.Table)
            {
                foreach (var table in CardManager.Instance.targetTables)
                {
                    sourceCards.AddRange(table.cards);
                }
            } else if (targetSource == CP.TargetSource.Hand)
            {
                sourceCards.AddRange(HandManager.Instance.Cards);
            }
            
            vp = CalculateVpForSuitCount(sourceCards); 
        }
        
        h.Out("VP:", vp);
        return vp;
    }
    
    public int CalculateVpForSuitSets(Dictionary<CP.Suit, int> sourceSuits)
    {
        int vp = 0;
        
        // Count how many of each suit a single set requires.
        Dictionary<CP.Suit, int> required = new Dictionary<CP.Suit, int>();
        foreach (var suit in suitSet)
        {
            if (!required.ContainsKey(suit))
                required[suit] = 0;
            required[suit]++;
        }

        // How many complete sets can be formed = min over each required suit of
        // (available count / required count), rounded down. Multiply by vpPerSet.
        if (required.Count > 0)
        {
            int sets = int.MaxValue;
            foreach (var kvp in required)
            {
                int available = sourceSuits.TryGetValue(kvp.Key, out int count) ? count : 0;
                int possible = available / kvp.Value; // integer division floors
                if (possible < sets)
                    sets = possible;
            }
            vp = sets * vpPerSet;
        }
        
        return vp;
    }

    public int CalculateVpForSuitCount(List<Card> sourceCards)
    {
        if (sourceCards.Count == 0) return 0;
        
        int vp = 0;
        int setCount = 0;
        int suitCountDelta;
        foreach (var card in sourceCards)
        {
            suitCountDelta = card.cardData.suits.Count;
            if (fixedCount == true && suitCountDelta == 0)
            {
                setCount++;
            } else if (fixedCount == false && suitCountDelta >= 0)
            {
                setCount++;
            }
        }

        vp = vpPerSet * setCount;
        return vp;
    }

    public virtual string GenerateTitle()
    {
        string result = "";
        foreach (CP.Suit suit in suits)
        {
            result += CP.SuitTag(suit);
        }
        
        return result;
    }

    public virtual string GenerateDescription()
    {
        string result = "";
        string conditionLabel = "";
        
        
        if (condition == CP.Condition.SuitSet)
        {
            foreach (var suit in suitSet)
            {
                conditionLabel += CP.SuitTag(suit);
            }
            result += conditionLabel + " = " + vpPerSet.ToString();
        } else if (condition == CP.Condition.FixedVp)
        {
            result = vpPerSet.ToString();
        } else if (condition == CP.Condition.SuitCount)
        {
            if (suitSet.Count == 1)
            {
                result += $"CARD WITH {suitCount} SUIT";
            }
            else
            {
                result += $"CARD WITH {suitCount} SUITS";
            }
            result += " =  " + vpPerSet.ToString();
        }
        
        return result;
    }
}
