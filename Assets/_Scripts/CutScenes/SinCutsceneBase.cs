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
    [SerializeField] private DialogueContainer placeholderDialogue;
    
    [Header("After Win")]
    [SerializeField] private float delayAfterWin = 2f;
    [Tooltip("Delay between each card starting to burn, so the table and hand clear in a cascade " +
             "instead of all at once.")]
    [SerializeField] private float burnCardStagger = 0.08f;
    [Tooltip("If on, OnWin waits for every card's burn to finish before the cutscene continues.")]
    [SerializeField] private bool waitForBurnsToFinish = false;
    
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
    /// -> dialogue start (place holder if suit was previusly selected), and camera turns to angel
    /// -> after dialogue gp starts again
    /// </summary>
    /// <returns></returns>
    
    public virtual IEnumerator DefaultStep()
    {
        yield return null;
        
        
        yield return OnWin();
        yield return TurnCameraToWindow();
        yield return ChangeEnvironment();
        yield return DialogueStart();


        yield return null;
    }
    
    
    public virtual IEnumerator OnWin()
    {
        // Burn every card on the table and in hand.
        yield return BurnAllCards();
        // TODO: some onWin effects like sfx and stuff
        yield return new WaitForSeconds(delayAfterWin);
        yield return null;
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
        Tween fade = Tween.Custom(target, target.color, lightColor, colorFadeDuration,
            (Light l, Color c) => l.color = c);
        yield return fade.ToYieldInstruction();

        yield return new WaitForSeconds(delayAfterColorChange);
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
        DialogueContainer desiredDialogue;
        if (TableManager.Instance.playedCutScenes.Contains(sin))
        {
            desiredDialogue = placeholderDialogue;
        }
        else
        {
            desiredDialogue = sinDialogue;
        }

        void OnDialogueEnd()
        {
            h.Out("Dialogue End Event BEBRA");
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
        if (TableTopCameraController.Instance)
        {
            TableTopCameraController.Instance.SwitchToHandView();
            
        }
        
        TableManager.Instance.AddPlayedCutscene(sin);
        CardManager.Instance.ExtendPileAccordingToSins();
        CardManager.Instance.RoundStart();

        yield return null;

        // The cutscene is only truly finished now — customDestroy kept it alive for this. Tear it down.
        DestroyCutscene();
    }

}