using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "GameObjectContainer", menuName = "Containers/GameObjectContainer")]
public class GameObjectContainer : ScriptableObject
{
    public List<GameObject> gameObjects = new List<GameObject>();
}
