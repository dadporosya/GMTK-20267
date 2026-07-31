using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using PrimeTween;
using Unity.VisualScripting;

/// <summary>
/// sfx победы или что-то в этом роде
/// -> small delay
/// -> camera turns to the left window
/// -> environment completely changes it color to the matching sin's color. then, small delay
/// -> dialogue start, and camera turns to angel
/// -> after dialogue gp starts again
/// </summary>
/// <returns></returns>

public class SinCutsceneBase : CutSceneBase
{
    public CP.Suit sin;
    [Header("Place Holder Settings")]
    [Tooltip("Used only when this sin is chosen a SECOND (or further) time within the same run. " +
             "The first time a sin comes up in a run it always plays its normal dialogue, even if " +
             "earlier runs already showed it. Leave empty to always use the normal dialogue.")]
    [SerializeField] private List<DialogueContainer> placeholderDialogues;
    
    [Header("After Win")]
    [SerializeField] private float delayAfterWin = 2f;
    [Tooltip("Delay between each card starting to burn, so the table and hand clear in a cascade " +
             "instead of all at once.")]
    [SerializeField] private float burnCardStagger = 0.08f;
    [Tooltip("If on, OnWin waits for every card's burn to finish before the cutscene continues.")]
    [SerializeField] private bool waitForBurnsToFinish = false;

    [Header("Suit Tracker Beat")]
    [Tooltip("If on, after the cards burn the camera turns to the table and the SuitTracker matching " +
             "this cutscene's sin plays its count-change animation (the count itself is left alone).")]
    [SerializeField] private bool playSuitTrackerAnim = true;
    [Tooltip("Time given to the camera to settle in table view before the tracker animates.")]
    [SerializeField] private float trackerCameraSettleDelay = 1f;
    [Tooltip("Time held on the table after the tracker animation before the cutscene continues.")]
    [SerializeField] private float delayAfterTrackerAnim = 1f;

    [Header("Environment")]
    [Tooltip("Light whose color is faded to lightColor. Falls back to RenderSettings.sun, then the " +
             "first Light in the scene, if left empty.")]
    [SerializeField] private Light environmentLight;
    [SerializeField] private Color lightColor=Color.white;
    [SerializeField] private float colorFadeDuration = 3f;
    [SerializeField] private float delayAfterColorChange = 1f;
    [Tooltip("If on, lightColor is overwritten with this sin's color (CP.SuitColor) before the fade, " +
             "matching the cutscene's 'environment turns the sin's color' description.")]
    [SerializeField] private bool useSinColor = true;

    [SerializeField] private bool changeLights = true;
    [SerializeField] private bool changeSkyBox = true;

    [Tooltip("If on, the object tagged 'AngelFeel' has its SpriteRenderer tinted to the sin's color " +
             "(at angelFillAlpha) during the cutscene, then restored to how it was on cutscene end.")]
    [SerializeField] private bool angelFill = true;
    [Tooltip("Alpha applied to the AngelFeel object's SpriteRenderer color when angelFill is on.")]
    [SerializeField] private float angelFillAlpha = 1f;

    [Tooltip("If on, the object(s) tagged with eyeTag have their SpriteRenderer color set to the " +
             "sin's color during the cutscene, then restored on cutscene end.")]
    [SerializeField] private bool lightenEye = true;
    [Tooltip("If on, the eye is colored with this sin's color (CP.SuitColor). If off, eyeColor is used.")]
    [SerializeField] private bool eyeUseSinColor = true;
    [Tooltip("Color the eye's SpriteRenderer is set to when eyeUseSinColor is off.")]
    [SerializeField] private Color eyeColor = Color.white;
    [Tooltip("How much the eye's target color is washed out towards white. 0 = the raw sin/eye color, " +
             "1 = pure white. Use this to make the eye read brighter without changing the sin's palette.")]
    [Range(0f, 1f)]
    [SerializeField] private float eyeLightenAmount = 0f;
    [Tooltip("Multiplies the eye's target RGB after lightening. 1 = unchanged, >1 pushes the color " +
             "into HDR territory so it can bloom. Alpha is never touched.")]
    [SerializeField] private float eyeColorIntensity = 1f;
    [Tooltip("Tag used to find the eye object(s).")]
    [SerializeField] private string eyeTag = "AngelEye";

