using System.Collections.Generic;
using PrimeTween;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

public class TableManager : MonoBehaviour
{
    public static TableManager Instance;

    public int targetScore=100;
    public int currentScore=0;

    [Tooltip("If true, gained points DECREASE the score and the player must bring it down to 0 to trigger onScoreReached. If false, the score rises toward targetScore as before.")]
    public bool decreasingScore = false;

    [SerializeField] private TMP_Text scoreText;

    [Header("Score count animation")]
    [Tooltip("Slowest counting speed, in points per second (used for tiny deltas).")]
    [SerializeField] private float minCountSpeed = 15f;
    [Tooltip("Fastest counting speed, in points per second (used for large deltas).")]
    [SerializeField] private float maxCountSpeed = 200f;
    [Tooltip("Delta size (in points) at which the fastest speed is reached.")]
    [SerializeField] private float speedRampDelta = 50f;

    public Dictionary<CP.Suit, int> suits = new Dictionary<CP.Suit, int>();
    [SerializeField] private List<CP.Suit> startSuits = new List<CP.Suit>();
    [SerializeField] private List<int> startSuitCount = new List<int>();

    public UnityEvent onScoreReached;
    
    
    // The value currently shown by the animation (may lag behind currentScore mid-tween).
    private int _displayedScore;
    private Tween _scoreTween;

    // Guards onScoreReached so it only fires once until the score leaves the reached state again.
    private bool _scoreReached;

    private void Start()
    {
        h.CreateStaticInstance(this, ref Instance);
        h.Out(Instance);

        _displayedScore = currentScore;
        RefreshScoreText(currentScore);

        foreach (CP.Suit suit in System.Enum.GetValues(typeof(CP.Suit)))
        {
            int startValue = 0;

            int index = startSuits.IndexOf(suit);
            if (index >= 0 && index < startSuitCount.Count)
                startValue = startSuitCount[index];

            suits[suit] = startValue;
        }
        
    }

    /// <summary>
    /// Adds <paramref name="amount"/> "gained points" to the score and animates the
    /// displayed number to the new value.
    /// In normal mode the score goes up; in <see cref="decreasingScore"/> mode gained
    /// points are subtracted instead, so the score counts down toward 0.
    /// </summary>
    public void AddScore(int amount) => SetScore(currentScore + (decreasingScore ? -amount : amount));

    /// <summary>
    /// Sets the current score to <paramref name="value"/> and smoothly counts the
    /// displayed number from the previously shown value to the new one.
    /// The bigger the change, the faster the count (clamped between min/max speed).
    /// Pass <paramref name="instant"/> = true to skip the animation.
    /// </summary>
    public void SetScore(int value, bool instant = false)
    {
        // In decreasing mode the score can't go below 0 (the goal); otherwise leave it free.
        if (decreasingScore)
            value = Mathf.Max(0, value);

        currentScore = value;

        CheckScoreReached();

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
        
        //
    }

    /// <summary>
    /// Whether the goal condition is currently met:
    /// decreasing mode -> score counted down to 0 (or below);
    /// normal mode      -> score reached targetScore (or above).
    /// </summary>
    public bool IsScoreReached()
    {
        return decreasingScore ? currentScore <= 0 : currentScore >= targetScore;
    }

    // Fires OnScoreReached once when the goal condition becomes true, and re-arms
    // if the score later leaves that state.
    private void CheckScoreReached()
    {
        if (IsScoreReached())
        {
            if (!_scoreReached)
            {
                _scoreReached = true;
                OnScoreReached();
            }
        }
        else
        {
            _scoreReached = false;
        }
    }

    public void OnScoreReached()
    {
        h.Out("ScoreReached");
        onScoreReached?.Invoke();
    }
    
    private void RefreshScoreText(int value)
    {
        if (scoreText != null)
            scoreText.text = value.ToString();
    }

    public void AddSuits(List<CP.Suit> suits)
    {
        foreach (var suit in suits)
        {
            AddSuit(suit);
        }
    }
    public void AddSuit(CP.Suit suit)
    {
        suits[suit]++;
    }
}
