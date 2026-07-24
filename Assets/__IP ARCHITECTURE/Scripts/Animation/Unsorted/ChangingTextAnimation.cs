using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Cycles a text component through a list of string frames. Each frame is a full string
/// that replaces the current text; frames advance every <see cref="defaultGap"/> seconds
/// (e.g. frames {"str1", "str2"} -> "str1" is shown, then after the gap swaps to "str2",
/// and so on). Per-frame gaps can be overridden via <see cref="gapsBetweenFrames"/>.
/// Works on a TMP_Text (TextMeshPro / TextMeshProUGUI) on this object or its children.
/// </summary>
public class ChangingTextAnimation : AnimationBase
{
    [Header("ChangingTextAnimation Settings")]
    [Tooltip("The strings to cycle through, in order. Each frame fully replaces the text.")]
    public List<string> frames = new List<string>();

    [Tooltip("Optional per-frame gap (seconds) before advancing to the next frame. If shorter than frames, remaining frames use defaultGap.")]
    public List<float> gapsBetweenFrames = new List<float>();

    [Tooltip("Seconds each frame is shown before switching to the next one.")]
    public float defaultGap = 0.6f;

    public bool shuffleOrder;

    [Tooltip("Text component to drive. Auto-found on this object / its children if left empty.")]
    public TMP_Text target;

    [Tooltip("Text restored when the animation returns to its initial state.")]
    public string initialText;

    public override void Awake()
    {
        if (target == null)
            target = GetComponent<TMP_Text>() ?? GetComponentInChildren<TMP_Text>();

        if (ShouldCaptureInitialState())
            initialText = target != null ? target.text : "";
    }

    public override void ReturnToInitialState()
    {
        if (target != null) target.text = initialText;
    }

    public override IEnumerator AnimationCoroutine()
    {
        if (target == null || frames.Count == 0)
        {
            yield return base.AnimationCoroutine();
            yield break;
        }

        List<int> order = new List<int>(frames.Count);
        for (int i = 0; i < frames.Count; i++) order.Add(i);
        if (shuffleOrder)
        {
            for (int i = order.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (order[i], order[j]) = (order[j], order[i]);
            }
        }

        foreach (int i in order)
        {
            target.text = frames[i];
            float gap = (i < gapsBetweenFrames.Count) ? gapsBetweenFrames[i] : defaultGap;
            yield return new WaitForSeconds(gap);
        }

        yield return base.AnimationCoroutine();
    }
}
