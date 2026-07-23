using TMPro;
using UnityEngine;

public class TextChangeAnimationNEW : MonoBehaviour
{
    private TMP_Text textComponent;
    private string oldText;
    [SerializeField] private bool animateOnTextChange=false;
    public AnimationBase animation;

    private void Start()
    {
        if (!textComponent) textComponent = GetComponent<TMP_Text>();
        oldText = textComponent.text;
    }
    
    private void FixedUpdate()
    {
        if (animateOnTextChange && oldText != textComponent.text)
        {
            PlayAnimation();
        }
    }

    public void PlayAnimation()
    {
        StartCoroutine(animation.Play());
    }
}