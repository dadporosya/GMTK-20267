using NUnit.Framework;
using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class TimedEvents : MonoBehaviour
{
    [SerializeField] float[] timeMarks;
    [SerializeField] UnityEvent[] events;

    private Monologue monologue;

    private float currentTime;
    private int currentTimeMark;
    private int timeMarksCount;

    private bool timePaused = false;

    private WaitUntil waitUntilCashed;



    private void Start()
    {
        currentTime = 0;
        currentTimeMark = 0;
        timeMarksCount = timeMarks.Length;
        StartCoroutine(Timelapse());
    }

    private void Awake()
    {

        monologue = FindFirstObjectByType<Monologue>();
        waitUntilCashed = new WaitUntil(() => !monologue.isMonologuing && !timePaused);
    }

    private IEnumerator Timelapse()
    {
        while (currentTimeMark < timeMarksCount)
        {
            if (currentTime >= timeMarks[currentTimeMark])
            {
                events[currentTimeMark].Invoke();
                currentTimeMark++;
                yield return waitUntilCashed;
            }
            currentTime += Time.deltaTime;
            yield return null;
        }
    }

    public void PauseTime(bool pause)
    {
        timePaused = pause;
    }
}
