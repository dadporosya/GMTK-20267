using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class TouchEventTrigger : MonoBehaviour
{
    [SerializeField] bool multipleInteractions;
    [SerializeField] UnityEvent eventToTrigger;
    [SerializeField] LayerMask triggerLayerMask;


    private void OnTriggerEnter(Collider collider)
    {
        if ((triggerLayerMask & (1 << collider.gameObject.layer)) != 0)
        {
            eventToTrigger.Invoke();
            if (!multipleInteractions)
            {
                gameObject.SetActive(false);
            }
        }
    }
}
