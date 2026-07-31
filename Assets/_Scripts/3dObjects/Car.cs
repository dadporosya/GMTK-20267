using System;
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
    [SerializeField] private Transform carStartPoint;

    [Tooltip("Duration of the trip in seconds.")]
    public float tripDuration = 3f;

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
            if (t.CompareTag("Wheel"))
                wheels.Add(t.gameObject);
        }

        h.Out($"Car '{name}' found {wheels.Count} wheel(s).");
    }

    // private void Update()
    // {
    //     if (Input.GetKeyDown(KeyCode.E))
    //     {
    //         transform.position = carStartPoint.position;
    //         MoveToThePoint();
    //     }
    // }

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
        if (distance <= 0.0001f || tripDuration <= 0f)
            return;

        float circumference = 2f * Mathf.PI * Mathf.Max(0.0001f, wheelRadius);
        float totalWheelRotation = (distance / circumference) * 360f;

        // Track the accumulated wheel rotation to calculate delta per frame
        float previousRotationProgress = 0f;

        Tween.Custom(0f, 1f, tripDuration, ease: movementEase, onValueChange: t =>
        {
            Vector3 newPos = Vector3.Lerp(startPos, endPos, t);
            transform.position = newPos;

            // Calculate wheel rotation based on progress, not frame-dependent distance
            float currentRotationProgress = t * totalWheelRotation;
            float rotationDelta = currentRotationProgress - previousRotationProgress;
            RotateWheels(rotationDelta);

            previousRotationProgress = currentRotationProgress;
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