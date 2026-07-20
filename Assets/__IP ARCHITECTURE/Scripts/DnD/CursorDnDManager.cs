using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using PrimeTween;

public class CursorDnDManager : MonoBehaviour
{
    public static CursorDnDManager Instance;

    [Header("Drag")]
    public DragableObject currentDraggingObject;

    private DragableObject currentReleasedObject;
    
    [Header("Hover")]
    public DragableObject currentHoverObject;

    [SerializeField] private float followSpeed = 20f;

    [SerializeField] private Canvas canvas;
    private RectTransform canvasRect;

    private Vector2 dragOffset;
    private Vector2 targetPos;
    
    [SerializeField] private float onClickScaleMultiplier = 1.1f;
    [SerializeField] private float onHoverScaleMult = 1.2f;

    [Header("Click Detection")]
    public static bool SuccessfulClick { get; private set; }

    private Coroutine emptyClickCoroutine;

    private void Awake()
    {
        h.CreateStaticInstance(this, ref Instance);

        if (!canvas) canvas = GameObject.FindGameObjectWithTag("MainCanvas").GetComponent<Canvas>();
        if (!canvas) canvas = FindFirstObjectByType<Canvas>();

        if (canvas)
            canvasRect = canvas.GetComponent<RectTransform>();

        SetEmptyClick();
    }

    private void Update()
    {
        HandleRelease();
        HandleDragging();
        HandleHovering();
    }

 
    

    private void HandleHovering()
    {
        if (currentDraggingObject) return;
    }
    
    public void SetHover(DragableObject obj)
    {
        if (currentHoverObject == obj || currentDraggingObject)
            return;
        
        ClearHover();
        
        currentHoverObject = obj;
        
        Tween.Scale(
            currentHoverObject.transform,
            endValue: currentHoverObject.originalScale * onHoverScaleMult,
            duration: 0.3f);
        currentHoverObject.OnHoverStart();
    }

    public void ClearHover()
    {
        if (!currentHoverObject || currentDraggingObject)
            return;
        
        Tween.Scale(
            currentHoverObject.transform,
            endValue: currentHoverObject.originalScale,
            duration: 0.3f);
        
        currentHoverObject.OnHoverEnd();

        currentHoverObject = null;
    }
    
    private void HandleDragging()
    {
        if (!currentDraggingObject)
            return;

        RectTransform rect =
            currentDraggingObject.RectTransform;

        targetPos = default;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            Input.mousePosition,
            canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : canvas.worldCamera,
            out targetPos
        );

        targetPos += dragOffset;

        Tween.UIAnchoredPosition(
            rect,
            endValue: targetPos,
            duration: 1f / followSpeed,
            ease: Ease.OutQuad
        );
    }


    private void HandleRelease()
    {
        if (!currentDraggingObject)
            return;
        
        if (!Input.GetMouseButtonUp(0)) return;
        
        void OnRelease()
        {
            currentReleasedObject.Release();
        }

        currentReleasedObject = currentDraggingObject;
        currentDraggingObject = null;
        
        OnRelease();
    }

    public void StartDragging(DragableObject dragObject)
    {
        SetSuccessfulClick();
        
        if (!dragObject.StartDragging()) return;
        
        currentDraggingObject = dragObject;

        Tween.StopAll(currentDraggingObject.transform);

        Sequence.Create(cycles: 1).
            Group(
            Tween.Scale(
                target: currentDraggingObject.transform,
                endValue: currentDraggingObject.transform.localScale * onClickScaleMultiplier,
                duration: 0.07f,
                ease: Ease.OutBounce
            ))
            .Chain(
            Tween.Scale(
                target: currentDraggingObject.transform,
                endValue: currentDraggingObject.originalScale * onHoverScaleMult,
                duration: 0.1f,
                ease: Ease.OutQuad
            )
        );

        RectTransform rect = dragObject.RectTransform;

        Vector2 mouseLocalPos;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            Input.mousePosition,
            canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : canvas.worldCamera,
            out mouseLocalPos
        );

        dragOffset = rect.anchoredPosition - mouseLocalPos;
    }

    /// <summary>
    /// Call from SlotBase's IPointerClickHandler or IPointerDownHandler.
    /// </summary>
    public void RegisterSlotClick(SlotBase slot)
    {
        SetSuccessfulClick();
    }

    
    public void SetSuccessfulClick()
    {
        SuccessfulClick = true;
        if (emptyClickCoroutine != null) StopCoroutine(emptyClickCoroutine);
        emptyClickCoroutine = StartCoroutine(SetEmptyClickCoroutine());
    }
    
    public void SetEmptyClick()
    {
        SuccessfulClick = false;
    }
    
    private IEnumerator SetEmptyClickCoroutine(float duration = 0.001f)
    {
        float timeElapsed = 0;
        while (timeElapsed < duration)
        {
            timeElapsed += Time.deltaTime;
            yield return null;
        }
        SetEmptyClick();
    }
}