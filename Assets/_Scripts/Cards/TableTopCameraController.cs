using System;
using UnityEngine;
using PrimeTween;
using UnityEngine.InputSystem;

/// <summary>
/// Drives the card-game camera between three framing modes and keeps the hand's
/// "follow the camera" behaviour in sync with the mode.
///
/// Modes (see <see cref="State"/>):
///   - <see cref="State.TableView"/>: looks straight down on <see cref="currentTable"/> so the
///     player can read the whole table. Zoom is the camera's height above the surface
///     (<see cref="tableViewZoom"/>). The hand does NOT follow the camera here.
///   - <see cref="State.HandView"/>: the default "playing" pose. The camera returns to a saved
///     local pose (by default whatever pose it starts the scene in — see
///     <see cref="handViewIsInitView"/>) and the hand follows the camera.
///   - <see cref="State.Free"/>: minimal for now — the camera just levels out to look forward at
///     the horizon and the hand does not follow the camera.
///   - <see cref="State.InitView"/>: a one-time-only initial view shown at the very start of the game.
///     Once faded to any other state, it never appears again. While active, all player input AND
///     mouse cursor are locked. When another script transitions away from InitView, the mouse is
///     permanently unlocked for the rest of the game.
///
/// Transitions are eased with PrimeTween. Table view is authored in world space (above the
/// table); hand/free views are authored in the camera's local space (relative to its parent
/// rig), matching how the camera is set up in the scene.
///
/// Spatial convention (matches the project): the table is the XZ plane, Y is up.
/// </summary>
public class TableTopCameraController : MonoBehaviour
{
    public static TableTopCameraController Instance;

    public enum State
    {
        TableView,
        HandView,
        Free,
        TaxometerView,
        WindowView,
        InitView
    }

    [Header("References")]
    [Tooltip("Transform actually moved/rotated. Falls back to this object's transform (attach this " +
             "component to the camera, or point this at the camera transform).")]
    [SerializeField] private Transform camTransform;
    [Tooltip("The table this camera looks straight down on in TableView. If left empty it falls back " +
             "to the first \"Table\"-tagged PlacingArea in the scene.")]
    public PlacingArea currentTable;

    [Header("State")]
    [Tooltip("The home/main mode the camera rests in. Applied on Start, and returned to whenever a " +
             "peek/hold key (W/S/D/A) is released. Change at runtime with ChangeMainState.")]
    [SerializeField] private State mainState = State.HandView;

    // The mode the camera is currently in (set by SwitchState). Starts at the main state.
    private State state = State.HandView;

    /// <summary>The mode the camera is currently in.</summary>
    public State CurrentState => state;

    /// <summary>The home/main mode the camera returns to when a peek/hold key is released.</summary>
    public State MainState => mainState;

    // While locked, ChangeMainState (and therefore the ChangeMainStateToHand / ToFree UnityEvent hooks
    // fired by DialogueManager) is ignored, so the home state stays wherever a cutscene put it.
    // Only ForceChangeMainState can move it. See SetMainStateLocked.
    private bool mainStateLocked = false;

    /// <summary>Whether the home/main state is currently locked against ChangeMainState.</summary>
    public bool MainStateLocked => mainStateLocked;

    [Header("Transition")]
    [Tooltip("Seconds to ease from one mode's pose to the next. 0 = snap instantly.")]
    [SerializeField] private float transitionDuration = 0.5f;
    [Tooltip("Easing curve used for the move + rotate between modes.")]
    [SerializeField] private Ease transitionEase = Ease.OutCubic;
    [Tooltip("Snap (no tween) when the starting state is applied on Start.")]
    [SerializeField] private bool instantOnStart = true;

    [Header("Init view")]
    [Tooltip("If true, the game starts in InitView instead of MainState. InitView only appears once — " +
             "after transitioning away from it, it never comes back. While InitView is active, ALL player " +
             "input AND the mouse cursor are locked. When another script transitions away, the mouse is " +
             "permanently unlocked.")]
    [SerializeField] private bool useInitView = true;
    [Tooltip("Camera LOCAL position (relative to its parent) used for the Init view.")]
    [SerializeField] private Vector3 initViewLocalPosition;
    [Tooltip("Camera LOCAL rotation, in euler degrees, used for the Init view.")]
    [SerializeField] private Vector3 initViewLocalEuler;
    [Tooltip("Player local rotation, in euler degrees, restored when entering the Init view (only its " +
             "Y/yaw is applied).")]
    [SerializeField] private Vector3 initViewPlayerLocalEuler;
    [Tooltip("If true, the InitView pose is captured from the scene at startup (overrides the serialized " +
             "initViewLocalPosition/Euler fields).")]
    [SerializeField] private bool initViewIsScenePose = true;

