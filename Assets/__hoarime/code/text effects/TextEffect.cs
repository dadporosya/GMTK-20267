using UnityEngine;
using TMPro;
using UnityEngine.EventSystems;
using System.Collections;
using UnityEngine.Rendering.Universal;

public class TextEffect : MonoBehaviour
{
    private TMP_Text textMesh;
    private Material textMaterial;

    [Header("Settings")]

    [SerializeField] float offsetFadeTime;

    [SerializeField] float minDilate;
    [SerializeField] float maxDilate;

    private float currentDilate;

    private TMP_Text animatedText;


    private Coroutine textAnimCoroutine;


    void Start()
    {
        textMesh = animatedText.GetComponent<TMP_Text>();
        textMaterial = textMesh.fontMaterial;

    }

    private void Awake()
    {
        currentDilate = minDilate;
        animatedText = GetComponent<TMP_Text>();
        if (textAnimCoroutine != null)
        {
            EndOffsetFadeAnimation();
        }
        StartOffsetFadeAnimation();
    }

    private void OnEnable()
    {
        if (textAnimCoroutine != null)
        {
            EndOffsetFadeAnimation();
        }
        StartOffsetFadeAnimation();
    }



    void Update()
    {

        textMaterial.SetFloat(ShaderUtilities.ID_FaceDilate, currentDilate);


    }

    private IEnumerator UnderlayOffsetFade(float fadeTo)
    {
        float targetValue = fadeTo;
        float startValue = currentDilate;

        float elapsedTime = 0;
        float progress;

        while (elapsedTime < offsetFadeTime)
        {
            progress = Mathf.Clamp01(elapsedTime / offsetFadeTime);
            currentDilate = Mathf.Lerp(startValue, targetValue, progress * progress);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        currentDilate = targetValue;


    }

    public void StartOffsetFadeAnimation()
    {
        textAnimCoroutine = StartCoroutine(OffsetFadeAnimation_c());
    }

    public void EndOffsetFadeAnimation()
    {
        StopCoroutine(textAnimCoroutine);
    }

    private IEnumerator OffsetFadeAnimation_c()
    {
        while (true)
        {
            StartCoroutine(UnderlayOffsetFade(maxDilate));
            yield return new WaitUntil(() => currentDilate == maxDilate);
            StartCoroutine(UnderlayOffsetFade(minDilate));
            yield return new WaitUntil(() => currentDilate == minDilate);
        }

    }







}