using PrimeTween;
using UnityEngine;

/// <summary>
/// Flips <see cref="model"/> to face its horizontal movement direction by rotating it
/// around <see cref="rotationAxis"/> (default Y) whenever the object's position along
/// <see cref="targetAxis"/> (default X) changes by more than <see cref="minDistance"/>.
/// Rotation is animated with PrimeTween.
///
/// The flip is applied to a dedicated pivot inserted between <see cref="model"/> and its
/// parent, not to the model itself. That way animations which overwrite the model's local
/// rotation (e.g. <c>RotatingAnimation</c> writing <c>localEulerAngles</c>) compose with the
/// flip through the hierarchy instead of overriding it.
/// </summary>
public class FlipByVelocity3D : MonoBehaviour
{
    public enum Axis { x, y, z }

    [Header("Axes")]
    [SerializeField] private Axis rotationAxis = Axis.y;
    [SerializeField] private Axis targetAxis = Axis.x;

    [Header("Model")]
    [SerializeField] private Transform model;
    [SerializeField] private bool rightFaced = true;

    [Header("Tween")]
    [SerializeField] private float rotationDuration = 0.25f;
    [SerializeField] private Ease rotationEasing = Ease.OutQuad;

    private readonly float minDistance = 0.001f;

    private Vector3 previousPos;
    private float currentTargetAngle = float.NaN;

    // Transform the flip rotation is written to. Owned by this component, so no other
    // animation touches it and the flip never gets overridden.
    private Transform flipPivot;

    private const string ModelTag = "model";

    private void Start()
    {
        if (!model) model = FindChildWithTag(transform, ModelTag);
        if (!model) model = transform;

        flipPivot = CreateFlipPivot(model);
        previousPos = transform.position;
    }

    private static Transform FindChildWithTag(Transform root, string tag)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>())
        {
            if (child != root && child.CompareTag(tag)) return child;
        }
        return null;
    }

    // Inserts a pivot as the model's new parent, matching the model's local transform, then
    // re-parents the model under it (preserving world placement). The flip is tweened on the
    // pivot while animations keep driving the model's own local rotation.
    private static Transform CreateFlipPivot(Transform model)
    {
        var pivot = new GameObject($"{model.name}_FlipPivot").transform;
        pivot.SetParent(model.parent, false);
        pivot.localPosition = model.localPosition;
        pivot.localRotation = Quaternion.identity;
        pivot.localScale = Vector3.one;
        model.SetParent(pivot, false);
        model.localPosition = Vector3.zero;
        return pivot;
    }

    private void Update()
    {
        Vector3 current = transform.position;
        float prev = GetAxis(previousPos, targetAxis);
        float curr = GetAxis(current, targetAxis);

        if (Mathf.Abs(curr - prev) < minDistance) return;

        float targetAngle;
        if (prev > curr) // moving in -targetAxis direction
            targetAngle = rightFaced ? 0f : 180f;
        else             // prev < curr, moving in +targetAxis direction
            targetAngle = rightFaced ? 180f : 0f;

        RotateTo(targetAngle);
        previousPos = current;
    }

    private void RotateTo(float angle)
    {
        if (!float.IsNaN(currentTargetAngle) && Mathf.Approximately(currentTargetAngle, angle)) return;
        currentTargetAngle = angle;

        Vector3 target = flipPivot.localEulerAngles;
        SetAxis(ref target, rotationAxis, angle);
        Tween.LocalRotation(flipPivot, Quaternion.Euler(target), rotationDuration, rotationEasing);
    }

    private static float GetAxis(Vector3 v, Axis axis)
    {
        switch (axis)
        {
            case Axis.x: return v.x;
            case Axis.y: return v.y;
            default:      return v.z;
        }
    }

    private static void SetAxis(ref Vector3 v, Axis axis, float value)
    {
        switch (axis)
        {
            case Axis.x: v.x = value; break;
            case Axis.y: v.y = value; break;
            default:      v.z = value; break;
        }
    }
}
