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
    public CP.ActivateCond activation = CP.ActivateCond.Burn;
    public CP.TargetSource targetSource = CP.TargetSource.Table; // where card will take the values
    
    [Header("Suits set condition")]
    public List<CP.Suits> suitSet = new List<CP.Suits>();

    [Header("Suits count condition")]
    public bool fixedCount = true; // if not, would be min count
    public int suitCount = 0;

    public int GenerateVP()
    {
        int vp = 0;
        if (condition == CP.Condition.SuitSet)
        {
            Dictionary<CP.Suits, int> sourceSuits = new Dictionary<CP.Suits, int>();
            if (targetSource == CP.TargetSource.Table)
            {
                foreach (var kvp in TableManager.Instance.suits)
                    sourceSuits[kvp.Key] = kvp.Value;
            } else if (targetSource == CP.TargetSource.Hand)
            {
                foreach (CP.Suits suit in System.Enum.GetValues(typeof(CP.Suits)))
                {
                    sourceSuits[suit] = 0;
                }

                foreach (Card card in HandManager.Instance.Cards)
                {
                    if (!card.cardData) continue;
                    foreach (CP.Suits suit in card.cardData.suits)
                    {
                        sourceSuits[suit]++;
                    }
                }
            }

            // Count how many of each suit a single set requires.
            Dictionary<CP.Suits, int> required = new Dictionary<CP.Suits, int>();
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
        }
        
        h.Out("VP:", vp);
        return vp;
    }
}