    // Remembers the AngelFeel object's original renderer color so CutsceneEnd can restore it.
    private SpriteRenderer angelFeelRenderer;
    private Color angelFeelOriginalColor;
    private bool angelFeelColorCaptured = false;

    // One entry per recolored eye renderer, holding what CutsceneEnd needs to put it back.
    private class EyeTintState
    {
        public SpriteRenderer renderer;
        public Color originalColor;
    }

    private readonly List<EyeTintState> eyeStates = new List<EyeTintState>();

    [SerializeField] private AudioClip soundtrack;
    [SerializeField] private float ostFadeIn = 2.67f;
    [Tooltip("If off, when the cutscene ends the sin's soundtrack is replaced by a random track from " +
             "BGMManager.bgTracks. If on, the soundtrack is left playing as-is after the cutscene.")]
    [SerializeField] private bool continuePlayingOst = false;

    [Header("Dialogue")]
    [SerializeField] private DialogueContainer sinDialogue;
    public override void Init()
    {
        base.Init();

        // A sin cutscene's real end point is the dialogue closing, which happens long after the step
        // sequence finishes. Keep the instance alive until DialogueEnd runs and calls DestroyCutscene().
        customDestroy = true;

        List<IEnumerator> rawSteps = new List<IEnumerator>()
        {
            DefaultStep(),

        };

        foreach (IEnumerator step in rawSteps)
        {
            cutsceneSteps.Add(step);
        }
    }

    /// <summary>
    /// sfx победы или что-то в этом роде
    /// -> small delay
    /// -> camera turns to the left window
    /// -> environment completely changes it color to the matching sin's color. then, small delay
    /// -> dialogue start (place holder only if this suit already came up earlier in the SAME run),
    ///    and camera turns to angel
    /// -> after dialogue gp starts again
    /// </summary>
    /// <returns></returns>
    
    public virtual IEnumerator DefaultStep()
    {
        yield return null;
        
        
        yield return OnWin();
        // yield return TurnCameraToWindow();
        StartCoroutine(ChangeEnvironment());
        yield return DialogueStart();


        yield return null;
    }
    
    
    public virtual IEnumerator OnWin()
    {
        
        // Burn every card on the table and in hand.

        // Lock out card interaction for the whole cutscene — re-enabled in CutsceneEnd.
        if (CardDragController.Instance) CardDragController.Instance.SetDraggingEnabled(false);

        yield return BurnAllCards();
        yield return new WaitForSeconds(1.67f);

        // With the table cleared, turn to it and let this sin's tracker react before moving on.
        yield return ShowSuitTracker();

        // Fade the current background music out to silence so the win lands in quiet before the
        // sin's soundtrack comes in during DialogueStart.
        if (BGMManager.Instance) BGMManager.Instance.FadeOutMusic();

        yield return new WaitForSeconds(delayAfterWin);
        yield return null;
    }

    /// <summary>
    /// Turns the camera to the table and plays the count-change animation on the
    /// <see cref="SuitTracker"/> whose suit matches this cutscene's <see cref="sin"/>, so the win
    /// visibly lands on that sin's tracker. The tracker's count is not touched — this is purely the
    /// "adding suit" flourish; the actual suit bookkeeping stays where it already happens.
    /// </summary>
    public virtual IEnumerator ShowSuitTracker()
    {
        if (!playSuitTrackerAnim) yield break;

        if (TableTopCameraController.Instance) TableTopCameraController.Instance.SwitchToTableView();

        if (trackerCameraSettleDelay > 0f) yield return new WaitForSeconds(trackerCameraSettleDelay);

        if (TableManager.Instance != null &&
            TableManager.Instance.suitTrackers.TryGetValue(sin, out SuitTracker tracker) && tracker)
        {
            // markSinAchieved: the first time this sin's cutscene is chosen the tracker also recolors
            // to GamePlusManager.achivedCardColor (alongside this animation) and the sin is
            // written to the save file. On a repeat, only the animation plays.
            tracker.PlayCountChangeAnimation(true);
            SFXManager.Instance.PlayRandomClip( new List<AudioClip>()
            {
                R.PROJECT.Audio.Cards.Activate.activateFaded1   
            },
                randomPitchRange: new Vector2(-3,-2)
                
                );
        }
        else
        {
            h.Out("SinCutsceneBase: no SuitTracker found for suit", sin, "— tracker animation skipped.");
        }

        if (delayAfterTrackerAnim > 0f) yield return new WaitForSeconds(delayAfterTrackerAnim);
    }

