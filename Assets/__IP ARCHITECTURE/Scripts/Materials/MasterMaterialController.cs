using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Drives the MasterMat shader properties on this object's renderer, and optionally on every
// renderer below it.
//
// Works with both worldspace renderers (SpriteRenderer/MeshRenderer/...) and UI Graphics
// (Image/RawImage/TMP): a UI Graphic is NOT a Renderer, so it has to be handled separately,
// and it needs the UI variant of the material (MasterMatUI) because the canvas shader carries
// the stencil/mask properties the regular MasterMat lacks. Both variants expose the same
// property names, so everything below this point is shared.
public class MasterMaterialController : MonoBehaviour
{
    [Header("Setup")]
    [Tooltip("Assign a MasterMat instance to every renderer below this one. Renderers that have their own MasterMaterialController (or sit under one that propagates) are left alone.")]
    [SerializeField] private bool assignMatToChildren=true;

    private Renderer _rend;
    private Graphic _graphic;
    private Material _mat;
    private readonly List<Material> _childMats = new();

    [Header("Brightness")]
    [SerializeField] private bool brightness;
    [SerializeField] private float brightnessAmount;

    [Header("Contrast")]
    [SerializeField] private bool contrast;
    [SerializeField] private float contrastAmount;

    [Header("Dissolve")]
    [SerializeField] private bool dissolve;
    [Tooltip("0 = fully visible, 1 = fully dissolved.")]
    [Range(0f, 1f)][SerializeField] private float dissolveAmount;
    [SerializeField] private float dissolveScale = 10f;
    [SerializeField] private float dissolveOutlineThickness = 0.1f;
    [SerializeField] private float dissolveTwirl;
    [SerializeField] private float dissolveTwirlStrength = 1f;
    [SerializeField] private Color dissolveEdgeColor = Color.white;

    [Header("Distortion")]
    [SerializeField] private bool distortion;
    [SerializeField] private float distortionScale = 10f;
    [SerializeField] private float distortionStrength = 1f;
    [SerializeField] private Color distortionDirection = new Color(0f, 1f, 0f, 0f);

    [Header("Emission")]
    [SerializeField] private bool emission;
    [SerializeField] private float emissionMult = 1.9f;

    [Header("Hit")]
    [SerializeField] private bool hitEffect;
    [SerializeField] private float hitBlend;
    [SerializeField] private float hitGlow = 5f;
    [SerializeField] private Color hitColor = Color.white;

    [Header("Hue Shift")]
    [SerializeField] private bool hue;
    [SerializeField] private float hueShiftAmount;
    [SerializeField] private bool hueShiftAnimation;
    [SerializeField] private float hueShiftAnimationSpeed;

    [Header("Negative")]
    [SerializeField] private bool negative;
    [SerializeField] private float negativeAmount;

    [Header("Pixelation")]
    [SerializeField] private bool pixelation;
    [SerializeField] private float pixelResolution = 16f;

    [Header("Saturation")]
    [SerializeField] private bool saturation;
    [SerializeField] private float saturationAmount;

    private void Awake()
    {
        _rend = GetComponent<Renderer>();
        // Only look for a Graphic when there is no Renderer: the two are mutually exclusive
        // in practice, and a Renderer takes priority if something ever has both.
        if (_rend == null) _graphic = GetComponent<Graphic>();

        if (_rend != null)
        {
            _mat = NewMat(false);
            _rend.material = _mat;
        }
        else if (_graphic != null)
        {
            _mat = NewMat(true);
            _graphic.material = _mat;
        }

        if (assignMatToChildren)
            SetupChildren();

        ApplyAll();
    }

    // ui = true picks the canvas variant of the shader. Both share property names.
    private static Material NewMat(bool ui)
    {
        Material source = ui ? R.ARCHITECTURE.Materials.MasterMatUI : R.ARCHITECTURE.Materials.MasterMat;
        return source != null ? new Material(source) : null;
    }

    private void SetupChildren()
    {
        _childMats.Clear();

        // Renderer, not SpriteRenderer: models built from MeshRenderer/SkinnedMeshRenderer
        // parts were previously skipped entirely and never received the MasterMat.
        foreach (var rend in GetComponentsInChildren<Renderer>(true))
        {
            if (rend.gameObject == gameObject) continue;
            if (IsOwnedByNestedController(rend.transform)) continue;

            var childMat = NewMat(false);
            if (childMat == null) continue;
            rend.material = childMat;
            _childMats.Add(childMat);
        }

        // UI children. Graphic does not derive from Renderer, so the loop above never sees
        // Images/RawImages/TMP text — this is why the effects silently did nothing on UI
        // prefabs. These get the MasterMatUI variant instead.
        foreach (var graphic in GetComponentsInChildren<Graphic>(true))
        {
            if (graphic.gameObject == gameObject) continue;
            if (IsOwnedByNestedController(graphic.transform)) continue;

            var childMat = NewMat(true);
            if (childMat == null) continue;
            graphic.material = childMat;
            _childMats.Add(childMat);
        }
    }