    [Header("Table view")]
    [Tooltip("If true, W becomes hold-to-peek: hold W for Table view, release to return to Hand view. " +
             "If false, W keeps the old ladder behaviour (W = step up Hand -> Table).")]
    [SerializeField] private bool holdToTable = true;
    [Tooltip("Height in world units the camera sits above the table surface while looking straight " +
             "down. This is the table-view zoom: higher = more of the table in frame.")]
    [SerializeField] private float tableViewZoom = 6f;
    [Tooltip("Lower clamp for tableViewZoom.")]
    [SerializeField] private float minTableViewZoom = 1.5f;
    [Tooltip("Upper clamp for tableViewZoom.")]
    [SerializeField] private float maxTableViewZoom = 25f;
    [Tooltip("Camera LOCAL X rotation (pitch) in table view. 90 = looking straight down. Only X is " +
             "set; the camera's local Y and Z (heading/roll) are kept exactly as they were.")]
    [SerializeField] private float tableViewPitchX = 90f;
    [Tooltip("Player local rotation, in euler degrees, restored when entering the table view (only its " +
             "Y/yaw is applied). Overwritten from the scene pose on Start when handViewIsInitView is on.")]
    [SerializeField] private Vector3 tableViewPlayerLocalEuler;

    [Header("Hand view")]
    [Tooltip("If true, the camera's local pose at startup is captured as the hand-view pose, so the " +
             "hand view = wherever the camera starts in the scene. This also seeds EVERY state's player " +
             "rotation (table/free/taxometer/window) from the startup player yaw, so by default every " +
             "state restores the player to the same starting facing. Turn OFF to author " +
             "handViewLocalPosition / handViewLocalEuler and each state's *PlayerLocalEuler by hand.")]
    [SerializeField] private bool handViewIsInitView = true;
    [Tooltip("Camera local position (relative to its parent) used for the hand view. " +
             "Overwritten from the scene pose on Start when handViewIsInitView is on.")]
    [SerializeField] private Vector3 handViewLocalPosition;
    [Tooltip("Camera local rotation, in euler degrees, used for the hand view. " +
             "Overwritten from the scene pose on Start when handViewIsInitView is on.")]
    [SerializeField] private Vector3 handViewLocalEuler;
    [Tooltip("The player root (the camera parent's parent) whose rotation is restored together with " +
             "the hand view. Auto-found from MouseLook / the camera's grandparent if left empty.")]
    [SerializeField] private Transform playerTransform;
    [Tooltip("Player local rotation, in euler degrees, restored together with the hand view (so free " +
             "look can turn the player and the hand view still faces the right way). " +
             "Overwritten from the scene pose on Start when handViewIsInitView is on.")]
    [SerializeField] private Vector3 handViewPlayerLocalEuler;
    [Tooltip("When returning to Hand view from Free / Window / Taxometer view, wait this many seconds " +
             "before the hand starts following the camera again. 0 = follow immediately.")]
    [SerializeField] private float handFollowDelayAfterFreeViews = 1f;

    [Header("Free view / mouse look")]
    [Tooltip("If true, Free view is full mouse-look 'free aspect' (levels the pitch and hands control " +
             "to MouseLook). If false, Free view is just another fixed pose: the camera moves/rotates " +
             "to freeViewLocalPosition / freeViewLocalEuler like the other states, with no mouse look.")]
    [SerializeField] private bool freeAspectCamera = false;
    [Tooltip("Camera LOCAL position (relative to its parent) used for Free view when freeAspectCamera " +
             "is OFF.")]
    [SerializeField] private Vector3 freeViewLocalPosition;
    [Tooltip("Camera LOCAL rotation, in euler degrees, used for Free view when freeAspectCamera is OFF.")]
    [SerializeField] private Vector3 freeViewLocalEuler;
    [Tooltip("Player local rotation, in euler degrees, restored when entering the fixed Free view (only " +
             "its Y/yaw is applied). Overwritten from the scene pose on Start when handViewIsInitView is on.")]
    [SerializeField] private Vector3 freeViewPlayerLocalEuler;
    [Tooltip("If true, entering Free view is hold-to-peek from Hand view: hold S for Free view and " +
             "release to return to Hand view. If false, S latches Free view via the normal ladder.")]
    [SerializeField] private bool holdToFreeAspect = false;
    [Tooltip("Mouse-look on the camera rig (usually on CameraParent). Auto-found up the parent chain " +
             "if left empty. Enabled only in Free view when freeAspectCamera is ON; disabled otherwise.")]
    [SerializeField] private MouseLook mouseLook;

    [Header("Taxometer view")]
    [Tooltip("Camera LOCAL position (relative to its parent) used while the Taxometer view is held (D).")]
    [SerializeField] private Vector3 taxometerViewLocalPosition;
    [Tooltip("Camera LOCAL rotation, in euler degrees, used while the Taxometer view is held (D).")]
    [SerializeField] private Vector3 taxometerViewLocalEuler;
    [Tooltip("Player local rotation, in euler degrees, restored while the Taxometer view is held (only its " +
             "Y/yaw is applied). Overwritten from the scene pose on Start when handViewIsInitView is on.")]
    [SerializeField] private Vector3 taxometerViewPlayerLocalEuler;
    [Tooltip("Looped SFX (R.PROJECT.Audio.sfx.taxometersound) played through the SFX mixer group while the " +
             "Taxometer view is active. Fades in on enter and out on leave.")]
    [SerializeField] private bool playTaxometerSound = true;
    [Tooltip("Target volume of the looped taxometer sound once faded in.")]
    [SerializeField, Range(0f, 1f)] private float taxometerSoundVolume = 1f;
    [Tooltip("Fade in / fade out time in seconds for the looped taxometer sound.")]
    [SerializeField] private float taxometerSoundFade = 0.67f;