    /// <summary>
    /// Burns every card currently placed on the tables <see cref="CardManager"/> tracks and every
    /// card still in the player's hand, staggered by <see cref="burnCardStagger"/> so they clear in
    /// a cascade. The card lists are snapshotted first because <see cref="Card.Burn"/> removes each
    /// card from its table / hand list as it burns (mutating the source collection mid-iteration).
    /// Each burn is started on the card itself so it runs to completion independently of the
    /// cutscene; when <see cref="waitForBurnsToFinish"/> is on, this waits for all of them.
    /// </summary>
    public virtual IEnumerator BurnAllCards()
    {
        List<Card> toBurn = new List<Card>();

        // Cards resting on every tracked table.
        if (CardManager.Instance != null)
        {
            foreach (PlacingArea table in CardManager.Instance.targetTables)
            {
                if (!table) continue;
                foreach (Card card in table.cards)
                    if (card && !toBurn.Contains(card)) toBurn.Add(card);
            }
        }

        // Cards still held in the player's hand.
        if (HandManager.Instance != null)
        {
            foreach (Card card in HandManager.Instance.Cards)
                if (card && !toBurn.Contains(card)) toBurn.Add(card);
        }

        if (toBurn.Count == 0) yield break;

        int running = 0;
        foreach (Card card in toBurn)
        {
            if (!card) continue;
            running++;
            // Run the burn on the card so it finishes even if the cutscene moves on; the callback
            // lets us optionally wait for every card to finish burning. Burn is idempotent.
            card.StartCoroutine(card.Burn(() => running--));

            if (burnCardStagger > 0f) yield return new WaitForSeconds(burnCardStagger);
        }

        if (waitForBurnsToFinish)
            while (running > 0) yield return null;
    }

    public virtual IEnumerator TurnCameraToWindow()
    {
        if (!TableTopCameraController.Instance) yield break;
        TableTopCameraController.Instance.SwitchToWindowView();
        yield return new WaitForSeconds(2f);
        
        yield return null;    
    }

