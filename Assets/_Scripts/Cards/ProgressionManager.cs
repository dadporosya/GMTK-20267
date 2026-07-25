using UnityEngine;

/// <summary>
/// Drives the target-score progression across levels, roughly an arithmetic progression
/// with a growing "boost" term.
///
///   level        score
///   1            initialScore
///   2            initialScore + constPerLevel
///   3            initialScore + constPerLevel * 2 + constBoostPerLevel
///
/// General form:
///   score = initialScore + constPerLevel * (level - 1) + constBoostPerLevel * max(0, level - 2)
///
/// The boost multiplier is clamped at 0 so level 1 stays exactly at initialScore
/// (and level 2 gets no boost yet).
/// </summary>
public class ProgressionManager : MonoBehaviour
{
    public static ProgressionManager Instance;

    [Tooltip("Target score of level 1.")]
    public int initialScore = 100;
    [Tooltip("Flat amount added to the target score for each level past the first.")]
    public int constPerLevel = 50;
    [Tooltip("Extra amount that accumulates on top of constPerLevel from level 3 onward.")]
    public int constBoostPerLevel = 25;
    [Tooltip("Current level (starts at 1).")]
    public int level = 1;

    private void Awake()
    {
        h.CreateStaticInstance(this, ref Instance);
    }

    private void Start()
    {
        SetScore(level);
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
                    + constBoostPerLevel * Mathf.Max(0, levelIn - 2);

        // overrideScore == true means the designer pins targetScore in the inspector, so the
        // progression only writes it when overrideScore is false.
        if (TableManager.Instance && !TableManager.Instance.overrideScore)
            TableManager.Instance.targetScore = score;

        h.Out("ProgressionManager: level", levelIn, "target score", score);

        return score;
    }

    /// <summary>
    /// Advances to the next level and applies its target score.
    /// </summary>
    public int NextLevel() => SetScore(level + 1);
}
