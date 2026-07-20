using System;
using FischlWorks_FogWar;

using UnityEngine;

public class FogOfWarRevealer : MonoBehaviour
{
    public int sightRange = 100;
    private csFogWar.FogRevealer revealer;

    private void Start()
    {
        if (FogOfWarManager.Instance != null)
            revealer = FogOfWarManager.Instance.AddRevealer(transform);
    }

    public void SetSightRange(int value)
    {
        sightRange = value;
        if (revealer == null) return;
        revealer.sightRange = sightRange;
    }

    private void OnDestroy()
    {
        if (FogOfWarManager.Instance != null && revealer != null)
            FogOfWarManager.Instance.RemoveRevealer(revealer);
    }
}