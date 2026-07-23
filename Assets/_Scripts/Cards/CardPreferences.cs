using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Card preferences
/// </summary>
public static class CP
{
    public enum Suit
    {
        Love,
        Grief,
        Fear,
        Pride,
        Hate
    }

    /// <summary>Display color for each suit.</summary>
    public static readonly Dictionary<Suit, Color> SuitColors = new Dictionary<Suit, Color>
    {
        { Suit.Love,  new Color(0.93f, 0.29f, 0.47f) }, // pink/rose
        { Suit.Grief, new Color(0.30f, 0.53f, 0.85f) }, // blue
        { Suit.Fear,  new Color(0.55f, 0.36f, 0.79f) }, // purple
        { Suit.Pride, new Color(0.95f, 0.73f, 0.20f) }, // gold
        { Suit.Hate,  new Color(0.80f, 0.20f, 0.18f) }, // crimson
    };

    /// <summary>Returns the color for a suit (white if none assigned).</summary>
    public static Color SuitColor(Suit suit)
    {
        return SuitColors.TryGetValue(suit, out Color c) ? c : Color.white;
    }


    public enum Condition
    {
        SuitSet,
        PerCardWithSuitCount,
        Custom
    }

    public enum ActivateCond
    {
        Burn,
        Place
    }

    public enum TargetSource
    {
        Table,
        Hand,
        OnPlace
    }

    public static string SuitTag(CP.Suit suit)
    {
        string result = $"<sprite name={suit.ToString()}>";
        
        return result;
    }
    
}