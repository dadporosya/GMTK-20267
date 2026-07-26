using UnityEngine;

public class EndingManager : MonoBehaviour
{
    public static EndingManager Instance;
    public CutSceneBase endingCutscene;
    public DialogueContainer endingDialogue;
    public DialogueContainer dontHaveMuchTimeDialogue;

    private void Start()
    {
        h.CreateStaticInstance(this, ref Instance);
    }
    
    public void StartEndingDialogue()
    {
        DialogueManager.Instance.StartDialogue(endingDialogue);
    }

    public void StartDontHaveMuchTimeDialogue()
    {
        DialogueManager.Instance.StartDialogue(dontHaveMuchTimeDialogue);
    }

    public void StartEndingCutscene()
    {
        CutSceneManager.Instance.RunCutscene(endingCutscene);
        
    }
}