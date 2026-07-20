using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Preferences
/// </summary>
public static class P
{
    public enum Team
    {
        None, Player, Enemy
    }
    
    public static TeamContainer PlayerTeam = new TeamContainer()
    {
        allyTags = new List<string> { "Player", "Ally" },
        allyParentTags = new List<string> { "PlayerParent" },
        
        enemyTags = new List<string> { "Enemy" },
        enemyParentTags = new List<string> { "EnemiesParent" }
    };
    
    public static TeamContainer EnemyTeam = new TeamContainer()
    {
        allyTags = new List<string> { "Enemy" },
        allyParentTags = new List<string> { "EnemiesParent" },
        
        enemyTags = new List<string> { "Player", "Ally" },
        enemyParentTags = new List<string> { "PlayerParent" }
    };
    
    public static TeamContainer EmptyTeam = new TeamContainer()
    {
    };

    public static TeamContainer GetTeamContainerByTeam(Team team)
    {
        switch (team)
        {
            case Team.Player: return PlayerTeam; break;
            case Team.Enemy: return EnemyTeam; break;
            case Team.None: return EmptyTeam; break;
        }

        return null;
    }

    public static Dictionary<string, string> raritiesNames = new Dictionary<string, string>
    {
        {"common", "dusted"},
        {"rare", "vanguard"},
        {"super rare", "x-grade"}
    };
    
    public static Dictionary<string, float> rarities = new Dictionary<string, float>
    {
        { raritiesNames["common"], 0.6f }, 
        { raritiesNames["rare"], 0.4f }, 
        { raritiesNames["super rare"], 0.1f }
    };
    public static float anomalyChange = 0.05f;

    public static List<string> equipmentTypes = new List<string>
    {
        "turret", "effect"
    };

    public enum EquipmentType
    {
        Any,
        Turret, 
        Effect,
        None
    }

    public enum DisplayType
    {
        Right,
        Left, Bottom,Top
    }

    public static Dictionary<EquipmentType, DisplayType> equipmentDisplayType =
        new Dictionary<EquipmentType, DisplayType>
        {
            {EquipmentType.Turret, DisplayType.Right},
            {EquipmentType.Effect, DisplayType.Left},
            {EquipmentType.Any, DisplayType.Top}
        };
    
    public enum PositionType
    {
        Head,
        Self,
        First,
        Last,
        Neighbours,
        Previous,
        Next,
        Distance,
        Position
    }

    public enum StatusEffects
    {
        Ignition,
        Stun,
        Shields
    }

    /// <summary>
    /// All possible Unit stats
    /// </summary>
    public enum Stat
    {
        none,
        health,
        maxHealth,
        speed,
        fireRate,
        damage,
        shields,
        maxShields,
        supply
    }

    /// <summary>
    /// Keys of all possible unit stats to get them from stat container
    /// </summary>
    public static Dictionary<Stat, string> StatK = new Dictionary<Stat, string>
    {
        { Stat.health, "health" },
        { Stat.maxHealth, "maxHealth" },
        { Stat.speed, "speed" },
        { Stat.fireRate, "fireRate" },
        { Stat.damage, "damage" },
        { Stat.shields, "shields" },
        { Stat.maxShields, "maxShields"},
        { Stat.supply, "supply" }
    };

    public enum MoodType
    {
        Despair, 
        Wrath,
        Hope,
    }

    public static Dictionary<string, Color> colors = new Dictionary<string, Color>()
    {
        {"move", Color.lawnGreen}, {"attack", Color.softRed}
    };

    
    // ---------------------PROJECT PREFERENCES----------------------
    public static int maxAttackers = 3;

}

[Serializable]
public class TeamContainer
{
    public List<string> allyTags = new List<string>();
    public List<string> allyParentTags = new List<string>();

    public List<string> enemyTags = new List<string>();
    public List<string> enemyParentTags = new List<string>();

    public TeamContainer Copy() => new TeamContainer
    {
        allyTags = new List<string>(allyTags),
        allyParentTags = new List<string>(allyParentTags),
        enemyTags = new List<string>(enemyTags),
        enemyParentTags = new List<string>(enemyParentTags),
    };
}