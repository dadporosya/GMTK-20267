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


        yield return null;
    }
    
    
    public virtual IEnumerator OnWin()
    {
        // TODO: some onWin effects like sfx and stuff
        yield return new WaitForSeconds(delayAfterWin);
        
        yield return null;
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
            StartCoroutine(DialogueEnd());
            DialogueManager.Instance.onDialogueEnd.RemoveListener(OnDialogueEnd);
        }
        
        DialogueManager.Instance.onDialogueEnd.AddListener(OnDialogueEnd);
        DialogueManager.Instance.StartDialogue(desiredDialogue);
        
        yield return null;
        // 
    }
    
    public virtual IEnumerator DialogueEnd()
    {
        if (!TableTopCameraController.Instance) yield break;
        TableTopCameraController.Instance.SwitchToWindowView();
        yield return null;
    }
}