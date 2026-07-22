using System.Collections;
using TMPro;
using UnityEngine;

public class TypewriterText : MonoBehaviour
{
    private TMP_Text textBox;
    private int currentVisibleCharacterIndex;

    private WaitForSeconds characterDelay;
    private WaitForSeconds interpunctuationDelay;
    private Coroutine typeWriterCoroutine;

    [HideInInspector] public bool isTypewriting = false;

    [SerializeField] float charactersPerSecond;
    [SerializeField] float interpunctiationSecondsDelay;
    [SerializeField] bool canSkip;

    private bool stopped;

    private void Awake()
    {
        textBox = GetComponent<TMP_Text>();
        characterDelay = new WaitForSeconds(1 / charactersPerSecond);
        interpunctuationDelay = new WaitForSeconds(interpunctiationSecondsDelay);

    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            if (!(textBox.maxVisibleCharacters >= textBox.textInfo.characterCount) && !(textBox.maxVisibleCharacters <= 1) && canSkip)
            {
                Skip();
            }
        }
    }



    public void SetText(string text)
    {
        if (typeWriterCoroutine != null)
        {
            StopCoroutine(typeWriterCoroutine);
        }

        textBox.text = text;
        textBox.maxVisibleCharacters = 0;
        currentVisibleCharacterIndex = 0;

        typeWriterCoroutine = StartCoroutine(TypewriteText());
    }

    public void SetTextSpeed(float cps)
    {
        charactersPerSecond = cps;
        characterDelay = new WaitForSeconds(1 / charactersPerSecond);
    }

    private IEnumerator TypewriteText()
    {
        textBox.ForceMeshUpdate();
        TMP_TextInfo textInfo = textBox.textInfo;
        isTypewriting = true;

        while (currentVisibleCharacterIndex < textInfo.characterCount)
        {
            if (!stopped)
            {
                char character = textInfo.characterInfo[currentVisibleCharacterIndex].character;
                textBox.maxVisibleCharacters++;

                if (character == '?' || character == '.' || character == ',' || character == ':' || character == ';' || character == '!' || character == '-')
                {
                    yield return interpunctuationDelay;
                }
                else
                {
                    yield return characterDelay;
                }

                currentVisibleCharacterIndex++;

            }
            else
            {
                yield return null;
            }
        }
        isTypewriting = false;
    }

    public void Pause(bool pause)
    {
        stopped = pause;
    }

    private void Skip()
    {
        StopCoroutine(typeWriterCoroutine);
        if (textBox.maxVisibleCharacters != textBox.textInfo.characterCount)
        {
            textBox.maxVisibleCharacters = textBox.textInfo.characterCount;
        }

    }

    public void SetSkippable(bool skippable)
    {
        canSkip = skippable;
    }
}