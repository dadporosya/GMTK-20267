using System.Collections.Generic;
using UnityEngine;

public class FacedAlwaysToCamera : MonoBehaviour
{
    public enum Axes
    {
        X, Y, Z
    }
    
    public List<Axes> lockedAxes;
    public Vector3 rotationOffs;

    [Tooltip("Camera to face. Falls back to Camera.main when left empty.")]
    [SerializeField] private Camera targetCamera;

    private void Awake()
    {
        if (!targetCamera) targetCamera = Camera.main;
    }

    private void LateUpdate()
    {
        if (!targetCamera)
        {
            targetCamera = Camera.main;
            if (!targetCamera) return;
        }

        // Rotation that makes this object face the camera, plus the configured offset.
        Vector3 currentEuler = transform.eulerAngles;
        Vector3 facingEuler = (Quaternion.LookRotation(transform.position - targetCamera.transform.position) * Quaternion.Euler(rotationOffs)).eulerAngles;

        // Keep the current angle on any locked axis, use the facing angle otherwise.
        Vector3 result = new Vector3(
            lockedAxes.Contains(Axes.X) ? currentEuler.x : facingEuler.x,
            lockedAxes.Contains(Axes.Y) ? currentEuler.y : facingEuler.y,
            lockedAxes.Contains(Axes.Z) ? currentEuler.z : facingEuler.z);

        transform.eulerAngles = result;
    }
}