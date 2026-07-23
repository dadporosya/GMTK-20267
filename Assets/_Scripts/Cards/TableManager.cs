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
    [Tooltip("Change animation played on the score while it is counting. Auto-found on scoreText if left empty.")]
    [SerializeField] private TextChangeAnimation scoreChangeAnimation;

    [Header("Score count animation")]
    [Tooltip("Slowest counting speed, in points per second (used for tiny deltas).")]
    [SerializeField] private float minCountSpeed = 15f;
    [Tooltip("Fastest counting speed, in points per second (used for large deltas).")]
    [SerializeField] private float maxCountSpeed = 200f;
    [Tooltip("Delta size (in points) at which the fastest speed is reached.")]
    [SerializeField] private float speedRampDelta = 50f;

    [Header("Suits")]
    public Dictionary<CP.Suit, int> suits = new Dictionary<CP.Suit, int>();
    [SerializeField] private List<CP.Suit> startSuits = new List<CP.Suit>();
    [SerializeField] private List<int> startSuitCount = new List<int>();

    [Tooltip("Prefab for a single suit line: a TMP_Text with a TextChangeAnimation component.")]
    [SerializeField] private GameObject suitTextPrefab;
    [Tooltip("Parent the suit lines are instantiated under (arranged top to bottom).")]
    [SerializeField] private Transform suitTextParent;
    [Tooltip("Vertical gap between suit lines, in the parent's local units.")]
    [SerializeField] private float suitLineSpacing = 40f;
    [Tooltip("If true, a suit line loops its change animation for suitChangeAnimDuration after each change. If false, it just plays once.")]
    [SerializeField] private bool loopSuitAnimation = false;
    [Tooltip("How long a suit line keeps looping its change animation after its count changes, in seconds. Only used when loopSuitAnimation is true.")]
    [SerializeField] private float suitChangeAnimDuration = 0.5f;

    public UnityEvent onScoreReached;


    // The value currently shown by the animation (may lag behind currentScore mid-tween).
    private int _displayedScore;
    private Tween _scoreTween;

    // Guards onScoreReached so it only fires once until the score leaves the reached state again.
    private bool _scoreReached;

    // Per-suit views spawned from suitTextPrefab.
    private readonly Dictionary<CP.Suit, TMP_Text> _suitTexts = new Dictionary<CP.Suit, TMP_Text>();
    private readonly Dictionary<CP.Suit, TextChangeAnimation> _suitAnims = new Dictionary<CP.Suit, TextChangeAnimation>();
    private readonly Dictionary<CP.Suit, Tween> _suitStopTweens = new Dictionary<CP.Suit, Tween>();

    private void Start()
    {
        h.CreateStaticInstance(this, ref Instance);
        h.Out(Instance);

        if (scoreChangeAnimation == null && scoreText != null)
            scoreChangeAnimation = scoreText.GetComponentInChildren<TextChangeAnimation>();

        _displayedScore = currentScore;
        RefreshScoreText(currentScore);

        BuildSuitLines();
    }

    // Spawns one suit line (TMP + TextChangeAnimation) per suit and stacks them vertically.
    private void BuildSuitLines()
    {
        int i = 0;
        foreach (CP.Suit suit in System.Enum.GetValues(typeof(CP.Suit)))
        {
            int startValue = 0;

            int index = startSuits.IndexOf(suit);
            if (index >= 0 && index < startSuitCount.Count)
                startValue = startSuitCount[index];

            suits[suit] = startValue;

            if (suitTextPrefab != null)
            {
                Transform parent = suitTextParent != null ? suitTextParent : transform;
                GameObject go = Instantiate(suitTextPrefab, parent);
                go.name = $"SuitLine_{suit}";

                // Stack vertically (top to bottom). Works whether the line is UI or world-space.
                if (go.transform is RectTransform rt)
                    rt.anchoredPosition = new Vector2(0f, -i * suitLineSpacing);
                else
                    go.transform.localPosition = new Vector3(0f, -i * suitLineSpacing, 0f);

                TMP_Text text = go.GetComponentInChildren<TMP_Text>();
                TextChangeAnimation anim = go.GetComponentInChildren<TextChangeAnimation>();

                if (text != null) _suitTexts[suit] = text;
                if (anim != null) _suitAnims[suit] = anim;

                RefreshSuitText(suit);
            }

            i++;
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
            if (scoreChangeAnimation != null) scoreChangeAnimation.Stop();
            _displayedScore = to;
            RefreshScoreText(to);
            return;
        }

        // Speed (points/second) scales with the delta: small change -> slow, big change -> fast.
        float t = speedRampDelta > 0f ? Mathf.Clamp01(delta / speedRampDelta) : 1f;
        float speed = Mathf.Lerp(minCountSpeed, maxCountSpeed, t);
        float duration = delta / Mathf.Max(speed, 0.0001f);

        // Play the change animation for the whole count, and stop it once the number settles.
        if (scoreChangeAnimation != null) scoreChangeAnimation.Play();

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
        ).OnComplete(() =>
        {
            if (scoreChangeAnimation != null) scoreChangeAnimation.Stop();
        });
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

    public void AddSuits(List<CP.Suit> suitsToAdd)
    {
        foreach (var suit in suitsToAdd)
            AddSuit(suit);
    }

    public void AddSuit(CP.Suit suit)
    {
        if (!suits.ContainsKey(suit)) suits[suit] = 0;
        suits[suit]++;

        RefreshSuitText(suit);
        PlaySuitChangeAnimation(suit);
    }

    // Rebuilds a single suit line: "{SuitTag} {count}", colored with that suit's color.
    private void RefreshSuitText(CP.Suit suit)
    {
        if (!_suitTexts.TryGetValue(suit, out TMP_Text text) || text == null)
            return;

        int count = suits.TryGetValue(suit, out int value) ? value : 0;
        string hex = ColorUtility.ToHtmlStringRGB(CP.SuitColor(suit));
        text.text = $"<color=#{hex}>{CP.SuitTag(suit)} {count}</color>";
    }

    // Plays the suit line's change animation: once, or looping for suitChangeAnimDuration.
    private void PlaySuitChangeAnimation(CP.Suit suit)
    {
        if (!_suitAnims.TryGetValue(suit, out TextChangeAnimation anim) || anim == null)
            return;

        if (!loopSuitAnimation)
        {
            anim.PlayOnce();
            return;
        }

        anim.Play();

        // Restart the stop timer so repeated changes keep the animation alive.
        if (_suitStopTweens.TryGetValue(suit, out Tween running) && running.isAlive)
            running.Stop();

        _suitStopTweens[suit] = Tween.Delay(suitChangeAnimDuration, anim.Stop);
    }
}
