using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// A single on-table suit counter, living on the SuitTracker prefab
/// (Assets/_Prefabs/Cards/SuitTracker). It shows a suit's name (title), its animated
/// sprite tag, and the current count for that suit.
///
/// TableManager keeps one SuitTracker per suit and pushes count changes in through
/// <see cref="SetCount"/>; each change plays <see cref="countChangeAnim"/> so a count tick
/// reads visibly. The suit sprite flip-books through its frames exactly like the cards do
/// (see Card / CardDataBase), via a <see cref="ChangingTextAnimationn"/> on the suit text.
/// </summary>
public class SuitTracker : MonoBehaviour
{
    [Header("Texts")]
    [Tooltip("Shows the suit's name (e.g. \"Lust\").")]
    [SerializeField] private TMP_Text titleTmp;
    [Tooltip("Shows the suit's sprite tag; flip-books through its sprite frames like the cards do.")]
    [SerializeField] private TMP_Text suitTmp;
    [Tooltip("Shows the current count for this suit.")]
    [SerializeField] private TMP_Text countTmp;

    [Header("Animations")]
    [Tooltip("Played once whenever the count changes. Auto-found on countTmp if left empty.")]
    [SerializeField] private AnimationBase countChangeAnim;
    [Tooltip("Suit sprite flip-book (mirrors the cards). Auto-found on suitTmp if left empty.")]
    [SerializeField] private ChangingTextAnimationn suitAnim;

    [Header("State")]
    [Tooltip("The suit this tracker represents. Set per-instance so TableManager can key its dictionary by it.")]
    public CP.Suit targetSuit;
    public int currentCount;

    private void Awake()
    {
        ResolveRefs();
    }

    // Resolve the animation components from their text objects if they weren't wired in the inspector
    // (same fallback pattern the Card uses for its ChangingTextAnimations).
    private void ResolveRefs()
    {
        if (!countChangeAnim && countTmp) countChangeAnim = countTmp.GetComponentInChildren<AnimationBase>();
        if (!suitAnim && suitTmp) suitAnim = suitTmp.GetComponentInChildren<ChangingTextAnimationn>();
    }

    /// <summary>
    /// Points this tracker at <paramref name="suit"/>: writes the suit name to the title,
    /// starts the suit sprite flip-book, and shows <paramref name="count"/> (without playing
    /// the count-change animation, since this is the initial state).
    /// </summary>
    public void Initialize(CP.Suit suit, int count = 0)
    {
        h.Out("Tracker INit ", suit, count);
        ResolveRefs();
        targetSuit = suit;

        if (titleTmp) titleTmp.text = suit.ToString();

        PlaySuitFlipbook();

        currentCount = count;
        RefreshCount();
    }

    /// <summary>Updates the shown count and plays the count-change animation.</summary>
    public void SetCount(int count)
    {
        currentCount = count;
        RefreshCount();
        PlayCountChangeAnimation();
    }

    private void RefreshCount()
    {
        if (countTmp) countTmp.text = currentCount.ToString();
    }

    private void PlayCountChangeAnimation()
    {
        if (countChangeAnim) countChangeAnim.PlayInstantly();
    }

    // Builds one sprite-tag frame per suit sprite frame (id 1..CP.SuitFrameCount) and cycles
    // them on suitTmp, mirroring how Card/CardDataBase flip-book the suit sprites. The frames
    // loop while the ChangingTextAnimationn's own loop flag (set on the component) is on.
    private void PlaySuitFlipbook()
    {
        if (suitAnim)
        {
            List<string> frames = new List<string>();
            for (int id = 1; id <= CP.SuitFrameCount; id++)
                frames.Add(CP.SuitTag(targetSuit, id));

            suitAnim.frames = frames;
            StartCoroutine(suitAnim.Play());
        }
        else if (suitTmp)
        {
            // No flip-book component: just show the first sprite frame.
            suitTmp.text = CP.SuitTag(targetSuit, 1);
        }
    }
}
