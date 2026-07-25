using System.Collections.Generic;
using System.Linq;
using PrimeTween;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

public class TableManager : MonoBehaviour
{
    public static TableManager Instance;

    public int targetScore=100;
    public int currentScore=0;

    [Tooltip("If false (default), targetScore is completely determined by ProgressionManager. " +
             "If true, targetScore keeps its inspector value and ProgressionManager leaves it alone.")]
    public bool overrideScore = false;

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

    [Tooltip("One SuitTracker per suit (7 total). Order doesn't matter — each tracker is keyed by its own targetSuit.")]
    [SerializeField] private List<SuitTracker> rawSuitTrackers = new List<SuitTracker>();
    // Built on Start from rawSuitTrackers: suit -> its tracker.
    public Dictionary<CP.Suit, SuitTracker> suitTrackers = new Dictionary<CP.Suit, SuitTracker>();

    public UnityEvent onScoreReached;


    // The value currently shown by the animation (may lag behind currentScore mid-tween).
    private int _displayedScore;
    private Tween _scoreTween;

    // Guards onScoreReached so it only fires once until the score leaves the reached state again.
    private bool _scoreReached;

    [Header("On Score Reached Stuff")]
    [SerializeField] private List<CutSceneBase> rawSinCutscenes;

    [SerializeField] private List<CP.Suit> suitsForCutscenes;
    public Dictionary<CP.Suit, CutSceneBase> SinCutScenes = new Dictionary<CP.Suit, CutSceneBase>();
    public List<CP.Suit> playedCutScenes = new List<CP.Suit>();
    
    // Created in Awake (not Start) so ProgressionManager can reliably reach TableManager.Instance
    // from its own Start, regardless of script execution order.
    private void Awake()
    {
        h.CreateStaticInstance(this, ref Instance);
    }

    private void Start()
    {
        for (int i = 0; i < rawSinCutscenes.Count; i++)
        {
            SinCutScenes.Add(suitsForCutscenes[i], rawSinCutscenes[i]);
        }

        if (scoreChangeAnimation == null && scoreText != null)
            scoreChangeAnimation = scoreText.GetComponentInChildren<TextChangeAnimation>();

        // currentScore starts from the inspector value; targetScore is owned by ProgressionManager
        // (unless overrideScore is true).
        _displayedScore = currentScore;
        RefreshScoreText(currentScore);

        BuildSuitTrackers();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F))
        {
            OnScoreReached();
        }
    }
    
    // Keys every SuitTracker by its own suit, then seeds each suit's starting count and
    // initializes the matching tracker. Expects one tracker per suit (7 total); list order
    // doesn't matter since the dictionary is keyed by each tracker's targetSuit.
    private void BuildSuitTrackers()
    {
        suitTrackers.Clear();

        foreach (SuitTracker tracker in rawSuitTrackers)
        {
            if (tracker == null) continue;
            suitTrackers[tracker.targetSuit] = tracker;
        }

        foreach (CP.Suit suit in System.Enum.GetValues(typeof(CP.Suit)))
        {
            int startValue = 0;

            int index = startSuits.IndexOf(suit);
            if (index >= 0 && index < startSuitCount.Count)
                startValue = startSuitCount[index];

            suits[suit] = startValue;

            // Trackers always start at 0, regardless of the suit's seeded start count.
            if (suitTrackers.TryGetValue(suit, out SuitTracker tracker) && tracker != null)
                tracker.Initialize(suit, 0);
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
            CheckScoreReached();
        });
    }

    /// <summary>
    /// Resets the score for a new round, once <see cref="targetScore"/> has been set for that round.
    /// In <see cref="decreasingScore"/> mode the player counts down from the target to 0, so the
    /// score starts at <see cref="targetScore"/>; otherwise it starts at 0 and rises to the target.
    /// </summary>
    public void ResetScoreForRound()
    {
        _scoreReached = false;
        SetScore(decreasingScore ? targetScore : 0, instant: false);
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
        h.Out("CheckScoreReached");
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
        
        
        onScoreReached?.Invoke();

        // Find the highest suit count in the suits dictionary.
        if (suits.Count == 0) return;

        int bestCount = int.MinValue;
        foreach (var kvp in suits)
            if (kvp.Value > bestCount) bestCount = kvp.Value;

        // Collect every suit tied at that highest count.
        List<CP.Suit> tied = new List<CP.Suit>();
        foreach (var kvp in suits)
            if (kvp.Value == bestCount) tied.Add(kvp.Key);

        if (tied.Count == 0) return;

        // Tie-break: prefer a tied suit whose cutscene hasn't been played yet; among those pick at
        // random. If every tied suit has already been played, pick at random from all of them.
        List<CP.Suit> unplayed = tied.FindAll(s => !playedCutScenes.Contains(s));
        List<CP.Suit> pool = unplayed.Count > 0 ? unplayed : tied;
        CP.Suit mostPlayed = h.RandChoice(pool);

        // Play the matching cutscene for that suit, if one is registered.
        if (SinCutScenes.TryGetValue(mostPlayed, out CutSceneBase cutscene) && cutscene != null)
        {
            CutSceneManager.Instance.RunCutscene(cutscene);
        }
        else
        {
            CutSceneManager.Instance.RunCutscene(h.RandChoice(SinCutScenes.Values.ToList()));
        }
        h.Out("ScoreReached");
    }

    public void AddPlayedCutscene(CP.Suit suit)
    {
        if (!playedCutScenes.Contains(suit))
            playedCutScenes.Add(suit);
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

        UpdateSuitTracker(suit);
    }

    public void RemoveSuits(List<CP.Suit> suitsToRemove)
    {
        foreach (var suit in suitsToRemove)
            RemoveSuit(suit);
    }

    public void RemoveSuit(CP.Suit suit)
    {
        if (!suits.ContainsKey(suit)) suits[suit] = 0;
        suits[suit] = h.Max(suits[suit]-1, 0);

        UpdateSuitTracker(suit);
    }

    // Pushes the current count for a suit into its tracker (which plays its count-change animation).
    private void UpdateSuitTracker(CP.Suit suit)
    {
        int count = suits.TryGetValue(suit, out int value) ? value : 0;
        if (suitTrackers.TryGetValue(suit, out SuitTracker tracker) && tracker != null)
            tracker.SetCount(count);
    }
}
