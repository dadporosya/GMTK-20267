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
    
}