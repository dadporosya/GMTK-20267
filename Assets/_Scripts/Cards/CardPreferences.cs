using UnityEngine;

/// <summary>
/// Card preferences
/// </summary>
public static class CP
{
    public enum Suits
    {
        Love,
        Grief,
        Fear,
        Pride,
        Anger
    }

    public enum Condition
    {
        SuitSet,
        SuitCount,
        Custom
    }

    public enum ActivationCondition
    {
        Burn,
        Place
    }
    
}