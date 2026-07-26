using UnityEngine;
using UnityEngine.UI;
using PrimeTween;

/// <summary>
/// Fades a UI <see cref="Image"/>'s alpha in/out using PrimeTween.
/// </summary>
[RequireComponent(typeof(Image))]
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
            FadeIn(0);
            Fadeout(startFadeDurationOut);
        }
    }

    /// <summary>Modulates the image's alpha to fully opaque (1).</summary>
    public Tween FadeIn(float duration, Ease easing = Ease.Default)
    {
        if (!image) image = GetComponent<Image>();
        _fadeTween.Stop();
        _fadeTween = Tween.Alpha(image, 1f, duration, easing);
        return _fadeTween;
    }

    /// <summary>Modulates the image's alpha to fully transparent (0).</summary>
    public Tween Fadeout(float duration, Ease easing = Ease.Default)
    {
        if (!image) image = GetComponent<Image>();
        _fadeTween.Stop();
        _fadeTween = Tween.Alpha(image, 0f, duration, easing);
        return _fadeTween;
    }
}
