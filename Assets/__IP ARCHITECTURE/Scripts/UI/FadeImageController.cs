using UnityEngine;
using UnityEngine.UI;
using PrimeTween;

public class FadeImageController : MonoBehaviour
{
    public Image image;
    public static FadeImageController Instance;

    [SerializeField] private bool enableStartFadeOut = true;
    [SerializeField] private float startFadeDurationOut = 1.67f;
    private Tween _fadeTween;

    private void Awake()
    {
        if (!image) image = GetComponent<Image>();
    }

    private void Start()
    {
        h.CreateStaticInstance(this, ref Instance);

        if (enableStartFadeOut)
        {
            // Instantly snap to full alpha, then smoothly fade out to 0.
            SetAlpha(1f);
            Fadeout(startFadeDurationOut);
        }
    }

    /// <summary>Instantly sets the image's alpha.</summary>
    public void SetAlpha(float alpha)
    {
        if (!image) image = GetComponent<Image>();
        _fadeTween.Stop();
        Color c = image.color;
        c.a = alpha;
        image.color = c;
    }

    /// <summary>Modulates the image's alpha to fully opaque (1).</summary>
    public Tween FadeIn(float duration, Ease easing = Ease.Linear)
    {
        if (!image) image = GetComponent<Image>();
        _fadeTween.Stop();
        _fadeTween = Tween.Alpha(image, 1f, duration, easing);
        return _fadeTween;
    }

    /// <summary>Modulates the image's alpha to fully transparent (0).</summary>
    public Tween Fadeout(float duration, Ease easing = Ease.Linear)
    {
        if (!image) image = GetComponent<Image>();
        _fadeTween.Stop();
        _fadeTween = Tween.Alpha(image, 0f, duration, easing);
        return _fadeTween;
    }
}
