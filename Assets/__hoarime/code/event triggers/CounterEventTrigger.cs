using TMPro;
using UnityEngine;
using UnityEngine.Events;

public class CounterEventTrigger : MonoBehaviour
{
    [SerializeField] int target;
    [SerializeField] UnityEvent onTargetReach;
    [SerializeField] TMP_Text countProgressDisplay;

    private int current;

    private void Start()
    {
        current = 0;
    }

    public void Count()
    {
        if(current >= target - 1)
        {
            onTargetReach.Invoke();
            current = 0;
            countProgressDisplay.gameObject.SetActive(false);
        }
        else
        {
            current++;
            countProgressDisplay.text = current + "/" + target;
        }
    }

    public void SetTarget(int newTarget)
    {
        current = 0;
        target = newTarget;
    }
}
