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
    
    [Header("SuitDestroying")]
    public List<CP.Suit> suitsToDestroy = new List<CP.Suit>();

    public virtual int GenerateVP()
    {
        int vp = 0;
        if (condition == CP.Condition.SuitSet)
        {
           vp = CalculateVpForSuitSets(GatherSourceSuits());
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
        
        TableManager.Instance.RemoveSuits(suitsToDestroy); // TODO more polish, so this part would be noticable

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

    public Dictionary<CP.Suit, int> GatherSourceSuits()
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

        return sourceSuits;
    }
    
    public int CalculateVpForSuitCount(List<Card> sourceCards)
    {
        if (sourceCards.Count == 0) return 0;
        
        int vp = 0;
        int setCount = 0;
        int suitCountDelta;
        foreach (var card in sourceCards)
        {
            suitCountDelta = suitCount - card.cardData.suits.Count;
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

    /// <summary>
    /// Builds one text frame per suit-sprite animation frame (id 1..CP.SuitFrameCount). Each frame
    /// is the full suit line with every suit tag pinned to that frame's id, so cycling the frames
    /// flip-books the suit sprites. Example (3 Wrath suits, 2 frames):
    ///   [ "&lt;sprite name=Wrath1&gt;&lt;sprite name=Wrath1&gt;&lt;sprite name=Wrath1&gt;",
    ///     "&lt;sprite name=Wrath2&gt;&lt;sprite name=Wrath2&gt;&lt;sprite name=Wrath2&gt;" ]
    /// </summary>
    public virtual List<string> GenerateSuits()
    {
        List<string> famousFrames = new List<string>();
        for (int id = 1; id <= CP.SuitFrameCount; id++)
        {
            string frame = "";
            foreach (CP.Suit suit in suits)
                frame += CP.SuitTag(suit, id);
            famousFrames.Add(frame);
        }

        return famousFrames;
    }

    /// <summary>
    /// Builds one description text frame per suit-sprite animation frame (id 1..CP.SuitFrameCount).
    /// The non-suit text (e.g. " = 100") is identical across frames; only the suit tags advance.
    /// </summary>
    public virtual List<string> GenerateDescription()
    {
        List<string> descriptionFrames = new List<string>();
        for (int id = 1; id <= CP.SuitFrameCount; id++)
            descriptionFrames.Add(BuildDescription(id));

        return descriptionFrames;
    }

    /// <summary>
    /// Builds the description string for a single suit-sprite frame <paramref name="id"/>. Override
    /// this (rather than <see cref="GenerateDescription"/>) to change wording while keeping the
    /// per-frame animation behaviour.
    /// </summary>
    protected virtual string BuildDescription(int id)
    {
        string result = "";

        if (condition == CP.Condition.SuitSet)
        {
            result = BuildSuitTags(id) + " = " + vpPerSet.ToString();
        }
        else if (condition == CP.Condition.SuitCount)
        {
            result = suitCount.ToString() + " " + SuitWord(suitCount) + " = " + vpPerSet.ToString();
        }
        else if (condition == CP.Condition.FixedVp)
        {
            result = vpPerSet.ToString();
        }
        else if (condition == CP.Condition.Multiple)
        {
            result = BuildSuitTags(id) + " = " + vpPerSet.ToString()
                     + "\nIF " + suitCount.ToString() + " " + SuitWord(suitCount) + " ON PLACED CARD";
        }

        // Target-source suffix.
        if (targetSource == CP.TargetSource.Hand)
        {
            result += "\nIN HAND";
        }
        else if (targetSource == CP.TargetSource.PlacedCard)
        {
            result += " ON PLACED CARD";
        }
        // TargetSource.Table adds nothing.

        // Activation suffix (only OnTurnEnd is described).
        if (activation == CP.ActivateCond.OnTurnEnd)
        {
            result += "\nON TURN END";
        }

        return result;
    }

    /// <summary>Concatenates the suit-set sprite tags for a single animation frame.</summary>
    private string BuildSuitTags(int id)
    {
        string tags = "";
        foreach (var suit in suitSet)
            tags += CP.SuitTag(suit, id);
        return tags;
    }

    /// <summary>"SUIT" for a single suit, "SUITS" otherwise.</summary>
    private static string SuitWord(int count)
    {
        return count == 1 ? "SUIT" : "SUITS";
    }
}