    /// <summary>
    /// Fades the environment light's color to <see cref="lightColor"/> over
    /// <see cref="colorFadeDuration"/> seconds (the sin's color when <see cref="useSinColor"/> is on),
    /// then waits <see cref="delayAfterColorChange"/>.
    /// </summary>
    public virtual IEnumerator ChangeEnvironment()
    {
        if (useSinColor) lightColor = CP.SuitColor(sin);

        Light target = ResolveEnvironmentLight();
        if (!target)
        {
            h.Out("SinCutsceneBase: no environment light found — color change skipped.");
            yield return new WaitForSeconds(delayAfterColorChange);
            yield break;
        }

        // Tween the light's color to lightColor over colorFadeDuration (PrimeTween, per project convention).
        if (changeLights)
        {
            Tween fade = Tween.Custom(target, target.color, lightColor, colorFadeDuration,
                (Light l, Color c) => l.color = c);
            
            // yield return fade.ToYieldInstruction();
        }

        if (changeSkyBox)
        {
            // Switch the camera's background from Skybox to a Solid Color fill and fade that fill to
            // lightColor over the same duration as the light, so the whole environment takes on the
            // sin's color. Fire-and-forget (matching the light fade above) since ChangeEnvironment
            // itself runs as a detached coroutine.
            Camera cam = Camera.main;
            if (cam)
            {
                Color startColor = cam.backgroundColor;
                cam.clearFlags = CameraClearFlags.SolidColor;
                Tween.Custom(cam, startColor, lightColor, colorFadeDuration,
                    (Camera c, Color col) => c.backgroundColor = col);
            }
            else
            {
                h.Out("SinCutsceneBase: no main camera found — skybox color change skipped.");
            }
        }

        if (angelFill)
        {
            // Tint the AngelFeel object to the sin's color at angelFillAlpha, remembering its original
            // color so CutsceneEnd can put it back exactly as it was.
            GameObject angelFeel = GameObject.FindWithTag("AngelFeel");
            if (angelFeel && angelFeel.TryGetComponent(out SpriteRenderer sr))
            {
                angelFeelRenderer = sr;
                angelFeelOriginalColor = sr.color;
                angelFeelColorCaptured = true;

                Color fill = CP.SuitColor(sin);
                fill.a = angelFillAlpha;
                // Fade the angel's tint in over the same duration as the light and skybox instead of
                // snapping, so the whole environment recolor reads as one smooth transition.
                Tween.Custom(sr, sr.color, fill, colorFadeDuration,
                    (SpriteRenderer r, Color c) => r.color = c);
            }
            else
            {
                h.Out("SinCutsceneBase: no object tagged 'AngelFeel' with a SpriteRenderer found — angel fill skipped.");
            }
        }

        if (lightenEye) LightenEye();

        yield return new WaitForSeconds(delayAfterColorChange);
    }

    /// <summary>
    /// Finds the object(s) tagged <see cref="eyeTag"/> ("AngelEye") and fades their
    /// <c>SpriteRenderer.color</c> to the sin's color over <see cref="colorFadeDuration"/>,
    /// brightened by <see cref="eyeLightenAmount"/> / <see cref="eyeColorIntensity"/>.
    /// Original colors are remembered so <see cref="CutsceneEnd"/> can put them back.
    /// </summary>
    protected virtual void LightenEye()
    {
        eyeStates.Clear();

        Color target = Lighten(eyeUseSinColor ? CP.SuitColor(sin) : eyeColor);

        foreach (SpriteRenderer sr in ResolveEyeRenderers())
        {
            if (!sr) continue;

            eyeStates.Add(new EyeTintState { renderer = sr, originalColor = sr.color });

            // Alpha is left as the renderer had it — only the RGB is swapped.
            target.a = sr.color.a;

            Tween.Custom(sr, sr.color, target, colorFadeDuration,
                (SpriteRenderer r, Color c) => { if (r) r.color = c; });
        }

        if (eyeStates.Count == 0)
            h.Out($"SinCutsceneBase: no object tagged '{eyeTag}' with a SpriteRenderer found — eye color skipped.");
    }

    /// <summary>
    /// Washes <paramref name="c"/> towards white by <see cref="eyeLightenAmount"/> and then scales the
    /// RGB by <see cref="eyeColorIntensity"/>. Alpha is passed through untouched — the caller decides
    /// the eye's transparency.
    /// </summary>
    protected Color Lighten(Color c)
    {
        Color result = Color.Lerp(c, Color.white, Mathf.Clamp01(eyeLightenAmount));

        float intensity = Mathf.Max(0f, eyeColorIntensity);
        result.r *= intensity;
        result.g *= intensity;
        result.b *= intensity;
        result.a = c.a;

        return result;
    }

    /// <summary>Eye renderers: the SpriteRenderers on everything tagged <see cref="eyeTag"/>.</summary>
    private List<SpriteRenderer> ResolveEyeRenderers()
    {
        List<SpriteRenderer> found = new List<SpriteRenderer>();
        if (string.IsNullOrEmpty(eyeTag)) return found;

        foreach (GameObject go in GameObject.FindGameObjectsWithTag(eyeTag))
        {
            if (!go) continue;
            foreach (SpriteRenderer sr in go.GetComponentsInChildren<SpriteRenderer>(true))
                if (sr && !found.Contains(sr)) found.Add(sr);
        }

        return found;
    }

