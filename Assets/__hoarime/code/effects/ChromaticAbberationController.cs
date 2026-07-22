using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class ChromaticAbberationController : MonoBehaviour
{
    [SerializeField] Volume globalVolume;
    [SerializeField] float fadeChromaticAbberationTime;
    [SerializeField] float vibrationFadeValueMax;
    [SerializeField] float vibrationFadeValueMin;

    private ChromaticAberration chromaticAberration;

    private Coroutine chromaticAbberationCoroutine;
    private Coroutine fadeCACoroutine;

    void Awake()
    {


        if (globalVolume.profile.TryGet(out chromaticAberration))
        {
            Debug.Log("chromatic abberation");
        }
    }

    public void FadeChromaticAbberation(float fadeTo)
    {
        if(fadeCACoroutine != null)
        {
            StopCoroutine(fadeCACoroutine);
        }
        fadeCACoroutine = StartCoroutine(FadeChromaticAbberation_c(fadeTo));
    }

    public void SetCAFadeTime(float time)
    {
        fadeChromaticAbberationTime = time;
    } 

    private IEnumerator FadeChromaticAbberation_c(float fadeTo)
    {
        float targetValue = fadeTo;
        float startValue = chromaticAberration.intensity.value;

        float elapsedTime = 0;
        float progress;

        while (elapsedTime < fadeChromaticAbberationTime)
        {
            progress = Mathf.Clamp01(elapsedTime / fadeChromaticAbberationTime);
            SetChromaticIntensity(Mathf.Lerp(startValue, targetValue, progress));
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        SetChromaticIntensity(targetValue);
    }

    public void StartAnimatingChromaticAbberation()
    {
        if(chromaticAbberationCoroutine != null)
        {
            StopCoroutine(chromaticAbberationCoroutine);
        }
        chromaticAbberationCoroutine = StartCoroutine(FadeChromaticAbberationCycle_c());
    }

    public void StopAnimatingChromaticAbberation()
    {
        StopCoroutine(chromaticAbberationCoroutine);
    }

    private IEnumerator FadeChromaticAbberationCycle_c()
    {
        while (true)
        {
            FadeChromaticAbberation(vibrationFadeValueMax);
            yield return new WaitUntil(() => chromaticAberration.intensity.value == vibrationFadeValueMax);
            FadeChromaticAbberation(vibrationFadeValueMin);
            yield return new WaitUntil(() => chromaticAberration.intensity.value == vibrationFadeValueMin);

        }
        
    }

    public void SetChromaticIntensity(float value)
    {
        if (chromaticAberration != null)
        {
            chromaticAberration.intensity.value = Mathf.Clamp01(value);
        }
    }

    
}