    [Header("Window view")]
    [Tooltip("Camera LOCAL position (relative to its parent) used while the Window view is held (A).")]
    [SerializeField] private Vector3 windowViewLocalPosition;
    [Tooltip("Camera LOCAL rotation, in euler degrees, used while the Window view is held (A).")]
    [SerializeField] private Vector3 windowViewLocalEuler;
    [Tooltip("Player local rotation, in euler degrees, restored while the Window view is held (only its " +
             "Y/yaw is applied). Overwritten from the scene pose on Start when handViewIsInitView is on.")]
    [SerializeField] private Vector3 windowViewPlayerLocalEuler;
    [Tooltip("When entering the Window view, force the turn to sweep over the LEFT shoulder " +
             "(counterclockwise) instead of taking the shortest path.")]
    [SerializeField] private bool windowTurnOverLeftShoulder = true;

    // Active move/rotate tweens, stopped before a new transition starts.
    private Tween posTween;
    private Tween rotTween;
    private Tween playerRotTween;

    // Looping AudioSource for the taxometer view sound. Created lazily and routed through the SFX mixer
    // group so it plays as an "SFX manager" sound; volume is tweened for the fade in/out.
    private AudioSource taxometerLoopSource;
    private Tween taxometerVolumeTween;

    // Pending delayed "hand follows camera" enable when returning to Hand view from a free/peek view.
    // Stopped as soon as the state changes again so a superseded return never re-enables follow.
    private Tween handFollowDelayTween;

    // The state the camera was in before the current one (set in SwitchState). Used to decide whether
    // returning to Hand view should delay re-enabling hand-follow.
    private State previousState;

    // Hold-to-view (W = Table, S = Free, D = Taxometer, A = Window). On release the camera always
    // returns to mainState, so no per-hold restore state is remembered.
    private bool isHoldingView;
    private KeyCode holdViewKey;

    // Tracks whether the InitView has been used. Once true, InitView is permanently disabled
    // AND the mouse is permanently unlocked.
    private bool initViewUsed = false;

    private void Awake()
    {
        h.CreateStaticInstance(this, ref Instance, setDontDestroy: false);
        if (!camTransform) camTransform = transform;
        if (!mouseLook) mouseLook = GetComponentInParent<MouseLook>();
        // The player root = the camera parent's parent (Player -> CameraParent -> Camera). MouseLook
        // lives on the player, so prefer that; otherwise walk up from the camera.
        if (!playerTransform)
        {
            if (mouseLook) playerTransform = mouseLook.transform;
            else if (camTransform && camTransform.parent) playerTransform = camTransform.parent.parent;
        }
    }

    private void Start()
    {
        // Capture the authored/initial local pose as the hand view if requested. Done in Start so
        // any parent rig has finished its own setup first.
        if (handViewIsInitView && camTransform)
        {
            handViewLocalPosition = camTransform.localPosition;
            handViewLocalEuler = camTransform.localEulerAngles;
            if (playerTransform)
            {
                handViewPlayerLocalEuler = playerTransform.localEulerAngles;
                // Seed each per-state player rotation from the same startup pose, so by default every
                // state restores the player to the starting yaw exactly as before. Turn handViewIsInitView
                // OFF to author each state's player rotation independently in the inspector.
                tableViewPlayerLocalEuler = handViewPlayerLocalEuler;
                freeViewPlayerLocalEuler = handViewPlayerLocalEuler;
                taxometerViewPlayerLocalEuler = handViewPlayerLocalEuler;
                windowViewPlayerLocalEuler = handViewPlayerLocalEuler;
            }
        }

        // Capture InitView pose from the scene if requested, BEFORE the start state is applied.
        if (useInitView && initViewIsScenePose && camTransform)
        {
            initViewLocalPosition = camTransform.localPosition;
            initViewLocalEuler = camTransform.localEulerAngles;
            if (playerTransform)
            {
                initViewPlayerLocalEuler = playerTransform.localEulerAngles;
            }
        }

        if (!currentTable) currentTable = FindTable();

        // If InitView is enabled and hasn't been used yet, start there; otherwise start at the main state.
        if (useInitView && !initViewUsed)
        {
            SwitchState(State.InitView, instant: instantOnStart);
        }
        else
        {
            SwitchState(mainState, instant: instantOnStart);
        }
    }

