using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;

/// <summary>
/// Cutscene that runs ones you have captured the very first castle
/// Possible script:
/// 1. run dialogue only w liza
/// 2. shake camera
/// 3. dialogue w liza & petya
/// 4. in the middle of dialogue ig if it is ok schange in th e dialogue it self
/// 5. than start event w petya capturing some stuff
/// 6. end gliutches
/// </summary>
public class TempCutscene : CutSceneBase
{

    public override void Init()
    {
        base.Init();
        
        List<IEnumerator> rawSteps = new List<IEnumerator>()
        {
            // IEnumerator 1...

        };

        foreach (IEnumerator step in rawSteps)
        {
            cutsceneSteps.Add(step);
        }
    }


}