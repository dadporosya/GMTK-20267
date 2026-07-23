using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ScriptableObjectContainer", menuName = "Containers/ScriptableObjectContainer")]
public class ScriptableObjectContainer : ScriptableObject
{
    public List<ScriptableObject> scriptableObjects = new List<ScriptableObject>();
}
