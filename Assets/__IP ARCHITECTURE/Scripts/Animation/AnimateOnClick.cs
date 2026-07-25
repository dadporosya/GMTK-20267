using UnityEngine;

/// <summary>
/// Plays an <see cref="AnimationControllerBase"/> when the SpriteRenderer is clicked.
/// The controller collects its matching-type animations itself in Awake;
/// this component just triggers PlayAnimations on click.
/// Requires a Collider on the same object for OnMouseDown to fire.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class AnimateOnClick : MonoBehaviour
{
    [SerializeField] private AnimationControllerBase animationController;
    [SerializeField] private bool ignoreWhilePlaying = true;

    private bool isPlaying;

    private void Awake()
    {
        if (animationController == null)
            animationController = GetComponent<AnimationControllerBase>();
    }

    private void OnMouseDown()
    {
        if (animationController == null)
        {
            h.Out("AnimateOnClick: no AnimationControllerBase assigned.");
            return;
        }

        if (ignoreWhilePlaying && isPlaying) return;

        isPlaying = true;
        StartCoroutine(animationController.PlayAnimations(() => isPlaying = false));
    }
}
