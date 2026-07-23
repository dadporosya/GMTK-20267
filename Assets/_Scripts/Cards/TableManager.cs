using System.Collections.Generic;
using PrimeTween;
using TMPro;
using UnityEngine;

public class TableManager : MonoBehaviour
{
    public static TableManager Instance;

    public int targetScore=100;
    public int currentScore=0;

    [SerializeField] private TMP_Text scoreText;

    [Header("Score count animation")]
    [Tooltip("Slowest counting speed, in points per second (used for tiny deltas).")]
    [SerializeField] private float minCountSpeed = 15f;
    [Tooltip("Fastest counting speed, in points per second (used for large deltas).")]
    [SerializeField] private float maxCountSpeed = 200f;
    [Tooltip("Delta size (in points) at which the fastest speed is reached.")]
    [SerializeField] private float speedRampDelta = 50f;

    public Dictionary<CP.Suits, int> suits = new Dictionary<CP.Suits, int>();
    [SerializeField] private List<CP.Suits> startSuits = new List<CP.Suits>();
    [SerializeField] private List<int> startSuitCount = new List<int>();
    
    
    // The value currently shown by the animation (may lag behind currentScore mid-tween).
    private int _displayedScore;
    private Tween _scoreTween;

    private void Start()
    {
        h.CreateStaticInstance(this, ref Instance);

        _displayedScore = currentScore;
        RefreshScoreText(currentScore);

        foreach (CP.Suits suit in System.Enum.GetValues(typeof(CP.Suits)))
        {
            int startValue = 0;

            int index = startSuits.IndexOf(suit);
            if (index >= 0 && index < startSuitCount.Count)
                startValue = startSuitCount[index];

            suits[suit] = startValue;
        }
        
    }

    /// <summary>
    /// Adds <paramref name="amount"/> to the current score (can be negative) and
    /// animates the displayed number up/down to the new value.
    /// </summary>
    public void AddScore(int amount) => SetScore(currentScore + amount);

    /// <summary>
    /// Sets the current score to <paramref name="value"/> and smoothly counts the
    /// displayed number from the previously shown value to the new one.
    /// The bigger the change, the faster the count (clamped between min/max speed).
    /// Pass <paramref name="instant"/> = true to skip the animation.
    /// </summary>
    public void SetScore(int value, bool instant = false)
    {
        currentScore = value;

        // Stop any running count so a new change takes over from what's on screen now.
        if (_scoreTween.isAlive)
            _scoreTween.Stop();

        int from = _displayedScore;
        int to = value;
        int delta = Mathf.Abs(to - from);

        if (instant || delta == 0)
        {
            _displayedScore = to;
            RefreshScoreText(to);
            return;
        }

        // Speed (points/second) scales with the delta: small change -> slow, big change -> fast.
        float t = speedRampDelta > 0f ? Mathf.Clamp01(delta / speedRampDelta) : 1f;
        float speed = Mathf.Lerp(minCountSpeed, maxCountSpeed, t);
        float duration = delta / Mathf.Max(speed, 0.0001f);

        // Linear ease so the numbers tick evenly, one after another.
        _scoreTween = Tween.Custom(
            from,
            to,
            duration,
            value =>
            {
                _displayedScore = Mathf.RoundToInt(value);
                RefreshScoreText(_displayedScore);
            },
            ease: Ease.Linear
        );
    }

    private void RefreshScoreText(int value)
    {
        if (scoreText != null)
            scoreText.text = value.ToString();
    }

    public void AddSuits(List<CP.Suits> suits)
    {
        foreach (var suit in suits)
        {
            AddSuit(suit);
        }
    }
    public void AddSuit(CP.Suits suit)
    {
        suits[suit]++;
    }
}