    /// <summary>Environment light to recolor: the assigned one, else the sun, else the first Light found.</summary>
    private Light ResolveEnvironmentLight()
    {
        if (environmentLight) return environmentLight;
        if (RenderSettings.sun) environmentLight = RenderSettings.sun;
        else environmentLight = FindFirstObjectByType<Light>();
        return environmentLight;
    }

    public virtual IEnumerator DialogueStart()
    {
        // Bring in this sin's soundtrack (crossfades up from the silence left by OnWin's fade-out) and
        // loop it through the cutscene. What happens when the cutscene ends is decided in CutsceneEnd
        // based on continuePlayingOst.
        if (soundtrack && BGMManager.Instance) BGMManager.Instance.PlayMusic(soundtrack, fadeTime:ostFadeIn);

        // The placeholder is for repeats *within the same run* only: the first time a sin comes up in
        // a run it always gets its normal dialogue, no matter how many earlier runs already showed it.
        // Only the second and further times the same suit is chosen in this run fall back to a
        // placeholder. (Falls back to the normal dialogue if no placeholders are authored.)
        bool repeatThisRun = TableManager.Instance && TableManager.Instance.HasPlayedThisRun(sin);

        DialogueContainer desiredDialogue = sinDialogue;
        if (repeatThisRun && placeholderDialogues != null && placeholderDialogues.Count > 0)
        {
            desiredDialogue = h.RandChoice(placeholderDialogues);
        }

        void OnDialogueEnd()
        {
            StartCoroutine(CutsceneEnd());
            DialogueManager.Instance.onDialogueEnd.RemoveListener(OnDialogueEnd);
        }
        
        DialogueManager.Instance.onDialogueEnd.AddListener(OnDialogueEnd);
        DialogueManager.Instance.StartDialogue(desiredDialogue);
        
        yield return null;
        // 
    }

    public virtual IEnumerator CutsceneEnd()
    {
        TableTopCameraController.Instance.SwitchToFree();

        // Fade the AngelFeel object's color back to how it was before ChangeEnvironment tinted it,
        // smoothly over 2 seconds instead of snapping.
        if (angelFill && angelFeelColorCaptured && angelFeelRenderer)
        {
            Tween.Custom(angelFeelRenderer, angelFeelRenderer.color, angelFeelOriginalColor, 2f,
                (SpriteRenderer r, Color c) => r.color = c);
            angelFeelColorCaptured = false;
        }

        // Fade the eye(s) back to the color they had before LightenEye brightened them.
        foreach (EyeTintState state in eyeStates)
        {
            if (state == null || !state.renderer) continue;

            SpriteRenderer sr = state.renderer;
            Tween.Custom(sr, sr.color, state.originalColor, 2f,
                (SpriteRenderer r, Color c) => { if (r) r.color = c; });
        }
        eyeStates.Clear();

        TableManager.Instance.AddPlayedCutscene(sin);

        // Fold the sin-based extra cards into the pile and show them full-screen; this waits for the
        // player to dismiss the preview and for the cards to burn away before the round begins.
        yield return CardManager.Instance.ExtendPileAccordingToSins(true);
        

        TableTopCameraController.Instance.SwitchToHandView();
        CardManager.Instance.RoundStart();

        // Hand card interaction back to the player now the cutscene is over.
        if (CardDragController.Instance) CardDragController.Instance.SetDraggingEnabled(true);

        // Unless the sin's soundtrack is meant to keep playing, hand the music back to a random
        // background track from BGMManager now the cutscene has ended.
        if (!continuePlayingOst && BGMManager.Instance) BGMManager.Instance.PlayRandomBgTrack();

        yield return null;

        // The cutscene is only truly finished now — customDestroy kept it alive for this. Tear it down.
        DestroyCutscene();
    }

}