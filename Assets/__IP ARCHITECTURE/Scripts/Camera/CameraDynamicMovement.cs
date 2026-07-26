using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Cursor parallax for the camera: the further the mouse is from the screen
/// centre, the more the camera tilts (and optionally shifts) toward it,
/// giving a subtle "look around" / parallax feel.
///
/// Recommended setup: put this on a dedicated empty pivot that is a PARENT of
/// the actual Camera (and of anything that writes the camera's transform
/// directly, like <see cref="BalatroScreenShake"/>). That way the parallax
/// tilt composes cleanly with screen shake and <see cref="CameraFlowTargeting"/>
/// follow instead of fighting them. It also works directly on the camera if
/// nothing else drives its local rotation.
///
/// The component captures its starting LOCAL position/rotation as the neutral
/// pose, so it only ever adds an offset on top of whatever the parent does.
/// </summary>
public class CameraDynamicMovement : MonoBehaviour
{
    [Header("Rotation (tilt)")]
    [Tooltip("Max tilt in degrees at the screen edges. X = pitch (up/down), Y = yaw (left/right).")]
    [SerializeField] private Vector2 maxTiltAngles = new Vector2(3f, 4f);

    [Tooltip("Invert the tilt direction (look 'away' from the cursor instead of toward it).")]
    [SerializeField] private bool invertTilt = false;

    [Header("Position (optional shift)")]
    [Tooltip("Max local position offset at the screen edges. Leave at 0 for tilt-only parallax.")]
    [SerializeField] private Vector2 maxPositionOffset = Vector2.zero;

    [Header("Feel")]
    [Tooltip("How quickly the camera eases toward the target pose. Higher = snappier.")]
    [SerializeField] private float smoothSpeed = 6f;

    [Tooltip("Dead zone (0..1) around the screen centre where no movement is applied.")]
    [Range(0f, 0.9f)]
    [SerializeField] private float deadZone = 0.05f;

    [Tooltip("Ease the normalized cursor offset with a smoothstep so the centre stays calm and edges feel natural.")]
    [SerializeField] private bool smoothFalloff = true;

    [Header("Runtime")]
    [Tooltip("When off, the camera eases back to the neutral pose.")]
    [SerializeField] private bool active = true;

    private Vector3 baseLocalPosition;
    private Quaternion baseLocalRotation;

    // Current smoothed cursor offset in [-1, 1] on each axis.
    private Vector2 currentOffset;

    private void Start()
    {
        baseLocalPosition = transform.localPosition;
        baseLocalRotation = transform.localRotation;
    }

    private void LateUpdate()
    {
        Vector2 target = active ? ReadCursorOffset() : Vector2.zero;

        // Frame-rate independent smoothing toward the target offset.
        float t = 1f - Mathf.Exp(-smoothSpeed * Time.deltaTime);
        currentOffset = Vector2.Lerp(currentOffset, target, t);

        // Tilt: horizontal cursor -> yaw (Y), vertical cursor -> pitch (X, inverted so
        // moving the mouse up tilts the view up).
        float sign = invertTilt ? -1f : 1f;
        float pitch = -currentOffset.y * maxTiltAngles.x * sign;
        float yaw = currentOffset.x * maxTiltAngles.y * sign;

        transform.localRotation = baseLocalRotation * Quaternion.Euler(pitch, yaw, 0f);

        // Optional positional parallax on the local XY plane (screen-aligned).
        if (maxPositionOffset != Vector2.zero)
        {
            Vector3 offset = new Vector3(
                currentOffset.x * maxPositionOffset.x * sign,
                currentOffset.y * maxPositionOffset.y * sign,
                0f);
            transform.localPosition = baseLocalPosition + offset;
        }
    }

    /// <summary>
    /// Returns the cursor position relative to the screen centre, normalized to
    /// roughly [-1, 1] per axis, with dead zone and optional smoothstep applied.
    /// </summary>
    private Vector2 ReadCursorOffset()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null) return Vector2.zero;

        Vector2 pos = mouse.position.ReadValue();
        Vector2 half = new Vector2(Screen.width, Screen.height) * 0.5f;
        if (half.x <= 0f || half.y <= 0f) return Vector2.zero;

        // -1..1 from centre, clamped (cursor can sit slightly outside the game view).
        Vector2 n = new Vector2(
            Mathf.Clamp((pos.x - half.x) / half.x, -1f, 1f),
            Mathf.Clamp((pos.y - half.y) / half.y, -1f, 1f));

        n.x = ApplyDeadZone(n.x);
        n.y = ApplyDeadZone(n.y);

        if (smoothFalloff)
        {
            n.x = SmoothStepSigned(n.x);
            n.y = SmoothStepSigned(n.y);
        }

        return n;
    }

    private float ApplyDeadZone(float v)
    {
        if (deadZone <= 0f) return v;
        float a = Mathf.Abs(v);
        if (a <= deadZone) return 0f;
        // Rescale so movement resumes from 0 just past the dead zone.
        return Mathf.Sign(v) * (a - deadZone) / (1f - deadZone);
    }

    private static float SmoothStepSigned(float v)
    {
        float a = Mathf.Abs(v);
        return Mathf.Sign(v) * (a * a * (3f - 2f * a));
    }

    /// <summary>Enable/disable the parallax; it eases back to neutral when disabled.</summary>
    public void SetActive(bool value) => active = value;

    /// <summary>Re-capture the current local pose as the neutral pose (call after repositioning the pivot).</summary>
    public void RecaptureBasePose()
    {
        baseLocalPosition = transform.localPosition;
        baseLocalRotation = transform.localRotation;
    }
}
