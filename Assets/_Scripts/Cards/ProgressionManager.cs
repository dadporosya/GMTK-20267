using UnityEngine;

/// <summary>
/// Drives the target-score progression across levels, roughly an arithmetic progression
/// with a growing "boost" term.
///
///   level        score
///   1            initialScore
///   2            initialScore + constPerLevel
///   3            initialScore + constPerLevel * 2 + constBoostPerLevel
///   4            initialScore + constPerLevel * 3 + constBoostPerLevel * 2 + constBoostPerLevel2
///
/// General form:
///   score = initialScore + constPerLevel * (level - 1)
///           + constBoostPerLevel  * max(0, level - 2)
///           + constBoostPerLevel2 * max(0, level - 3)
///
/// Each boost multiplier is clamped at 0 so level 1 stays exactly at initialScore,
/// level 2 gets no boost yet, and constBoostPerLevel2 only kicks in from level 4 onward.
/// </summary>
public class ProgressionManager : MonoBehaviour
{
    public static ProgressionManager Instance;

    [Tooltip("Target score of level 1.")]
    public int initialScore = 2500;
    [Tooltip("Flat amount added to the target score for each level past the first.")]
    public int constPerLevel = 1500;
    [Tooltip("Extra amount that accumulates on top of constPerLevel from level 3 onward.")]
    public int constBoostPerLevel = 1250;
    [Tooltip("Further extra amount that accumulates on top from level 4 onward.")]
    public int constBoostPerLevel2 = 1000;
    [Tooltip("Current level (starts at 1).")]
    public int level = 1;

    private void Awake()
    {
        h.CreateStaticInstance(this, ref Instance);
    }

    private void Start()
    {
        // SetScore(level);
    }
    
    /// <summary>
    /// Computes the target score for <paramref name="levelIn"/> and pushes it onto
    /// <see cref="TableManager.targetScore"/>.
    /// </summary>
    public int SetScore(int levelIn)
    {
        this.level = levelIn;

        int score = initialScore
                    + constPerLevel * (levelIn - 1)
                    + constBoostPerLevel * Mathf.Max(0, levelIn - 2)
                    + constBoostPerLevel2 * Mathf.Max(0, levelIn - 3);

        // overrideScore == true means the designer pins targetScore in the inspector, so the
        // progression only writes it when overrideScore is false.
        if (TableManager.Instance && !TableManager.Instance.overrideScore)
        {
            TableManager.Instance.targetScore = score;
            TableManager.Instance.ResetScoreForRound();
        }
        h.Out("ProgressionManager: level", levelIn, "target score", score);

        return score;
    }

    /// <summary>
    /// Advances to the next level and applies its target score.
    /// </summary>
    public int NextLevel() => SetScore(level + 1);

    public int CurrentLevel() => SetScore(level);
}
