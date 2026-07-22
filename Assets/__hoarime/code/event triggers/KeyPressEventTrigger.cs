using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Events;

public class KeyPressEventTrigger : MonoBehaviour
{
    [SerializeField] KeyCode key;
    [SerializeField] bool multipleTriggers;
    [SerializeField] UnityEvent eventToTrigger;

    private void Update()
    {
        if(Input.GetKeyUp(key))
        {
            eventToTrigger.Invoke();
            if(!multipleTriggers)
            {
                gameObject.SetActive(false);
            }
        }
    }

}