    private void Update()
    {
        // While InitView is active, block ALL player input.
        // The state can only be changed by another script calling SwitchToHandView(), etc.
        if (state == State.InitView) return;

        // While a hold view is active, ignore the W/S ladder so releasing the held key restores the
        // intended state. When holdToTable is on, W is consumed by the hold system (below), not the ladder.
        if (!isHoldingView)
        {
            if (!holdToTable && Input.GetKeyDown(KeyCode.W))
            {
                ChangeStateUp();
            }
            else if (!holdToFreeAspect && Input.GetKeyDown(KeyCode.S))
            {
                ChangeStateDown();
            }
        }

        HandleHoldViews();
    }

    /// <summary>
    /// Peek keys are hold-to-view: hold W for Table, S for Free (when <see cref="holdToFreeAspect"/> is
    /// on), D for Taxometer, A for Window. Releasing the held key always returns to
    /// <see cref="mainState"/> — the current home state — regardless of where the peek started. A peek
    /// only begins from the main state; from any other (latched) state pressing W steps back to the main
    /// state instead. Only the key that started the peek ends it, so pressing another hold key mid-hold
    /// is ignored.
    /// </summary>
    private void HandleHoldViews()
    {
        if (!isHoldingView)
        {
            if (Input.GetKeyDown(KeyCode.S) && mainState == State.Free)
            {
                // When Free is the home state, S sends the home back to Hand view.
                BeginHoldView(KeyCode.S, State.HandView);
            }
            else if (holdToTable && Input.GetKeyDown(KeyCode.W))
            {
                // From the main state, hold W to peek the Table view (release returns to main).
                // From any other (latched) state, W steps back to the main state.
                if (state == mainState)
                {
                    if (mainState != State.TableView) BeginHoldView(KeyCode.W, State.TableView);
                }
                else SwitchState(mainState);
            }
            else if (holdToFreeAspect && Input.GetKeyDown(KeyCode.S) && state == mainState
                     && mainState != State.Free)
            {
                // From the main state, hold S to peek the Free view (release returns to main).
                BeginHoldView(KeyCode.S, State.Free);
            }
            else if (Input.GetKeyDown(KeyCode.D) && mainState != State.TaxometerView)
                BeginHoldView(KeyCode.D, State.TaxometerView);
            else if (Input.GetKeyDown(KeyCode.A) && mainState != State.WindowView)
                BeginHoldView(KeyCode.A, State.WindowView);
        }
        else if (Input.GetKeyUp(holdViewKey))
        {
            EndHoldView();
        }
    }

    public void SetIsHoldingView(bool isHoldingViewIn)
    {
        isHoldingView = isHoldingViewIn;
    }



    private void BeginHoldView(KeyCode key, State view)
    {
        isHoldingView = true;
        holdViewKey = key;
        SwitchState(view);
    }

    private void EndHoldView()
    {
        isHoldingView = false;
        SwitchState(mainState);   // release always returns to the main state
    }

    /// <summary>
    /// Sets the home/main state the camera rests in and returns to when a peek/hold key (W/S/D/A) is
    /// released. Also moves the camera there right away, unless a peek is currently being held (in which
    /// case releasing the key brings it to the new home). Example: after ChangeMainState(State.Free),
    /// every peek release turns the camera to Free view.
    /// Does nothing while the main state is locked (<see cref="SetMainStateLocked"/>) — use
    /// <see cref="ForceChangeMainState"/> to move the home state anyway.
    /// </summary>
    public void ChangeMainState(State newMainState)
    {
        if (mainStateLocked)
        {
            h.Out("TableTopCameraController: main state is locked —", newMainState, "ignored.");
            return;
        }

        ForceChangeMainState(newMainState);
    }

    /// <summary>
    /// Same as <see cref="ChangeMainState"/>, but goes through even when the main state is locked.
    /// Use this from the code that owns the lock.
    /// </summary>
    public void ForceChangeMainState(State newMainState)
    {
        mainState = newMainState;
        if (!isHoldingView) SwitchState(mainState);
    }

    /// <summary>
    /// Locks (or unlocks) the home/main state so <see cref="ChangeMainState"/> is ignored. This is what
    /// keeps outside hooks — notably DialogueManager's onDialogueStart / onDialogueEnd UnityEvents wired
    /// to ChangeMainStateToFree / ChangeMainStateToHand — from yanking the camera home back during a
    /// cutscene that has deliberately parked it somewhere. Peek/hold keys still work; they just return
    /// to the locked home state.
    /// </summary>
    public void SetMainStateLocked(bool locked)
    {
        mainStateLocked = locked;
    }

    public void ChangeMainStateToHand()
    {
        ChangeMainState(State.HandView);
    }

    public void ChangeMainStateToFree()
    {
        ChangeMainState(State.Free);
    }

    public void ChangeStateUp()
    {
        if (state == State.TableView) return;
        if (state == State.HandView)
        {
            SwitchToTableView();
        }
        else if (state == State.Free)
        {
            SwitchToHandView();
        }
    }

