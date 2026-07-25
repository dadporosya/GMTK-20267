using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Card preferences
/// </summary>
public static class CP
{
    public enum Suit
    {
        Pride,
        Greed,
        Lust,
        Envy,
        Gluttony,
        Wrath,
        Sloth
    }

    /// <summary>Display color for each of the seven deadly-sin suits. Chosen to stay visually
    /// distinct from one another.</summary>
    public static readonly Dictionary<Suit, Color> SuitColors = new Dictionary<Suit, Color>
    {
        { Suit.Pride,    new Color(0.2706f, 0.3059f, 0.1843f) }, // royal purple
        { Suit.Greed,    new Color(0.5333f, 0.4431f, 0.3294f) }, // gold
        { Suit.Lust,     new Color(0.4235f, 0.1882f, 0.1882f) }, // magenta / rose
        { Suit.Envy,     new Color(0.3373f, 0.2667f, 0.3255f) }, // emerald green
        { Suit.Gluttony, new Color(0.4353f, 0.3373f, 0.2314f) }, // orange
        { Suit.Wrath,    new Color(0.5725f, 0.2353f, 0.1373f) }, // crimson red
        { Suit.Sloth,    new Color(0.4588f, 0.3490f, 0.4471f) }, // slate blue
    };

    /// <summary>How many sprite frames each suit tag has in the TMP sprite asset
    /// (e.g. "Wrath1", "Wrath2"). Drives the number of text frames the card generators emit.</summary>
    public const int SuitFrameCount = 2;

    /// <summary>Returns the color for a suit (white if none assigned).</summary>
    public static Color SuitColor(Suit suit)
    {
        return SuitColors.TryGetValue(suit, out Color c) ? c : Color.white;
    }

    /// <summary>
    /// Multiple Condition:
    /// Only for OnPlace events
    /// If placed card has necessary amount of suits, return vp for suit sets
    /// </summary>
    public enum Condition
    {
        SuitSet,
        SuitCount,
        FixedVp,
        Multiple,
        Custom
    }
    
    

    public enum ActivateCond
    {
        Burn,
        OtherCardPlaced,
        OnTurnEnd,
        OnTurnStart
    }

    public enum TargetSource
    {
        Table,
        Hand,
        PlacedCard
    }

    public static string SuitTag(CP.Suit suit, int id=-1)
    {
        string idText = id == -1 ? "" :  id.ToString();
        string result = $"<sprite name={suit.ToString()}{idText}>";
        
        return result;
    }

    public static string CardIconTag(int id = 1)
    {
        string idText = id == -1 ? "" :  id.ToString();
        string result = $"<sprite name=card{idText}>";
        
        return result;
    }
    
}