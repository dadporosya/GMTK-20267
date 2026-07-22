using UnityEngine;
using UnityEngine.Events;

public class BaseInteractable : MonoBehaviour, IInteractable
{
    [SerializeField] UnityEvent onInteractEvent;
    [SerializeField] bool isInteractable;
    [SerializeField] bool multipleInteractions;



    public void Interact()
    {
        if (isInteractable)
        {
            if(!multipleInteractions)
            {
                SetInteractive(false);
            }
            onInteractEvent.Invoke();
        }

    }

    public void SetInteractive(bool interactable)
    {
        isInteractable = interactable;
    }

    public bool IsInteractable()
    {
        return isInteractable;
    }

}
