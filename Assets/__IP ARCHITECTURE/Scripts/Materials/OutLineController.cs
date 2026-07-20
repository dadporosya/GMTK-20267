using UnityEngine;

using UnityEngine;

public class OutlineController : MonoBehaviour
{
    [SerializeField] private bool fill = true;
    [SerializeField] private float outlineThickness = 100f; // must match _Thickness in material
    [SerializeField] private Color outlineColor = Color.white;

    private SpriteRenderer sourceRenderer;
    private SpriteRenderer instanceRenderer;
    private TrackedSpriteRenderer tracker;

    private void Awake()
    {
        sourceRenderer = GetComponent<SpriteRenderer>();
        if (!sourceRenderer) return;

        var outlineObj = new GameObject("Outline");
        outlineObj.transform.SetParent(transform, false);
        instanceRenderer = outlineObj.AddComponent<SpriteRenderer>();

        instanceRenderer.material       = new Material(R.ARCHITECTURE.Materials.DefaultOutlineMaterial);
        instanceRenderer.sortingLayerID = sourceRenderer.sortingLayerID;
        instanceRenderer.sortingOrder   = sourceRenderer.sortingOrder - 1;

        ApplyFillMode();
        CopyRenderer();
        ApplyOutlineScale();
        SetThickness(outlineThickness);
        SetColor(outlineColor);

        tracker = new TrackedSpriteRenderer(sourceRenderer, OnSourceChanged);
    }

    private void Update()
    {
        tracker?.Tick();

        if (Input.GetKey(KeyCode.G))
        {
            ChangeThickness(5);
        }         if (Input.GetKey(KeyCode.F))
        {
            ChangeThickness(-5);
        } 

    }

    private void OnSourceChanged()
    {
        CopyRenderer();
        ApplyOutlineScale();
    }

    private void CopyRenderer()
    {
        if (!instanceRenderer || !sourceRenderer) return;
        instanceRenderer.sprite          = sourceRenderer.sprite;
        instanceRenderer.flipX           = sourceRenderer.flipX;
        instanceRenderer.flipY           = sourceRenderer.flipY;
        instanceRenderer.drawMode        = sourceRenderer.drawMode;
        instanceRenderer.size            = sourceRenderer.size;
        instanceRenderer.maskInteraction = sourceRenderer.maskInteraction;
        instanceRenderer.sortingLayerID  = sourceRenderer.sortingLayerID;
        instanceRenderer.sortingOrder    = sourceRenderer.sortingOrder - 1;
    }

    private void ApplyOutlineScale()
    {
        if (!instanceRenderer || !sourceRenderer) return;
        Sprite spr = sourceRenderer.sprite;
        if (spr == null) return;

        // Texture size in pixels
        float texW = spr.texture.width;
        float texH = spr.texture.height;

        // Match shader formula: sprite is shrunk by (thickness*2) px on each axis
        // so we expand the outline object by the inverse ratio
        float scaleX = (texW + outlineThickness * 2f) / texW;
        float scaleY = (texH + outlineThickness * 2f) / texH;

        instanceRenderer.transform.localScale = new Vector3(scaleX, scaleY, 1f);
        // h.Out(instanceRenderer.gameObject.name);
        // h.Out(sourceRenderer.gameObject.name);
    }

    private void ApplyFillMode()
    {
        if (!instanceRenderer) return;
        if (fill)
        {
            var c = instanceRenderer.color;
            c.a = 0f;
            instanceRenderer.color = c;
        }
        else
        {
            var c = instanceRenderer.color;
            c.a = 1f / 255f;
            instanceRenderer.color = c;
        }
    }

    public void SetColor(Color color)
    {
        if (!instanceRenderer) return;
        instanceRenderer.material.SetColor("_SolidOutline", color);
        ApplyFillMode();
    }
    
    public void SetEnabled(bool value)
    {
        if (!instanceRenderer) return;
        enabled = value;
        instanceRenderer.material.SetFloat("_OutlineEnabled", value ? 1f : 0f);
    }
    
    public void SetThickness(float value)
    {
        if (!instanceRenderer) return;
        // h.Out("thickness", value);
        outlineThickness = value;
        outlineThickness = Mathf.Clamp(outlineThickness, 0f, 100f);
        ApplyOutlineScale();
        instanceRenderer.material.SetFloat("_Thickness", outlineThickness);
    }
    
    public void ChangeThickness(float value)
    {
        SetThickness(outlineThickness + value);
    }
}
// Tracks a SpriteRenderer and invokes a callback only when something changes
public class TrackedSpriteRenderer
{
    private readonly SpriteRenderer  _r;
    private readonly System.Action   _onChange;

    private Sprite      _sprite;
    private bool        _flipX, _flipY;
    private Vector2     _size;
    private int         _sortingLayer, _sortingOrder;
    private SpriteMaskInteraction _mask;
    private SpriteDrawMode        _drawMode;

    public TrackedSpriteRenderer(SpriteRenderer r, System.Action onChange)
    {
        _r        = r;
        _onChange = onChange;
        Snapshot();
    }

    public void Tick()
    {
        if (_r.sprite          != _sprite       ||
            _r.flipX           != _flipX        ||
            _r.flipY           != _flipY        ||
            _r.size            != _size         ||
            _r.drawMode        != _drawMode     ||
            _r.maskInteraction != _mask         ||
            _r.sortingLayerID  != _sortingLayer ||
            _r.sortingOrder    != _sortingOrder)
        {
            Snapshot();
            _onChange?.Invoke();
        }
    }

    private void Snapshot()
    {
        _sprite       = _r.sprite;
        _flipX        = _r.flipX;
        _flipY        = _r.flipY;
        _size         = _r.size;
        _drawMode     = _r.drawMode;
        _mask         = _r.maskInteraction;
        _sortingLayer = _r.sortingLayerID;
        _sortingOrder = _r.sortingOrder;
    }
}