    public void ChangeStateDown()
    {
        if (state == State.Free) return;
        if (state == State.HandView)
        {
            SwitchToFree();
        }
        else if (state == State.TableView)
        {
            SwitchToHandView();
        }
    }

    // ---- public API ---------------------------------------------------------

    public void SwitchToTableView() => SwitchState(State.TableView);
    public void SwitchToHandView() => SwitchState(State.HandView);
    public void SwitchToFree() => SwitchState(State.Free);
    public void SwitchToTaxometerView() => SwitchState(State.TaxometerView);
    public void SwitchToWindowView() => SwitchState(State.WindowView);

    /// <summary>
    /// Moves + rotates the camera into <paramref name="newState"/> and updates whether the hand
    /// follows the camera. Pass <paramref name="instant"/> = true to snap without a tween.
    /// </summary>
    public void SwitchState(State newState, bool instant = false)
    {
        // Prevent InitView from being re-entered once it's been used.
        if (newState == State.InitView && initViewUsed)
        {
            h.Out("TableTopCameraController: InitView has already been used. Ignoring switch to InitView.");
            return;
        }

        // If InitView was active and we're leaving it, mark it as used and permanently unlock the mouse.
        if (state == State.InitView && newState != State.InitView)
        {
            initViewUsed = true;
            UnlockMousePermanently();
        }

        previousState = state;
        state = newState;

        // Any pending delayed hand-follow enable is no longer valid once the state changes again.
        if (handFollowDelayTween.isAlive) handFollowDelayTween.Stop();

        // Looped taxometer SFX: fade in when entering the taxometer view, fade out when leaving it.
        if (newState == State.TaxometerView) StartTaxometerSound();
        else if (previousState == State.TaxometerView) StopTaxometerSound();

        switch (newState)
        {
            case State.TableView:
                ApplyTableView(instant);
                break;

            case State.HandView:
                ApplyHandView(instant);
                break;

            case State.Free:
                ApplyFreeView(instant);
                break;

            case State.TaxometerView:
                ApplyTaxometerView(instant);
                break;

            case State.WindowView:
                ApplyWindowView(instant);
                break;

            case State.InitView:
                ApplyInitView(instant);
                break;
        }
    }

    /// <summary>Current table-view zoom (camera height above the table surface).</summary>
    public float TableViewZoom => tableViewZoom;

    /// <summary>
    /// Sets the table-view zoom (camera height above the table). Clamped to
    /// [minTableViewZoom, maxTableViewZoom]; re-applies live when currently in table view.
    /// </summary>
    public void SetTableViewZoom(float zoom)
    {
        tableViewZoom = Mathf.Clamp(zoom, minTableViewZoom, maxTableViewZoom);
        if (state == State.TableView) ApplyTableView(instant: false);
    }

    /// <summary>Nudges the table-view zoom by <paramref name="delta"/> (positive = further away).</summary>
    public void ZoomTableView(float delta) => SetTableViewZoom(tableViewZoom + delta);

    /// <summary>
    /// Assigns the table looked at in table view. When <paramref name="switchToIt"/> is true the
    /// camera also switches to table view; otherwise it re-frames only if already in table view.
    /// </summary>
    public void SetTable(PlacingArea table, bool switchToIt = false)
    {
        currentTable = table;
        if (switchToIt) SwitchState(State.TableView);
        else if (state == State.TableView) ApplyTableView(instant: false);
    }

    // ---- per-mode application -----------------------------------------------

    /// <summary>
    /// One-time initial view pose. Hand stops following the camera, mouse is locked and hidden.
    /// When leaving this state (via any external script call), the mouse is permanently unlocked.
    /// </summary>
    private void ApplyInitView(bool instant)
    {
        if (mouseLook) mouseLook.canLook = false;
        // Lock and hide the mouse during the intro.
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
        SetCardsFollow(false);
        MoveToLocal(initViewLocalPosition, Quaternion.Euler(initViewLocalEuler), instant);
        RestorePlayerYaw(initViewPlayerLocalEuler, instant);
    }

