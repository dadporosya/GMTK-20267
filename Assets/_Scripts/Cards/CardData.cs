using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "CardData", menuName = "Cards/CardData")]
public class CardData : ScriptableObject
{
    public string title;
    public List<CP.Suits> suits = new List<CP.Suits>();
    private int countdown = 0; // if 0 -> player at once
    
    [Header("Effect")]
    /// Target suits -> amount of vp
    public CP.Condition condition = CP.Condition.SuitSet;
    public int vpPerSet = 0;
    public CP.ActivateCond activation = CP.ActivateCond.Burn;
    public CP.TargetSource targetSource = CP.TargetSource.Table; // where card will take the values
    
    [Header("Suits set condition")]
    public List<CP.Suits> suitSet = new List<CP.Suits>();

    [Header("Suits count condition")]
    public bool fixedCount = true; // if not, would be min count
    public int suitCount = 0;

    public int GenerateVP()
    {
        int vp = 0;
        if (condition == CP.Condition.SuitSet)
        {
            Dictionary<CP.Suits, int> sourceSuits = new Dictionary<CP.Suits, int>();
            if (targetSource == CP.TargetSource.Table)
            {
                // TASK: add all suits from TableManager.INstance.suits to sourceSuits
            } else if (targetSource == CP.TargetSource.Hand)
            {
                // skip for now
            }
            
            /// TASK:
            /// you have to calculate amount of vp player will resieve
            /// it should give vpPerSet for each set of suitSet suits
            /// if suitSet = {A, B, B}, and player have 2A and 5B, player will recieve
            /// 2 * vpCount, as u can create only 2 sets of A B B from 2A and 5B
            /// literally, divide all suit count by it count in suitSet, round down,
            /// then, take min value, and multiply it by vpPerSet
            /// if suitSet = {A}, and player have 5A, it will be 5 * vpPErset
            /// if suit set = {A, B}, and player has 5A, 0B, 67C, player will receive 0 vp, as there are no requiered sets
        }
        
        h.Out(vp);
        return vp;
    }
}
