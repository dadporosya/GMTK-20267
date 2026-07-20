using System;
using PrimeTween;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;

public class DragableObject :
    MonoBehaviour,
    IBeginDragHandler,
    IDragHandler,
    IEndDragHandler,
    IPointerEnterHandler,
    IPointerExitHandler,
    IDropHandler,
    IPointerClickHandler,
    IPointerDownHandler
{
    public enum State
    {
        Dragging,
        Static
    }

    [HideInInspector]
    public State state = State.Static;

    [Header("Hover")]
    [SerializeField] private float hoverScaleMultiplier = 1.15f;
    [SerializeField] private float hoverDuration = 0.12f;

    public RectTransform RectTransform { get; private set; }

    [SerializeField] private CanvasGroup canvasGroup;

    public Vector3 originalScale;
    
    public UnityEvent OnStartDragging = new UnityEvent();
    public UnityEvent OnRelease = new UnityEvent();

    public SlotBase currentSlot;
    [HideInInspector] public SlotBase previousSlot;

    
    private void Awake()
    {
        RectTransform = GetComponent<RectTransform>();

        canvasGroup = GetComponent<CanvasGroup>();

        if (!canvasGroup)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        
        // h.Out(transform.localScale);
        originalScale = transform.localScale;
    }

    public virtual void Start()
    {
        
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (!currentSlot)
            return;

        DragableObject dragged =
            eventData.pointerDrag.GetComponent<DragableObject>();

        if (!dragged || dragged == this)
            return;

        currentSlot.PlacingObject(dragged);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        CursorDnDManager.Instance.StartDragging(this);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        ;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        CursorDnDManager.Instance.SetSuccessfulClick();
    }

    public void OnDrag(PointerEventData eventData)
    {
    }

    public void OnEndDrag(PointerEventData eventData)
    {
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (state == State.Dragging)
            return;

        CursorDnDManager.Instance.SetHover(this);

        OnHoverEnter();
    }

    public virtual void OnHoverEnter()
    {
        
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (state == State.Dragging)
            return;

        CursorDnDManager.Instance.ClearHover();

        OnHoverExit();
    }
    
    public virtual void OnHoverExit()
    {
        
    }

    
    

    public bool StartDragging()
    {
        if (currentSlot)
        {
            if (!currentSlot.RemovingCondition())
            {
                h.Out("cannot be removed");
                return false;
            }
            
            previousSlot = currentSlot;

            currentSlot.RemoveObject();

            currentSlot = null;
        }
        
        state = State.Dragging;

        canvasGroup.blocksRaycasts = false;

        transform.SetAsLastSibling();

        OnStartDragging?.Invoke();

        return true;
    }

    public void Release()
    {
        state = State.Static;

        canvasGroup.blocksRaycasts = true;

        if (!currentSlot && previousSlot)
        {
            previousSlot.PlaceObject(this);
            currentSlot = previousSlot;
        }

        OnRelease?.Invoke();
        
        
    }

    public void OnHoverStart()
    {
        
    }

    public void OnHoverEnd()
    {
        
    }
}