    /// <summary>
    /// Permanently unlocks the mouse cursor for the rest of the game.
    /// Called automatically when transitioning away from InitView.
    /// </summary>
    private void UnlockMousePermanently()
    {
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    /// <summary>Top-down over the table; hand stops following the camera (frozen like the peek views).</summary>
    private void ApplyTableView(bool instant)
    {
        if (mouseLook) mouseLook.canLook = false;
        SetCursorVisible(true);

        SetCardsFollow(false);

        if (!currentTable) currentTable = FindTable();
        if (!currentTable)
        {
            h.Out("TableTopCameraController: no currentTable to look at (TableView skipped).");
            return;
        }

        if (!camTransform) camTransform = transform;

        Bounds b = TableBounds();
        Vector3 center = b.center;

        // Straight above the surface by the zoom height.
        Vector3 worldPos = new Vector3(center.x, b.max.y + tableViewZoom, center.z);

        // Only change the camera's LOCAL X rotation (pitch); keep its local Y and Z EXACTLY as they
        // were in the previous state. Working in local space (not world) avoids the world-euler
        // gimbal aliasing that made Y/Z jump — e.g. (90,90,0) being reported as (90,0,-90) — because
        // in the scene the yaw lives on CameraParent and the camera's own local Y/Z are 0.
        Vector3 le = camTransform.localEulerAngles;
        Quaternion localRot = Quaternion.Euler(tableViewPitchX, le.y, le.z);

        // Bring the world target position into the parent's space so position + rotation move
        // together in local space.
        Transform parent = camTransform.parent;
        Vector3 localPos = parent ? parent.InverseTransformPoint(worldPos) : worldPos;

        MoveToLocal(localPos, localRot, instant);
        // Free look yaws the player; restore the table view's saved yaw so the top-down framing lines up
        // with the table instead of inheriting wherever mouse look left the player.
        RestorePlayerYaw(tableViewPlayerLocalEuler, instant);
    }

    /// <summary>Return to the saved hand pose; hand follows the camera.</summary>
    private void ApplyHandView(bool instant)
    {
        if (mouseLook) mouseLook.canLook = false;
        SetCursorVisible(true);

        // Returning from Table / Free / Window / Taxometer view: wait a beat before the hand starts
        // following the camera again, so the hand doesn't snap to the camera the instant the view
        // returns. Any other origin (or an instant/Start apply, or a zero delay) follows immediately.
        bool cameFromFreeViews = previousState == State.TableView
                              || previousState == State.Free
                              || previousState == State.WindowView
                              || previousState == State.TaxometerView
                              || previousState == State.InitView;
        if (cameFromFreeViews && !instant && handFollowDelayAfterFreeViews > 0f)
        {
            SetCardsFollow(false);
            handFollowDelayTween = Tween.Delay(handFollowDelayAfterFreeViews, () => SetCardsFollow(true));
        }
        else
        {
            SetCardsFollow(true);
        }

        MoveToLocal(handViewLocalPosition, Quaternion.Euler(handViewLocalEuler), instant);
        // Free look yaws the player, so restore the hand view's remembered yaw too, otherwise the
        // hand view would come back facing wherever mouse look left the player.
        RestorePlayerYaw(handViewPlayerLocalEuler, instant);
    }

    /// <summary>
    /// Free view. Two modes, switched by <see cref="freeAspectCamera"/>:
    ///   - OFF (default): a plain fixed pose — move/rotate the camera to
    ///     <see cref="freeViewLocalPosition"/> / <see cref="freeViewLocalEuler"/> like the other
    ///     states, with mouse look disabled.
    ///   - ON: INSTANTLY level the camera pitch (X -> 0), keeping the current yaw, then hand full
    ///     control to MouseLook. The controller does NO further rotation — MouseLook owns all
    ///     free-look rotation.
    /// The hand does not follow the camera here either way.
    /// </summary>
    private void ApplyFreeView(bool instant)
    {
        if (!freeAspectCamera)
        {
            // Fixed free pose: behaves like the other fixed views.
            if (mouseLook) mouseLook.canLook = false;
            SetCursorVisible(true);
            SetCardsFollow(false);
            MoveToLocal(freeViewLocalPosition, Quaternion.Euler(freeViewLocalEuler), instant);
            RestorePlayerYaw(freeViewPlayerLocalEuler, instant);
            return;
        }

        SetCardsFollow(false);
        SetCursorVisible(false);
        StopTweens(); // cancel any hand/table move still tweening the camera

        // Instant one-off pitch reset on the camera MouseLook actually drives (its cam = Camera.main),
        // so the reset matches what mouse look continues from. Keep the current yaw, zero the roll.
        Transform cam = Camera.main ? Camera.main.transform : (camTransform ? camTransform : transform);
        cam.rotation = Quaternion.Euler(0f, cam.eulerAngles.y, 0f);

        // Resync MouseLook's internal look angles to this levelled pose (FreezeCamera(false) calls its
        // internal reset) so it doesn't snap the pitch back, then enable looking. Existing API — no
        // MouseLook changes.
        if (mouseLook)
        {
            mouseLook.FreezeCamera(false);
            mouseLook.canLook = true;
        }
    }

    /// <summary>Peek pose held while D is down; hand stops following the camera. Restored on release.</summary>
    private void ApplyTaxometerView(bool instant)
    {
        if (mouseLook) mouseLook.canLook = false;
        SetCursorVisible(true);
        SetCardsFollow(false);
        MoveToLocal(taxometerViewLocalPosition, Quaternion.Euler(taxometerViewLocalEuler), instant);
        RestorePlayerYaw(taxometerViewPlayerLocalEuler, instant);
    }

    // ---- taxometer sound ----------------------------------------------------

    /// <summary>
    /// Lazily creates the looping AudioSource for the taxometer sound, routed through the SFX mixer
    /// group (so it behaves like an SFX-manager sound) and loaded with R.PROJECT.Audio.sfx.taxometersound.
    /// </summary>
    private void EnsureTaxometerSource()
    {
        if (taxometerLoopSource) return;

        AudioClip clip = R.PROJECT.Audio.sfx.taxometersound;
        if (!clip)
        {
            h.Out("TableTopCameraController: taxometersound clip not found (taxometer SFX skipped).");
            return;
        }

        GameObject go = new GameObject("TaxometerLoopSource");
        go.transform.SetParent(transform);
        taxometerLoopSource = go.AddComponent<AudioSource>();
        taxometerLoopSource.clip = clip;
        taxometerLoopSource.loop = true;
        taxometerLoopSource.playOnAwake = false;
        taxometerLoopSource.volume = 0f;
        taxometerLoopSource.outputAudioMixerGroup = AudioMixerManager.GetSFXGroup();
    }

    /// <summary>Starts (if needed) the looped taxometer sound and fades it in over taxometerSoundFade.</summary>
    private void StartTaxometerSound()
    {
        if (!playTaxometerSound) return;
        EnsureTaxometerSource();
        if (!taxometerLoopSource) return;

        if (taxometerVolumeTween.isAlive) taxometerVolumeTween.Stop();

        if (!taxometerLoopSource.isPlaying)
        {
            taxometerLoopSource.volume = 0f;
            taxometerLoopSource.Play();
        }

        taxometerVolumeTween = Tween.AudioVolume(taxometerLoopSource, taxometerSoundVolume, taxometerSoundFade);
    }

    /// <summary>Fades the looped taxometer sound out over taxometerSoundFade, then stops the source.</summary>
    private void StopTaxometerSound()
    {
        if (!taxometerLoopSource || !taxometerLoopSource.isPlaying) return;

        if (taxometerVolumeTween.isAlive) taxometerVolumeTween.Stop();

        taxometerVolumeTween = Tween.AudioVolume(taxometerLoopSource, 0f, taxometerSoundFade)
            .OnComplete(this, cam =>
            {
                if (cam.taxometerLoopSource) cam.taxometerLoopSource.Stop();
            });
    }

    private void OnDestroy()
    {
        if (taxometerVolumeTween.isAlive) taxometerVolumeTween.Stop();
    }

    /// <summary>Peek pose held while A is down; hand stops following the camera. Restored on release.</summary>
    private void ApplyWindowView(bool instant)
    {
        if (mouseLook) mouseLook.canLook = false;
        SetCursorVisible(true);
        SetCardsFollow(false);

        if (windowTurnOverLeftShoulder)
        {
            // Force the turn to the window to go over the LEFT shoulder (counterclockwise) instead of
            // the shortest path. Whichever transform actually carries the yaw (the camera's local pose
            // or the player) sweeps left; the other has a ~0 yaw change and is left untouched.
            MoveToLocalTurningLeft(windowViewLocalPosition, windowViewLocalEuler, instant);
            RestorePlayerYawTurningLeft(windowViewPlayerLocalEuler, instant);
        }
        else
        {
            MoveToLocal(windowViewLocalPosition, Quaternion.Euler(windowViewLocalEuler), instant);
            RestorePlayerYaw(windowViewPlayerLocalEuler, instant);
        }
    }

    // ---- helpers ------------------------------------------------------------

    private void SetCardsFollow(bool follow)
    {
        if (HandManager.Instance) HandManager.Instance.SetFollowCamera(follow);
    }

    /// <summary>Shows + frees the cursor (Hand/Table view) or hides + locks it for mouse look (Free view).</summary>
    private void SetCursorVisible(bool visible)
    {
        Cursor.visible = visible;
        Cursor.lockState = visible ? CursorLockMode.None : CursorLockMode.Locked;
    }


    /// <summary>World-space move + rotate (used by the table/free views).</summary>
    private void MoveToWorld(Vector3 worldPos, Quaternion worldRot, bool instant)
    {
        if (!camTransform) camTransform = transform;
        StopTweens();

        if (instant || transitionDuration <= 0f)
        {
            camTransform.SetPositionAndRotation(worldPos, worldRot);
            return;
        }

        posTween = Tween.Position(camTransform, worldPos, transitionDuration, transitionEase);
        rotTween = Tween.Rotation(camTransform, worldRot, transitionDuration, transitionEase);
    }

    /// <summary>Local-space move + rotate (used by the hand view, relative to the parent rig).</summary>
    private void MoveToLocal(Vector3 localPos, Quaternion localRot, bool instant)
    {
        if (!camTransform) camTransform = transform;
        StopTweens();

        if (instant || transitionDuration <= 0f)
        {
            camTransform.localPosition = localPos;
            camTransform.localRotation = localRot;
            return;
        }

        posTween = Tween.LocalPosition(camTransform, localPos, transitionDuration, transitionEase);
        rotTween = Tween.LocalRotation(camTransform, localRot, transitionDuration, transitionEase);
    }

    /// <summary>
    /// Like <see cref="MoveToLocal"/>, but the local Y (yaw) is forced to sweep over the LEFT shoulder
    /// (counterclockwise) rather than taking the shortest path — used when turning to the window.
    /// Tweens the euler angles component-wise so the yaw can travel more than 180 degrees.
    /// </summary>
    private void MoveToLocalTurningLeft(Vector3 localPos, Vector3 localEuler, bool instant)
    {
        if (!camTransform) camTransform = transform;
        StopTweens();

        if (instant || transitionDuration <= 0f)
        {
            camTransform.localPosition = localPos;
            camTransform.localEulerAngles = localEuler;
            return;
        }

        Vector3 startEuler = camTransform.localEulerAngles;
        posTween = Tween.LocalPosition(camTransform, localPos, transitionDuration, transitionEase);
        rotTween = Tween.LocalEulerAngles(camTransform, startEuler, LeftShoulderEuler(startEuler, localEuler),
                                          transitionDuration, transitionEase);
    }

    /// <summary>
    /// Builds a target euler equal to <paramref name="targetEuler"/> but whose Y is expressed so that
    /// tweening from <paramref name="startEuler"/> sweeps over the LEFT shoulder (yaw decreasing). If
    /// the shortest turn is already leftward or negligible it is kept as-is, so an axis that doesn't
    /// actually turn never spins a full circle.
    /// </summary>
    private static Vector3 LeftShoulderEuler(Vector3 startEuler, Vector3 targetEuler)
    {
        // Unity: increasing Y yaws right (clockwise from above), so a LEFT turn is a decreasing yaw.
        float yawDelta = Mathf.DeltaAngle(startEuler.y, targetEuler.y); // shortest signed turn [-180,180]
        if (yawDelta > 0.5f) yawDelta -= 360f;                          // redirect a right turn the long way
        return new Vector3(targetEuler.x, startEuler.y + yawDelta, targetEuler.z);
    }

    /// <summary>
    /// Restores ONLY the Y rotation (yaw) of the player root (the MouseLook object) to
    /// <paramref name="playerEuler"/>'s yaw, keeping its current X/Z. Each fixed view
    /// (hand/table/free/window/taxometer) passes its own per-state player euler so that after free look
    /// has yawed the player, the view snaps back to that state's authored facing instead of inheriting
    /// wherever mouse look left the player.
    /// </summary>
    private void RestorePlayerYaw(Vector3 playerEuler, bool instant)
    {
        if (!playerTransform) return;
        Vector3 cur = playerTransform.localEulerAngles;
        Quaternion target = Quaternion.Euler(cur.x, playerEuler.y, cur.z);
        RotatePlayerLocal(target, instant);
    }

    /// <summary>
    /// Like <see cref="RestorePlayerYaw"/>, but forces the player's yaw to sweep over the LEFT shoulder
    /// (counterclockwise) instead of the shortest path. Used when turning to the window so the turn
    /// direction is consistent whether the yaw is carried by the player or the camera.
    /// </summary>
    private void RestorePlayerYawTurningLeft(Vector3 playerEuler, bool instant)
    {
        if (!playerTransform) return;
        if (playerRotTween.isAlive) playerRotTween.Stop();

        Vector3 cur = playerTransform.localEulerAngles;
        Vector3 target = new Vector3(cur.x, playerEuler.y, cur.z);

        if (instant || transitionDuration <= 0f)
        {
            playerTransform.localRotation = Quaternion.Euler(target);
            return;
        }

        playerRotTween = Tween.LocalEulerAngles(playerTransform, cur, LeftShoulderEuler(cur, target),
                                                transitionDuration, transitionEase);
    }

    /// <summary>Rotates the player root to <paramref name="localRot"/> (used by the hand view).</summary>
    private void RotatePlayerLocal(Quaternion localRot, bool instant)
    {
        if (!playerTransform) return;
        if (playerRotTween.isAlive) playerRotTween.Stop();

        if (instant || transitionDuration <= 0f)
        {
            playerTransform.localRotation = localRot;
            return;
        }

        playerRotTween = Tween.LocalRotation(playerTransform, localRot, transitionDuration, transitionEase);
    }

    private void StopTweens()
    {
        if (posTween.isAlive) posTween.Stop();
        if (rotTween.isAlive) rotTween.Stop();
        if (playerRotTween.isAlive) playerRotTween.Stop();
    }

    /// <summary>World-space bounds of the table surface (collider, then renderer, then a unit box).</summary>
    private Bounds TableBounds()
    {
        if (currentTable)
        {
            if (currentTable.Area) return currentTable.Area.bounds;
            if (currentTable.TryGetComponent(out Collider col)) return col.bounds;
            if (currentTable.TryGetComponent(out Renderer rend)) return rend.bounds;
            return new Bounds(currentTable.transform.position, Vector3.one);
        }
        return new Bounds(camTransform ? camTransform.position : transform.position, Vector3.one);
    }

    private PlacingArea FindTable()
    {
        GameObject go = GameObject.FindGameObjectWithTag("Table");
        if (go && go.TryGetComponent(out PlacingArea pa)) return pa;
        return FindFirstObjectByType<PlacingArea>();
    }
}