    // True when some MasterMaterialController below this one already claims that renderer.
    // Without this check both controllers would assign their own new Material to the same
    // renderer in Awake (order is undefined), and the loser would keep writing properties to
    // a material that is no longer rendered — the effect silently does nothing.
    private bool IsOwnedByNestedController(Transform t)
    {
        for (var cur = t; cur != null && cur != transform; cur = cur.parent)
        {
            var nested = cur.GetComponent<MasterMaterialController>();
            if (nested == null) continue;
            // A nested controller always owns its own renderer, and owns the ones further
            // down only when it is set to propagate to its children.
            if (cur == t || nested.assignMatToChildren) return true;
        }
        return false;
    }

    public void SetPixelation(bool value)
    {
        pixelation = value;
        ApplyAll();
    }

    public void SetPixelResolution(float value)
    {
        pixelResolution = value;
        ApplyAll();
    }

    public void SetHitEffect(bool value)
    {
        hitEffect = value;
        ApplyAll();
    }

    public void SetHitBlend(float value)
    {
        hitBlend = value;
        ApplyAll();
    }

    public void SetHitColor(Color value)
    {
        hitColor = value;
        ApplyAll();
    }

    public void SetDissolve(bool value)
    {
        dissolve = value;
        ApplyAll();
    }

    public float GetDissolveAmount() => dissolveAmount;

    // Fast path for per-frame animation: writes only the dissolve amount instead of
    // re-uploading every property through ApplyAll().
    public void SetDissolveAmount(float value)
    {
        dissolveAmount = value;
        if (_mat != null) _mat.SetFloat("_Dissolve_amount", value);
        foreach (var m in _childMats)
            if (m != null) m.SetFloat("_Dissolve_amount", value);
    }

    public void ApplyAll()
    {
        ApplyTo(_mat);
        foreach (var m in _childMats)
            ApplyTo(m);
    }

    private void ApplyTo(Material m)
    {
        if (m == null) return;

        m.SetFloat("_Brightness", brightness ? 1f : 0f);
        m.SetFloat("_Brightness_Amount", brightnessAmount);

        m.SetFloat("_Contrast", contrast ? 1f : 0f);
        m.SetFloat("_Contrast_Amount", contrastAmount);

        m.SetFloat("_Disslove", dissolve ? 1f : 0f);
        m.SetFloat("_Dissolve_amount", dissolveAmount);
        m.SetFloat("_DissloveScale", dissolveScale);
        m.SetFloat("_DissloveOutlinesthickness", dissolveOutlineThickness);
        m.SetFloat("_DissloveTwirl", dissolveTwirl);
        m.SetFloat("_DissloveTwirlStrength", dissolveTwirlStrength);
        m.SetColor("_EdgeColor", dissolveEdgeColor);

        m.SetFloat("_Distortion", distortion ? 1f : 0f);
        m.SetFloat("_DistortionScale", distortionScale);
        m.SetFloat("_DistortionStrength", distortionStrength);
        m.SetColor("_Distortion_Direction", distortionDirection);

        m.SetFloat("_Emission", emission ? 1f : 0f);
        m.SetFloat("_Emission_Mult", emissionMult);

        m.SetFloat("_Hit_Effect", hitEffect ? 1f : 0f);
        m.SetFloat("_Hit_Blend", hitBlend);
        m.SetFloat("_Hit_Glow", hitGlow);
        m.SetColor("_Hit_Color", hitColor);

        m.SetFloat("_Hue", hue ? 1f : 0f);
        m.SetFloat("_HueShiftAmount", hueShiftAmount);
        m.SetFloat("_HueShiftAnimation", hueShiftAnimation ? 1f : 0f);
        m.SetFloat("_HueShiftAnimationSpeed", hueShiftAnimationSpeed);

        m.SetFloat("_Negative", negative ? 1f : 0f);
        m.SetFloat("_Negative_Amount", negativeAmount);

        m.SetFloat("_Pixelation", pixelation ? 1f : 0f);
        m.SetFloat("_Pixel_Resolution", pixelResolution);

        m.SetFloat("_Saturation", saturation ? 1f : 0f);
        m.SetFloat("_Saturation_Amount", saturationAmount);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (_mat != null || _childMats.Count > 0)
            ApplyAll();
    }
#endif
}
