using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Nudges this object toward the cursor for a subtle parallax feel.
/// Attach to a UI object (e.g. PreviewParent). Cursor top-right => moves top-right slightly.
/// Works on RectTransform (UI) or a plain Transform.
/// </summary>
public class CursorParallax : MonoBehaviour
{
    [Tooltip("Max offset in local units (UI: pixels) at the screen edge.")]
    [SerializeField] float maxOffset = 30f;

    [Tooltip("How quickly it eases toward the target (higher = snappier).")]
    [SerializeField] float smoothing = 8f;

    [Tooltip("Invert direction (UI drifts away from cursor instead of toward it).")]
    [SerializeField] bool invert = false;

    [Tooltip("Optionally react to vertical cursor movement too.")]
    [SerializeField] bool useVertical = true;

    RectTransform _rect;
    Vector3 _basePos;

    void Awake()
    {
        _rect = transform as RectTransform;
        _basePos = transform.localPosition;
    }

    void Update()
    {
        if (Mouse.current == null) return;

        Vector2 mouse = Mouse.current.position.ReadValue();

        // -1..1 offset from screen center
        float nx = Mathf.Clamp((mouse.x / Screen.width) * 2f - 1f, -1f, 1f);
        float ny = Mathf.Clamp((mouse.y / Screen.height) * 2f - 1f, -1f, 1f);

        if (!useVertical) ny = 0f;
        if (invert) { nx = -nx; ny = -ny; }

        Vector3 target = _basePos + new Vector3(nx, ny, 0f) * maxOffset;

        transform.localPosition = Vector3.Lerp(
            transform.localPosition,
            target,
            1f - Mathf.Exp(-smoothing * Time.deltaTime) // frame-rate independent
        );
    }
}
