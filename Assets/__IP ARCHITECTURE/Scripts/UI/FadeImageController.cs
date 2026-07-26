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

    private Tween _fadeTween;

    private void Awake()
    {
        if (!image) image = GetComponent<Image>();
    }

    /// <summary>Modulates the image's alpha to fully opaque (1).</summary>
    public Tween fadeIn(float duration, Ease easing = Ease.Default)
    {
        if (!image) image = GetComponent<Image>();
        _fadeTween.Stop();
        _fadeTween = Tween.Alpha(image, 1f, duration, easing);
        return _fadeTween;
    }

    /// <summary>Modulates the image's alpha to fully transparent (0).</summary>
    public Tween fadeout(float duration, Ease easing = Ease.Default)
    {
        if (!image) image = GetComponent<Image>();
        _fadeTween.Stop();
        _fadeTween = Tween.Alpha(image, 0f, duration, easing);
        return _fadeTween;
    }
}
