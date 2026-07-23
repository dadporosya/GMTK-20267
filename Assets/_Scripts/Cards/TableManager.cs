using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class TableManager : MonoBehaviour
{
    public static TableManager Instance;
    
    public int targetScore=100;
    public int currentScore=0;

    [SerializeField] private TMP_Text scoreText;
    
    public Dictionary<CP.Suits, int> suitCounts = new Dictionary<CP.Suits, int>();

    private void Start()
    {
        h.CreateStaticInstance(this, ref Instance);
    }
    
    /// TASK:
    /// create func for changing current score and depicting it in score text
    /// make smooth number changing animation, like
    /// if score is 0, and you add 10, it text changes like: 1 -> 2 -> 3... -> 10
    /// and the higher is changing delta, the faster is anim. so create max and min param for speed
    /// 
}
