using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Events;
using UnityEngine.UI;

public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance;
    
    public GameObject dialogueWindow;
    public Image portraitImage;
    public TextMeshProUGUI portraitTitle;
    public TextMeshProUGUI dialogueText;
    private TextAnimation textAnimationComp;
    [HideInInspector] public ScalingText portraitScalingTitle;
    [HideInInspector] public ScalingText dialogueScalingText;
    
    private bool dialogueActive=false;
    private DialogueContainer dialogue;
    private Queue<DialogueNode> currentParagraphs = new Queue<DialogueNode>();

    [SerializeField] private float defaultTypeSpeed = 10f;
    [HideInInspector] public float typeSpeed;
    
    private enum TypingState { notStarted, active, finished }
    private TypingState typingState = TypingState.notStarted;
    
    private Coroutine typeCoroutine;

    // Set while a node is waiting for its enter transitions (objects appearing) to finish.
    // Input is ignored during that window so a keypress can't skip or double-start the node.
    private bool nodeStarting;
    private Coroutine startParagraphCoroutine;

    private DialogueNode paragraph;
    private Talkable currentNodeTalkable;
    private Talkable initiatingTalkable;
    
    [SerializeField] private const float MAX_TYPE_SPEED = 0.1f;

    [SerializeField] private GameObject skipIcon;
    
    [SerializeField] private Material defaultMaterial;
    
    public UnityEvent onDialogueEnd;
    public UnityEvent onDialogueStart;
    public UnityEvent onStartNode;
    public UnityEvent onEndNode;

    private string pathLabel = "Dialogues";
    private string alliesLabel = "/Allies";
    private string placeholderLabel = "/PlaceHolder";
    [SerializeField] private bool disableWindowOnStart = true;

    [SerializeField] private DialogueWindowAnimations windowAnimations;

    private void Awake()
    {
        h.CreateStaticInstance(this, ref Instance);
    }
    private void Start()
    {
        if (!windowAnimations) windowAnimations = GetComponent<DialogueWindowAnimations>();
        if (!windowAnimations) windowAnimations = FindObjectOfType<DialogueWindowAnimations>();

        // Snap the window shut on boot; no open/close animation here.
        if (disableWindowOnStart) EndDialogue(animate: false);
        if (portraitTitle) portraitScalingTitle = portraitTitle.GetComponent<ScalingText>();
        dialogueScalingText = dialogueText.GetComponent<ScalingText>();
        textAnimationComp = dialogueText.GetComponent<TextAnimation>();
        
        typeSpeed = defaultTypeSpeed;
        
        onStartNode.AddListener(DisableSkipIcon);
        onEndNode.AddListener(EnableSkipIcon);
    }

    private void EnableSkipIcon()
    {
        StartCoroutine(EnableSkipIconCoroutine());
    }

    private IEnumerator EnableSkipIconCoroutine()
    {
        yield return new WaitForSeconds(0.5f);
        if (skipIcon) skipIcon.SetActive(true);
    }
    
    private void DisableSkipIcon()
    {
        if (skipIcon) skipIcon.SetActive(false);
    }

    private void Update()
    {
        // Ignore input while the node's objects are still appearing.
        if (nodeStarting)
            return;

        if (typingState == TypingState.notStarted)
            return;

        if (Input.GetKeyDown(KeyCode.Space)
            || Input.GetKeyDown(KeyCode.Return))
        {
            DisplayNextParagraph();
        } else if (Input.GetKeyDown(KeyCode.L))
        {
            EndDialogue();
        }
    }

    public void SetText(
        ScalingText scalingHolder,
        TextMeshProUGUI originalHolder, 
        string text)
    {
        if (scalingHolder)
        {
            scalingHolder.SetText(text);
            return;
        }

        originalHolder.text = text;
    }

    public void StartDialogue(string path)
    {
        string fullPath = pathLabel + path;
        var dCont = Resources.Load<DialogueContainer>(fullPath);
        if (!dCont)
        {
            h.Out("dialogue " + fullPath + " not found");
            return;
        }
        StartDialogue(Instantiate(dCont));
    }

    public void StartDialogue(string path, Talkable talkable)
    {
        string fullPath = pathLabel + path;
        var dCont = Resources.Load<DialogueContainer>(fullPath);
        if (!dCont)
        {
            h.Out("dialogue " + fullPath + " not found");
            return;
        }

        if (!talkable)
        {
            h.Out("no talkable");
            return;
        }
        
        DialogueContainer dInstance = Instantiate(dCont);
        talkable.InitDialogue(dInstance);

        StartDialogue(dInstance, talkable);
    }

    public void StartAllyDialogue(string path)
    {
        string fullPath = alliesLabel + path;
        if (!h.ResourceExists(pathLabel + fullPath))
        {
            h.Out(pathLabel + fullPath + " not found");
            fullPath = alliesLabel + placeholderLabel;
        }
        StartDialogue(fullPath);
    }

    public void StartAllyDialogue(string path, Talkable talkable)
    {
        string fullPath = alliesLabel + path;
        if (!h.ResourceExists(pathLabel + fullPath))
        {
            h.Out(pathLabel + fullPath + " not found.");
            fullPath = alliesLabel + placeholderLabel;
        }
        StartDialogue(fullPath, talkable);
    }
    
    // MAIN OVERRIDE IDK
    public void StartDialogue(DialogueContainer dialogueIn, Talkable source = null)
    {
        initiatingTalkable = source;
        dialogueActive = true;
        dialogueWindow.SetActive(true);
        dialogue = dialogueIn;
        currentParagraphs = new Queue<DialogueNode>(dialogueIn.nodes);

        typingState = TypingState.notStarted;

        SetNodeText("");

        onDialogueStart?.Invoke();

        // Play the open animation first; don't show the first paragraph until it finishes.
        StartCoroutine(StartDialogueRoutine());
    }

    private IEnumerator StartDialogueRoutine()
    {
        if (windowAnimations) yield return windowAnimations.OnDialogueStart();
        // The open animation is done; the dialogue actually starts now.
        initiatingTalkable?.OnDialogueStart();
        // Fire the on-dialogue-start lifecycle on every object of a multi-object dialogue.
        (dialogue as DialogueContainerWithModels)?.OnDialogueStart();
        DisplayNextParagraph();
    }

    public void DisplayNextParagraph()
    {
        // The current node is still bringing its objects on screen - don't advance past it.
        if (nodeStarting) return;

        if (typingState == TypingState.active)
        {
            StopTyping();
            return;
        }
        if (typingState == TypingState.finished)
        {
            typingState = TypingState.notStarted;
            
            DisplayNextParagraph();
            return;
        }
        
        if (currentParagraphs.Count <= 0 || !dialogueActive)
        {
            EndDialogue();
            return;
        }
        
        if (typingState == TypingState.notStarted)
        {
            StartParagraph();
        }
    }

    private void SetNodeText(string text)
    {
        SetText(dialogueScalingText, dialogueText, text);
    }
    
    private IEnumerator TypingDialogue(
        string textIn,
        ScalingText scalingTextBox,
        TextMeshProUGUI defaultTextBox,
        List<AudioClip> audioClips=null
        )
    {
        typingState = TypingState.active;

        // Slice textIn by color tags
        Queue<string> textSlicesByColor = new Queue<string>();
        string colorTagPattern = @"(<color[^>]*>)";
        System.Text.RegularExpressions.Regex colorRegex = new System.Text.RegularExpressions.Regex(colorTagPattern);

        string[] parts = colorRegex.Split(HTML.RemoveUniqueTags(textIn));

// Recombine each tag with the text that follows it
        for (int i = 0; i < parts.Length; i++)
        {
            if (colorRegex.IsMatch(parts[i]) && i + 1 < parts.Length)
            {
                textSlicesByColor.Enqueue(parts[i] + parts[i + 1]);
                i++; // skip the next part, already consumed
            }
            else if (!string.IsNullOrEmpty(parts[i]))
            {
                textSlicesByColor.Enqueue(parts[i]);
            }
        }

        //h.Out(textSlicesByColor, textSlicesByColor.Count);

        string filteredText = "";
        
        void AddSplit()
        {
            if (textSlicesByColor.Count == 0) return;
            filteredText += textSlicesByColor.Dequeue();
        }

        
        // n
        AddSplit();
        
        string displayedText = "";
        int alphaIndex = 0;

        if (textAnimationComp)
        {
                    
            textAnimationComp.SetAwake();
            textAnimationComp.ForceUpdate(filteredText); // generate dict keys for animations
            textAnimationComp.SetSleep();
        }
        
        filteredText = HTML.RemoveTags(filteredText, targetTag:HTML.ANIMATION_TAG);
        textIn = HTML.RemoveTags(textIn, targetTag: HTML.ANIMATION_TAG);
        
        SetText(scalingTextBox, defaultTextBox, filteredText);
        // _textAnimationComponent.cleaned = true;
        int characterCount = textIn.Length;
        // foreach (char c in textIn)

        bool playVoice = audioClips != null && audioClips.Count > 0;

        InvisibleText.HideAllCharacters(defaultTextBox);

        // HideAllCharacters only records the alpha=0 overrides; they are pushed to the
        // mesh inside TextAnimation's next Update. Force that apply now so the fully
        // visible text is never rendered for a frame before typing begins (the "flash").
        if (textAnimationComp) textAnimationComp.ForceUpdate();

        for (int i = 0; i < characterCount; i++)
        {
            char c =  textIn[i];
            
            // Process unique tags
            if (c == '<')
            {
                //todo: mb fix, so it detects only tags, not string like: hello! '< priiiiii, hhasdfhs >'
                int endIndex = textIn.IndexOf('>', i);
                if (endIndex != -1)
                {
                    string tag = textIn.Substring(i, endIndex - i + 1);
                    //h.Out(tag);
                    // Check if tag is in HTML.uniqueTags
                    bool isUniqueTag = false;
                    foreach (string uniqueTag in HTML.ALL_UNIQUE_TAGS)
                    {
                        if (tag.StartsWith("<" + uniqueTag))
                        {
                            isUniqueTag = true;
                            break;
                        }
                    }
                    
                    // If not a unique tag, skip it
                    if (!isUniqueTag)
                    {
                        alphaIndex += tag.Length;
                        i = endIndex;
                        continue;
                    }
                    
                    // process unique tags (e.g., pause)
                    // Process pause
                    if (tag.Contains(HTML.DIALOGUE_PAUSE_TAG))
                    {
                        int l = HTML.DIALOGUE_PAUSE_TAG.Length + 2; // pause + =

                        string pauseStr = textIn.Substring(i + l, endIndex - i - (l));
                        if (float.TryParse(pauseStr, out float pauseTime))
                        {
                            yield return new WaitForSeconds(pauseTime);
                            i = endIndex;
                            continue;
                        }
                    }
                    
                }
            }
            
            InvisibleText.ShowCharacterById(defaultTextBox, alphaIndex);
            alphaIndex++;
            // SetText(dialogueScalingText, dialogueText, originalText);
            // displayedText = dialogueText.text.originalText.Insert(alphaIndex, HTML_ALPHA);
            
            // // change char alpha instead
            // displayedText = filteredText.Insert(alphaIndex, HTML.ALPHA);
            // SetText(scalingTextBox, defaultTextBox, displayedText);
            
            // Sound FX of typing
            if (playVoice && !SFXManager.Instance.defaultAudioSource.isPlaying)
            {
                AudioClip clip = h.RandChoice(audioClips);
                SFXManager.Instance.PlayClip(clip, volumeIn:1);
            }

            if (alphaIndex == filteredText.Length)
            {
                AddSplit();
            }

            // _textAnimationComponent.cleaned = true;    
            yield return new WaitForSeconds(MAX_TYPE_SPEED / typeSpeed);
        }
        StopTyping();
    }

    private void StartParagraph()
    {
        paragraph = currentParagraphs.Dequeue();
        if (paragraph == null)
        {
            h.Out("Paragraph == null!!!");
            DisplayNextParagraph();
        }

        if (!string.IsNullOrEmpty(paragraph.speakerId)
            && dialogue != null
            && dialogue.speakers.TryGetValue(paragraph.speakerId, out var speakerGo)
            && speakerGo)
        {
            paragraph.InitFromGameObject(speakerGo);
        }

        // If this node's speaker is a Talkable, let it react to the node lifecycle.
        currentNodeTalkable = null;
        if (paragraph.speakerGameObject
            && paragraph.speakerGameObject.TryGetComponent<Talkable>(out var nodeTalkable))
        {
            currentNodeTalkable = nodeTalkable;
        }

        // A node may first have to bring new objects on screen; the node itself only starts
        // once their appearing animations have finished.
        startParagraphCoroutine = StartCoroutine(StartParagraphRoutine());
    }

    /// <summary>
    /// Runs a node's enter/exit transitions first (multi-object dialogues: objects this node
    /// enables play their appearing animation), and only once they are done fires the node-start
    /// lifecycle and begins typing the text.
    /// </summary>
    private IEnumerator StartParagraphRoutine()
    {
        nodeStarting = true;

        // Multi-object dialogues drive every TalkableWithModels in their objects list
        // (enable/disable, model, eyes) from the node's per-object settings. This yields until
        // any newly enabled object has finished appearing, and applies a per-node speaker
        // override before the portrait is read below.
        DialogueContainerWithModels modelsDialogue = dialogue as DialogueContainerWithModels;
        if (modelsDialogue != null) yield return modelsDialogue.OnNodeStart(paragraph, this);

        nodeStarting = false;

        // The dialogue may have been ended while we were waiting for the animations.
        if (!dialogueActive) yield break;

        paragraph?.onNodeStart?.Invoke();
        onStartNode?.Invoke();
        currentNodeTalkable?.OnNodeStart(paragraph);

        if (portraitImage)
        {
            portraitImage.sprite = paragraph.speakerPortrait;
            portraitImage.material = defaultMaterial;
            if (paragraph.material) portraitImage.material = paragraph.material;
        }

        if (portraitScalingTitle || portraitTitle)
            SetText(portraitScalingTitle, portraitTitle, paragraph.speakerName);
        
        typeCoroutine = StartCoroutine(
            TypingDialogue(
                paragraph.text,
                dialogueScalingText,
                dialogueText,
                audioClips:paragraph.speakerVoiceClips
            )
        );
    }
    
    private void StopTyping()
    {
        if (typeCoroutine != null) StopCoroutine(typeCoroutine);
        if (paragraph != null) SetText(dialogueScalingText, dialogueText, 
            HTML.RemoveTags(HTML.RemoveUniqueTags(paragraph.text), targetTag: HTML.ANIMATION_TAG));
        typingState = TypingState.finished;
        paragraph?.onNodeEnd?.Invoke();
        onEndNode?.Invoke();
        currentNodeTalkable?.OnNodeEnd(paragraph);
        (dialogue as DialogueContainerWithModels)?.OnNodeEnd(paragraph);
        InvisibleText.ShowAllCharacters(dialogueText);
    }
    

    public void EndDialogue(bool animate = true)
    {
        typingState = TypingState.notStarted;
        dialogueActive = false;

        // A node may still be waiting on its objects' appearing animations - drop it, otherwise
        // it would start typing into a closing window.
        if (startParagraphCoroutine != null) StopCoroutine(startParagraphCoroutine);
        startParagraphCoroutine = null;
        nodeStarting = false;

        // Run each driven object's end lifecycle (end animation + events, then reset/disable)
        // before we drop the ref.
        (dialogue as DialogueContainerWithModels)?.OnDialogueEnd();

        dialogue = null;
        currentParagraphs.Clear();

        onDialogueEnd?.Invoke();
        initiatingTalkable?.OnDialogueEnd();
        initiatingTalkable = null;

        // The dialogue ends and the close animation runs at the same time (unlike start,
        // which waits). The window is only deactivated once the animation finishes.
        if (animate && windowAnimations && dialogueWindow.activeInHierarchy)
            StartCoroutine(EndDialogueRoutine());
        else
            dialogueWindow.SetActive(false);
    }

    private IEnumerator EndDialogueRoutine()
    {
        yield return windowAnimations.OnDialogueEnd();
        dialogueWindow.SetActive(false);
    }
}

// current ver