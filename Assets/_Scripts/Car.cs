using System.Collections.Generic;
using PrimeTween;
using UnityEngine;

/// <summary>
/// Drives a car transform toward <see cref="animEndPoint"/> starting at <see cref="initSpeed"/>
/// and naturally decelerating to a stop, spinning its wheels around their local X axis
/// in proportion to the distance actually travelled.
/// </summary>
public class Car : MonoBehaviour
{
    [Tooltip("Where the car should come to rest.")]
    public Transform animEndPoint;

    [Tooltip("Speed (units/second) at the very start of the move.")]
    public float initSpeed = 10f;

    [Tooltip("How the car slows down. An ease-out gives a natural deceleration.")]
    public Ease movementEase = Ease.OutCubic;

    [Tooltip("Effective wheel radius used to convert travelled distance into wheel spin.")]
    public float wheelRadius = 0.5f;

    [Tooltip("Auto-filled in Start() with every child tagged 'wheel'.")]
    public List<GameObject> wheels = new List<GameObject>();

    void Start()
    {
        // Find every child GameObject tagged "wheel" and assign them to the list.
        wheels.Clear();
        foreach (Transform t in GetComponentsInChildren<Transform>(true))
        {
            if (t.CompareTag("wheel"))
                wheels.Add(t.gameObject);
        }

        h.Out($"Car '{name}' found {wheels.Count} wheel(s).");
    }

    /// <summary>
    /// Moves the car to <see cref="animEndPoint"/>: it leaves at <see cref="initSpeed"/> and
    /// decelerates to a natural stop, rotating all wheels around their local X axis as it goes.
    /// </summary>
    [ContextMenu("Move To The Point")]
    public void MoveToThePoint()
    {
        if (animEndPoint == null)
        {
            h.Out("Car.MoveToThePoint: animEndPoint is not assigned.");
            return;
        }

        Vector3 startPos = transform.position;
        Vector3 endPos = animEndPoint.position;

        // Keep the car on the XZ plane; Y stays whatever the car started at.
        endPos.y = startPos.y;

        float distance = Vector3.Distance(startPos, endPos);
        if (distance <= 0.0001f || initSpeed <= 0f)
            return;

        // With an ease-out the car covers the distance while slowing down. Basing the
        // duration on distance / initSpeed makes the opening pace match initSpeed, and the
        // easing curve handles the natural slow-down toward the end.
        float duration = distance / initSpeed;

        float circumference = 2f * Mathf.PI * Mathf.Max(0.0001f, wheelRadius);
        Vector3 prevPos = startPos;

        Tween.Custom(0f, 1f, duration, ease: movementEase, onValueChange: t =>
        {
            Vector3 newPos = Vector3.Lerp(startPos, endPos, t);
            transform.position = newPos;

            // Spin the wheels by the distance travelled this frame (positive = forward roll).
            float delta = Vector3.Distance(newPos, prevPos);
            float degrees = (delta / circumference) * 360f;
            RotateWheels(degrees);

            prevPos = newPos;
        });
    }

    /// <summary>Rotates every wheel around its local X axis by the given degrees.</summary>
    void RotateWheels(float degrees)
    {
        for (int i = 0; i < wheels.Count; i++)
        {
            if (wheels[i] != null)
                wheels[i].transform.Rotate(Vector3.right, degrees, Space.Self);
        }
    }
}
