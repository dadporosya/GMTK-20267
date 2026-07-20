using System;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Events;

[CreateAssetMenu(fileName = "DialogueSpeaker", menuName = "Dialogues/DialogueSpeaker")]
public class DialogueSpeaker : ScriptableObject
{
    public string speakerName;
    public Sprite portrait;
    public List<AudioClip> voiceClips = SFXManager.Instance.defaultDialogueVoiceList;
}

[Serializable]
public class DialogueNode
{
    [HideInInspector] public int nodeId = -1;
    [Tooltip("leave blank if you do not wanna to reinit it")]
    public string speakerId;
    [HideInInspector] public bool initialized = false;
    public bool speakerIsThis=true; //
    public bool overWriteValues = false;
    [TextArea(3, 7)]
    public string text;
    
    [SerializeField] bool initFromScriptableObject=true;
    public DialogueSpeaker speaker;
    public GameObject speakerGameObject;
    public string speakerName;
    public Sprite speakerPortrait;
    public bool useSpriteAtlas = true;
    public Sprite[] spriteAtlas;
    public int spriteId;
    public List<AudioClip> _speakerVoiceClips = new List<AudioClip>();

    public Material material;
    
    public UnityEvent onNodeStart;
    public UnityEvent onNodeEnd;
    
    
    
    public List<AudioClip> speakerVoiceClips
    {
        get { return _speakerVoiceClips; }
        set
        {
            _speakerVoiceClips = value;
            if (_speakerVoiceClips == null || _speakerVoiceClips.Count == 0)
            {
                // h.Out(_speakerVoiceClips);
                if (SFXManager.Instance)
                    _speakerVoiceClips = SFXManager.Instance.defaultDialogueVoiceList;
            }
        }
    }

    public void Init()
    {
        initialized = false;
        if (speakerIsThis) return;
        
        if (initFromScriptableObject && speaker != null)
        {
            InitFromScriptableObject(speaker);
            return;
        }

        if (speakerGameObject)
        {
            InitFromGameObject(speakerGameObject);
            return;
        }

        if(h.CheckIfAllExist(speakerName, speakerPortrait))
        {
            InitFromDirectValues(speakerName, speakerPortrait, speakerVoiceClips);
            return;
        }
        
        speakerIsThis = true;
    }

    public void InitFromScriptableObject(DialogueSpeaker data)
    {
        initialized = true;
        if (overWriteValues || speaker == null) speaker = data;
        if (overWriteValues || string.IsNullOrEmpty(speakerName)) speakerName = speaker.speakerName;
        if (overWriteValues || speakerPortrait == null) speakerPortrait = speaker.portrait;
        if (overWriteValues || _speakerVoiceClips.Count == 0) speakerVoiceClips = speaker.voiceClips;
    }
    public void InitFromGameObject(GameObject go)
    {
        // h.Out("init from go");
        speakerGameObject = go;
        initialized = true;
        if (go.TryGetComponent<Talkable>(out var talkable))
        {
            if (overWriteValues || speakerName == "") speakerName = talkable.Name;
            
            if (overWriteValues && talkable.portraitAtlas != null && talkable.portraitAtlas.Length > 0)
            {
                speakerPortrait = talkable.portraitAtlas[spriteId];
            } else if (!overWriteValues && spriteAtlas != null && spriteAtlas.Length > 0 && spriteId < spriteAtlas.Length)
            {
                speakerPortrait = spriteAtlas[spriteId];
            } else if (overWriteValues || speakerPortrait == null) speakerPortrait = talkable.Portrait;
            
            if (overWriteValues || _speakerVoiceClips.Count == 0) speakerVoiceClips = talkable.voiceClips;
            
            if (talkable.material) material = talkable.material;
        }
        else
        {
            if (overWriteValues || string.IsNullOrEmpty(speakerName)) speakerName = go.name;
            if (!overWriteValues && spriteAtlas != null && spriteAtlas.Length > 0 && spriteId < spriteAtlas.Length)
            {
                speakerPortrait = spriteAtlas[spriteId];
            } else if (overWriteValues || speakerPortrait == null) speakerPortrait = go.GetComponent<SpriteRenderer>().sprite;
        }

        InitSpeaker();
    }

    public void InitFromDirectValues(
        string nameIn,
        Sprite spriteIn,
        List<AudioClip> voiceIn
        )
    {
        if (overWriteValues || string.IsNullOrEmpty(speakerName)) speakerName = nameIn;
        if (overWriteValues || speakerPortrait == null) speakerPortrait = spriteIn;
        if (overWriteValues || _speakerVoiceClips.Count == 0) speakerVoiceClips = voiceIn;
        InitSpeaker();
    }

    private void InitSpeaker()
    {
        speaker = ScriptableObject.CreateInstance<DialogueSpeaker>();
        speaker.speakerName = speakerName;
        speaker.portrait = speakerPortrait;
    }
    
    void OnDisable() // ???
    {
        initialized = false;
        if (!initFromScriptableObject && !speakerIsThis) speaker = null;
    }
}


[CreateAssetMenu(fileName = "DialogueContainer", menuName = "Dialogues/DialogueContainer")]
public class DialogueContainer : ScriptableObject
{
    public List<string> speakerIds = new List<string>();
    public List<GameObject> speakerPrefabs = new List<GameObject>();
    public Dictionary<string, GameObject> speakers = new Dictionary<string, GameObject>();
    public List<DialogueNode> nodes = new List<DialogueNode>();

    // virtual so overrides (e.g. DialogueContainerWithModels) can extend init/teardown.
    protected virtual void OnEnable()
    {
        speakers.Clear();

        AssignNodeIds();

        int idCount = speakerIds.Count;
        int pfbCount = speakerPrefabs.Count;
        for (int i = 0; i < idCount; i++)
        {
            string id = speakerIds[i];
            if (string.IsNullOrEmpty(id)) continue;
            GameObject pfb = pfbCount > i ? speakerPrefabs[i] : null;
            speakers[id] = pfb;
        }
    }

    protected virtual void OnDisable()
    {
        speakers.Clear();
    }

    /// <summary>
    /// Assigns each node its index in the nodes list as nodeId.
    /// </summary>
    public void AssignNodeIds()
    {
        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodes[i] != null) nodes[i].nodeId = i;
        }
    }
}


[Serializable]
public class SerializableDialogueContainer
{
    public List<DialogueNode> values = new List<DialogueNode>();

    public DialogueContainer Convert()
    {
        DialogueContainer d = ScriptableObject.CreateInstance<DialogueContainer>();
        d.nodes = values;
        return d;
    }
}
