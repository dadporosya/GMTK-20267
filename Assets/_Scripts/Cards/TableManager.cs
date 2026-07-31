using System;
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

    [Header("Angel Fill")]
    [Tooltip("Sprite renderer using Tiled draw mode. Its tile size height is scaled to reflect score progress " +
             "toward the goal: full when the goal is reached, empty at the start of the round.")]
    [SerializeField] private SpriteRenderer angelFill;
    [Tooltip("If true, angelFill is tinted with the color of the currently most frequent sin (suit). " +
             "The renderer's existing alpha is preserved.")]
    [SerializeField] private bool sinColorFill = false;
    // Full tile-size height captured on Start; the shown height is this * progress (0..1).
    private float _angelFillFullHeight;

    [Header("Score count animation")]
    [Tooltip("Slowest counting speed, in points per second (used for tiny deltas).")]
    [SerializeField] private float minCountSpeed = 15f;
    [Tooltip("Fastest counting speed, in points per second (used for large deltas).")]
    [SerializeField] private float maxCountSpeed = 200f;
    [Tooltip("Delta size (in points) at which the fastest speed is reached.")]
    [SerializeField] private float speedRampDelta = 50f;
    [Tooltip("Volume of the looped counter tick sound played while the score is counting.")]
    [SerializeField] private float counterSoundVolume = 1f;

    // Looping AudioSource that plays R.PROJECT.Audio.sfx.counter for as long as the score is
    // counting. Started once when a count begins and stopped when it settles; it is NOT restarted
    // when a new count takes over mid-animation, so the loop plays continuously across changes.
    private AudioSource _counterSource;

    [Header("Suits")]
    public Dictionary<CP.Suit, int> suits = new Dictionary<CP.Suit, int>();
    [SerializeField] private List<CP.Suit> startSuits = new List<CP.Suit>();
    [SerializeField] private List<int> startSuitCount = new List<int>();

    [Tooltip("One SuitTracker per suit (7 total). Order doesn't matter — each tracker is keyed by its own targetSuit.")]
    [SerializeField] private List<SuitTracker> rawSuitTrackers = new List<SuitTracker>();
    // Built on Start from rawSuitTrackers: suit -> its tracker.
    public Dictionary<CP.Suit, SuitTracker> suitTrackers = new Dictionary<CP.Suit, SuitTracker>();
    public Transform suitTrackerParent;

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
    [Tooltip("Suits whose cutscene has already been reached. Persisted to disk by SeenCutscenesSave " +
             "and reloaded on the next launch, so seen sins stay seen between play sessions.")]
    public List<CP.Suit> playedCutScenes = new List<CP.Suit>();

    /// <summary>
    /// Suits whose cutscene has already played *in this run*. Runtime only — never serialized and
    /// never written to disk, so it starts empty every time the scene loads (i.e. every new run).
    /// This is what decides the placeholder dialogue: a sin always gets its normal dialogue the
    /// first time it comes up in a run, no matter how many earlier runs already showed it, and only
    /// repeats within the same run fall back to the placeholder.
    /// </summary>
    [NonSerialized] public List<CP.Suit> playedCutScenesThisRun = new List<CP.Suit>();

    [Tooltip("If false, the saved list of seen cutscenes on disk is ignored (useful while testing).")]
    [SerializeField] private bool loadSeenCutscenesFromDisk = true;

    // Created in Awake (not Start) so ProgressionManager can reliably reach TableManager.Instance
    // from its own Start, regardless of script execution order.
    private void Awake()
    {
        h.CreateStaticInstance(this, ref Instance);

        LoadPlayedCutscenes();
    }

    // Merges the suits saved by previous launches into playedCutScenes. Anything already set in the
    // inspector is kept, so authored "pretend this was seen" entries still work.
    private void LoadPlayedCutscenes()
    {
        if (!loadSeenCutscenesFromDisk) return;

        foreach (CP.Suit suit in SeenCutscenesSave.Load())
        {
            if (!playedCutScenes.Contains(suit))
                playedCutScenes.Add(suit);
        }

        h.Out("Loaded seen cutscenes", playedCutScenes);
    }

    private void Start()
    {
        for (int i = 0; i < rawSinCutscenes.Count; i++)
        {
            SinCutScenes.Add(suitsForCutscenes[i], rawSinCutscenes[i]);
        }

        if (scoreChangeAnimation == null && scoreText != null)
            scoreChangeAnimation = scoreText.GetComponentInChildren<TextChangeAnimation>();

        // Capture the full tile height before the first refresh so the fill can be scaled from it.
        if (angelFill != null)
            _angelFillFullHeight = angelFill.size.y;

        // currentScore starts from the inspector value; targetScore is owned by ProgressionManager
        // (unless overrideScore is true).
        _displayedScore = currentScore;
        RefreshScoreText(currentScore);

        if (suitTrackerParent)
        {
            foreach (Transform suitTracker in suitTrackerParent)
            {
                if (suitTracker.TryGetComponent(out SuitTracker tracker)) continue;
                if (rawSuitTrackers.Contains(tracker)) continue;
                rawSuitTrackers.Add(tracker);
            }
        }
        // rawSuitTrackers = FindObjectsOfType<SuitTracker>().ToList();
        BuildSuitTrackers();
    }




    // Keys every SuitTracker by its own suit, then seeds each suit's starting count and
    // initializes the matching tracker. Expects one tracker per suit (7 total); list order
    // doesn't matter since the dictionary is keyed by each tracker's targetSuit.
    private void BuildSuitTrackers()
    {
        suitTrackers.Clear();
        List<CP.Suit> rawSuits = Enum.GetValues(typeof(CP.Suit))
            .Cast<CP.Suit>()
            .ToList();
        for (int i = 0; i < rawSuitTrackers.Count; i++)
        {
            suitTrackers.Add(rawSuits[i], rawSuitTrackers[i]);
        }

        foreach (CP.Suit suit in System.Enum.GetValues(typeof(CP.Suit)))
        {
            int startValue = 0;

            int index = startSuits.IndexOf(suit);
            if (index >= 0 && index < startSuitCount.Count)
                startValue = startSuitCount[index];

            suits[suit] = startValue;
            
            h.Out("ASUIT TRACKERS", suitTrackers);
            foreach (var kv in suitTrackers)
            {
                if (kv.Value)
                {
                    kv.Value.Initialize(kv.Key, suits[suit]);
                }
            }
            
                
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
            StopCounterSound();
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

        // Start the looped counter sound (no-op if it's already playing from a previous count).
        StartCounterSound();

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
            StopCounterSound();
            CheckScoreReached();
        });
    }

    // Lazily creates a dedicated looping AudioSource (routed through the SFX mixer group) and starts
    // the counter clip if it isn't already playing. Called every time a count begins; because it only
    // starts when not already playing, an ongoing loop keeps going across back-to-back score changes.
    private void StartCounterSound()
    {
        // if (_counterSource == null)
        // {
        //     GameObject go = new GameObject("CounterSound");
        //     go.transform.SetParent(transform);
        //     _counterSource = go.AddComponent<AudioSource>();
        //     _counterSource.clip = R.PROJECT.Audio.sfx.counter;
        //     _counterSource.loop = true;
        //     _counterSource.playOnAwake = false;
        //     _counterSource.outputAudioMixerGroup = AudioMixerManager.GetSFXGroup();
        // }
        //
        // _counterSource.volume = counterSoundVolume;
        // if (!_counterSource.isPlaying)
        //     _counterSource.Play();
    }

    // Stops the looped counter sound once the score stops counting.
    private void StopCounterSound()
    {
        if (_counterSource != null && _counterSource.isPlaying)
            _counterSource.Stop();
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

        // Tie-break, in order of preference:
        //  1. tied suits not yet played this run AND never seen in an earlier run — brand new content;
        //  2. tied suits not yet played this run — they still get their normal dialogue;
        //  3. all tied suits — every one of them would repeat, so it doesn't matter which.
        // Within the chosen tier the pick is random.
        List<CP.Suit> freshThisRun = tied.FindAll(s => !HasPlayedThisRun(s));
        List<CP.Suit> neverSeen = freshThisRun.FindAll(s => !playedCutScenes.Contains(s));

        List<CP.Suit> pool = neverSeen.Count > 0 ? neverSeen
                           : freshThisRun.Count > 0 ? freshThisRun
                           : tied;
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

    /// <summary>
    /// True if this suit's cutscene already played earlier in the *current run*. Repeats within a
    /// run are what the placeholder dialogue is for — cross-run history (<see cref="playedCutScenes"/>)
    /// deliberately does not count here.
    /// </summary>
    public bool HasPlayedThisRun(CP.Suit suit) =>
        playedCutScenesThisRun != null && playedCutScenesThisRun.Contains(suit);

    // Records a suit as seen: always in the per-run list, and (the first time ever) in the persisted
    // list, which is immediately written to disk so the progress survives a crash or an alt-F4 as
    // well as a clean quit.
    public void AddPlayedCutscene(CP.Suit suit)
    {
        if (playedCutScenesThisRun == null) playedCutScenesThisRun = new List<CP.Suit>();
        if (!playedCutScenesThisRun.Contains(suit)) playedCutScenesThisRun.Add(suit);

        if (playedCutScenes.Contains(suit)) return;

        playedCutScenes.Add(suit);
        SeenCutscenesSave.Save(playedCutScenes);
    }

    /// <summary>
    /// Forgets which sins played in the current run, so every sin gets its normal dialogue again.
    /// Call this when a new run begins without the scene being reloaded; on a scene reload the list
    /// starts empty by itself. Cross-run progress on disk is untouched.
    /// </summary>
    public void ResetRunCutscenes()
    {
        if (playedCutScenesThisRun == null) playedCutScenesThisRun = new List<CP.Suit>();
        else playedCutScenesThisRun.Clear();
    }

    // Dev helper: wipes the saved progress so every sin cutscene counts as unseen again.
    [ContextMenu("Clear Seen Cutscenes Save")]
    public void ClearSeenCutscenesSave()
    {
        playedCutScenes.Clear();
        SeenCutscenesSave.Clear();
        h.Out("Cleared seen cutscenes save");
    }
    
    private void RefreshScoreText(int value)
    {
        if (scoreText != null)
            scoreText.text = value.ToString();

        UpdateAngelFill(value);
    }

    /// <summary>
    /// 0..1 progress toward the goal, valid in both modes.
    /// Decreasing mode: (targetScore - score) / targetScore  (== (maxScore - remainingScore) / maxScore).
    /// Normal mode:     score / targetScore.
    /// </summary>
    private float GetScoreProgress(int score)
    {
        if (targetScore <= 0) return 0f;

        float progress = decreasingScore
            ? (float)(targetScore - score) / targetScore
            : (float)score / targetScore;

        return Mathf.Clamp01(progress);
    }

    // Scales the angelFill tile-size height to match score progress: 0 -> empty, 1 -> full.
    // When sinColorFill is on, also tints it with the most frequent sin's color (keeping alpha).
    private void UpdateAngelFill(int score)
    {
        if (angelFill == null) return;

        Vector2 size = angelFill.size;
        size.y = _angelFillFullHeight * GetScoreProgress(score);
        angelFill.size = size;

        if (sinColorFill && TryGetMostFrequentSuit(out CP.Suit topSuit))
        {
            Color color = CP.SuitColor(topSuit);
            color.a = angelFill.color.a; // keep the alpha it already had
            angelFill.color = color;
        }
    }

    // Finds the suit with the highest count. Returns false if no suits are tracked yet.
    // Ties resolve to whichever tied suit is encountered first.
    private bool TryGetMostFrequentSuit(out CP.Suit topSuit)
    {
        topSuit = default;
        if (suits.Count == 0) return false;

        int bestCount = int.MinValue;
        bool found = false;
        foreach (var kvp in suits)
        {
            if (kvp.Value > bestCount)
            {
                bestCount = kvp.Value;
                topSuit = kvp.Key;
                found = true;
            }
        }

        return found;
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

    // Sets every suit's count back to 0 and refreshes each tracker to match.
    public void ResetSuits()
    {
        foreach (CP.Suit suit in System.Enum.GetValues(typeof(CP.Suit)))
        {
            suits[suit] = 0;
            UpdateSuitTracker(suit);
        }
    }

    // Sets every suit's count back to 0 and refreshes each tracker to match.
    public void RefreshSuits()
    {
        foreach (CP.Suit suit in System.Enum.GetValues(typeof(CP.Suit)))
        {
            suits[suit] = 0;
            UpdateSuitTracker(suit);
        }
    }

    // Pushes the current count for a suit into its tracker (which plays its count-change animation).
    private void UpdateSuitTracker(CP.Suit suit)
    {
        int count = suits.TryGetValue(suit, out int value) ? value : 0;
        if (suitTrackers.TryGetValue(suit, out SuitTracker tracker) && tracker != null)
            tracker.SetCount(count);

        // The leading sin may have changed, so re-apply the fill tint (uses the shown score).
        if (sinColorFill)
            UpdateAngelFill(_displayedScore);
    }
}
