using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Cursor parallax for the camera: the further the mouse is from the screen
/// centre, the more the camera tilts (and optionally shifts) toward it, giving a
/// subtle "look around" / parallax feel.
///
/// COMPOSES with other camera drivers instead of fighting them. This project's
/// <see cref="TableTopCameraController"/> tweens the camera's own local pose, and
/// <see cref="BalatroScreenShake"/> / MouseLook also write the camera transform.
/// If this component simply overwrote the transform every frame it would override
/// those (which is the bug this version fixes). Instead, each LateUpdate it:
///   1. reads the transform's current local pose,
///   2. decides whether an external system wrote it since we last wrote (by
///      comparing against the exact pose we left behind),
///   3. treats that external pose as the neutral "base" and layers ONLY the small
///      parallax offset on top.
/// So the tilt rides along with whatever the table-top controller / shake is doing,
/// and cleanly returns to zero when the cursor is centred — no drift, no stomping.
///
/// Drop it straight onto the Camera object (alongside the table-top controller).
/// Runs late so it sees the other systems' pose for the frame before adding tilt.
/// </summary>
[DefaultExecutionOrder(10000)]
public class CameraDynamicMovement : MonoBehaviour
{
    [Header("Rotation (tilt)")]
    [Tooltip("Max tilt in degrees at the screen edges. X = pitch (up/down), Y = yaw (left/right).")]
    [SerializeField] private Vector2 maxTiltAngles = new Vector2(3f, 4f);

    [Tooltip("Invert the tilt direction (look 'away' from the cursor instead of toward it).")]
    [SerializeField] private bool invertTilt = false;

    [Header("Position (optional shift)")]
    [Tooltip("Max LOCAL position offset at the screen edges. Leave at 0 for tilt-only parallax.")]
    [SerializeField] private Vector2 maxPositionOffset = Vector2.zero;

    [Header("Feel")]
    [Tooltip("How quickly the tilt eases toward the target. Higher = snappier.")]
    [SerializeField] private float smoothSpeed = 6f;

    [Tooltip("Dead zone (0..1) around the screen centre where no movement is applied.")]
    [Range(0f, 0.9f)]
    [SerializeField] private float deadZone = 0.05f;

    [Tooltip("Ease the normalized cursor offset with a smoothstep so the centre stays calm and edges feel natural.")]
    [SerializeField] private bool smoothFalloff = true;

    [Header("Runtime")]
    [Tooltip("When off, the tilt eases back to zero and the camera is left entirely to the other drivers.")]
    [SerializeField] private bool active = true;

    // Current smoothed cursor offset in [-1, 1] on each axis.
    private Vector2 currentOffset;

    // The exact local pose we wrote last frame, and the offset we baked into it, so
    // next frame we can tell our own contribution apart from an external write and
    // strip it back off before re-applying.
    private bool hasWritten;
    private Vector3 lastWrittenLocalPos;
    private Quaternion lastWrittenLocalRot;
    private Vector3 appliedPosOffset;
    private Quaternion appliedRotOffset = Quaternion.identity;

    private void LateUpdate()
    {
        Vector2 target = active ? ReadCursorOffset() : Vector2.zero;

        float t = 1f - Mathf.Exp(-smoothSpeed * Time.deltaTime); // frame-rate independent
        currentOffset = Vector2.Lerp(currentOffset, target, t);

        // --- work out the neutral base pose for this frame -------------------
        Vector3 curPos = transform.localPosition;
        Quaternion curRot = transform.localRotation;

        Vector3 basePos;
        Quaternion baseRot;
        if (hasWritten
            && Vector3.SqrMagnitude(curPos - lastWrittenLocalPos) < 1e-8f
            && Quaternion.Angle(curRot, lastWrittenLocalRot) < 0.01f)
        {
            // Nobody else touched the transform since we wrote it: peel our own
            // offset back off to recover the true base.
            basePos = curPos - appliedPosOffset;
            baseRot = curRot * Quaternion.Inverse(appliedRotOffset);
        }
        else
        {
            // A camera driver (table-top controller / shake / mouse look) wrote the
            // transform this frame: adopt whatever it set as the base and ride on top.
            basePos = curPos;
            baseRot = curRot;
        }

        // --- build this frame's parallax offset ------------------------------
        float sign = invertTilt ? -1f : 1f;
        float pitch = -currentOffset.y * maxTiltAngles.x * sign;
        float yaw = currentOffset.x * maxTiltAngles.y * sign;
        Quaternion rotOffset = Quaternion.Euler(pitch, yaw, 0f);

        Vector3 posOffset = maxPositionOffset == Vector2.zero
            ? Vector3.zero
            : new Vector3(currentOffset.x * maxPositionOffset.x * sign,
                          currentOffset.y * maxPositionOffset.y * sign, 0f);

        // --- apply & remember ------------------------------------------------
        Quaternion newRot = baseRot * rotOffset;
        Vector3 newPos = basePos + posOffset;

        transform.localRotation = newRot;
        transform.localPosition = newPos;

        lastWrittenLocalRot = newRot;
        lastWrittenLocalPos = newPos;
        appliedRotOffset = rotOffset;
        appliedPosOffset = posOffset;
        hasWritten = true;
    }

    /// <summary>
    /// Cursor position relative to the screen centre, normalized to ~[-1, 1] per
    /// axis, with dead zone and optional smoothstep applied.
    /// </summary>
    private Vector2 ReadCursorOffset()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null) return Vector2.zero;

        Vector2 pos = mouse.position.ReadValue();
        Vector2 half = new Vector2(Screen.width, Screen.height) * 0.5f;
        if (half.x <= 0f || half.y <= 0f) return Vector2.zero;

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
        return Mathf.Sign(v) * (a - deadZone) / (1f - deadZone);
    }

    private static float SmoothStepSigned(float v)
    {
        float a = Mathf.Abs(v);
        return Mathf.Sign(v) * (a * a * (3f - 2f * a));
    }

    /// <summary>Enable/disable the parallax; it eases back to neutral when disabled.</summary>
    public void SetActive(bool value) => active = value;
}
