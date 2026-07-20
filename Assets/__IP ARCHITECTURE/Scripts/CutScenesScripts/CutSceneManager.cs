using Unity.VisualScripting;
using UnityEngine;

public class CutSceneManager : MonoBehaviour
{
    public static CutSceneManager Instance;
    
    public CutSceneBase currentCutScene;
    public string currentCutScenePath;

    public string defaultLabel = "Cutscenes/";

    [SerializeField] private bool runOnStart=false;

    private void Start()
    {
        h.CreateStaticInstance(this, ref Instance);
        if (runOnStart) RunCutscene();
    }
    
    public void RunCutscene()
    {
        if (currentCutScene)
        {
            RunCutscene(currentCutScene);
            return;
        }

        if (currentCutScenePath != "")
        {
            RunCutscene(currentCutScenePath);
            return;
        }
        
        h.Out("Current cutscene is not assigned");
    }

    public void RunCutscene(CutSceneBase scene)
    {
        // run pfb
        scene.Run();
        // Destroy(scene.gameObject);
    }

    public void RunCutscene(string path)
    {
        string fullPath = defaultLabel + path;
        var cutScene = Resources.Load<CutSceneBase>(fullPath);
        if (!cutScene)
        {
            h.Out("cutscene " + fullPath + " is not found");
            return;
        }
        cutScene.Run();
        
    }

    public CutSceneBase LoadCutscene(string path)
    {
        return Resources.Load<CutSceneBase>(defaultLabel + path);
    }
}
