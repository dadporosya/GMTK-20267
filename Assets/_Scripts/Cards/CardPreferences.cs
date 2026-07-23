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