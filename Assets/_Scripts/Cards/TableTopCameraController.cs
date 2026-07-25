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
        WindowView
    }

    [Header("References")]
    [Tooltip("Transform actually moved/rotated. Falls back to this object's transform (attach this " +
             "component to the camera, or point this at the camera transform).")]
    [SerializeField] private Transform camTransform;
    [Tooltip("The table this camera looks straight down on in TableView. If left empty it falls back " +
             "to the first \"Table\"-tagged PlacingArea in the scene.")]
    public PlacingArea currentTable;

    [Header("State")]
    [Tooltip("Which mode the camera starts in (applied on Start).")]
    [SerializeField] private State state = State.HandView;

    /// <summary>The mode the camera is currently in.</summary>
    public State CurrentState => state;

    [Header("Transition")]
    [Tooltip("Seconds to ease from one mode's pose to the next. 0 = snap instantly.")]
    [SerializeField] private float transitionDuration = 0.5f;
    [Tooltip("Easing curve used for the move + rotate between modes.")]
    [SerializeField] private Ease transitionEase = Ease.OutCubic;
    [Tooltip("Snap (no tween) when the starting state is applied on Start.")]
    [SerializeField] private bool instantOnStart = true;

    [Header("Table view")]
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

    [Header("Hand view")]
    [Tooltip("If true, the camera's local pose at startup is captured as the hand-view pose, so the " +
             "hand view = wherever the camera starts in the scene. Turn OFF to author " +
             "handViewLocalPosition / handViewLocalEuler by hand.")]
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

    [Header("Free view / mouse look")]
    [Tooltip("Mouse-look on the camera rig (usually on CameraParent). Auto-found up the parent chain " +
             "if left empty. Enabled only in Free view; disabled in Hand/Table view.")]
    [SerializeField] private MouseLook mouseLook;

    [Header("Taxometer view")]
    [Tooltip("Camera LOCAL position (relative to its parent) used while the Taxometer view is held (D).")]
    [SerializeField] private Vector3 taxometerViewLocalPosition;
    [Tooltip("Camera LOCAL rotation, in euler degrees, used while the Taxometer view is held (D).")]
    [SerializeField] private Vector3 taxometerViewLocalEuler;

    [Header("Window view")]
    [Tooltip("Camera LOCAL position (relative to its parent) used while the Window view is held (A).")]
    [SerializeField] private Vector3 windowViewLocalPosition;
    [Tooltip("Camera LOCAL rotation, in euler degrees, used while the Window view is held (A).")]
    [SerializeField] private Vector3 windowViewLocalEuler;

    // Active move/rotate tweens, stopped before a new transition starts.
    private Tween posTween;
    private Tween rotTween;
    private Tween playerRotTween;

    // Hold-to-view (D = Taxometer, A = Window): remember the state to restore on release.
    private bool isHoldingView;
    private KeyCode holdViewKey;
    private State stateBeforeHold;

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
            if (playerTransform) handViewPlayerLocalEuler = playerTransform.localEulerAngles;
        }

        if (!currentTable) currentTable = FindTable();

        SwitchState(state, instant: instantOnStart);
    }

    private void Update()
    {
        // While a hold view (D/Taxometer, A/Window) is active, ignore the W/S ladder so releasing the
        // held key restores the state the player was actually in.
        if (!isHoldingView)
        {
            if (Input.GetKeyDown(KeyCode.W))
            {
                ChangeStateUp();
            }
            else if (Input.GetKeyDown(KeyCode.S))
            {
                ChangeStateDown();
            }
        }

        HandleHoldViews();
    }

    /// <summary>
    /// Hold D to peek at the Taxometer view, hold A to peek at the Window view. Releasing the held key
    /// restores whatever state was active before the peek. Only the key that started the peek ends it,
    /// so pressing the other key mid-hold is ignored.
    /// </summary>
    private void HandleHoldViews()
    {
        if (!isHoldingView)
        {
            if (Input.GetKeyDown(KeyCode.D)) BeginHoldView(KeyCode.D, State.TaxometerView);
            else if (Input.GetKeyDown(KeyCode.A)) BeginHoldView(KeyCode.A, State.WindowView);
        }
        else if (Input.GetKeyUp(holdViewKey))
        {
            EndHoldView();
        }
    }

    private void BeginHoldView(KeyCode key, State view)
    {
        isHoldingView = true;
        holdViewKey = key;
        stateBeforeHold = state;   // remember the previous state
        SwitchState(view);
    }

    private void EndHoldView()
    {
        isHoldingView = false;
        SwitchState(stateBeforeHold);   // restore the previous state
    }

    public void ChangeStateUp()
    {
        if (state == State.TableView) return;
        if (state == State.HandView)
        {
            SwitchToTableView();
        } else if (state == State.Free)
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
        } else if (state == State.TableView)
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
        state = newState;

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

    /// <summary>Top-down over the table; hand stops following the camera.</summary>
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
    }

    /// <summary>Return to the saved hand pose; hand follows the camera.</summary>
    private void ApplyHandView(bool instant)
    {
        if (mouseLook) mouseLook.canLook = false;
        SetCursorVisible(true);
        SetCardsFollow(true);
        MoveToLocal(handViewLocalPosition, Quaternion.Euler(handViewLocalEuler), instant);
        // Free look yaws the player, so restore the player's remembered rotation too, otherwise the
        // hand view would come back facing wherever mouse look left the player.
        RotatePlayerLocal(Quaternion.Euler(handViewPlayerLocalEuler), instant);
    }

    /// <summary>
    /// Free look: INSTANTLY level the camera pitch (X -> 0), keeping the current yaw, then hand full
    /// control to MouseLook. The controller does NO further rotation — MouseLook owns all free-look
    /// rotation. The hand does not follow the camera here.
    /// </summary>
    private void ApplyFreeView(bool instant)
    {
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
    }

    /// <summary>Peek pose held while A is down; hand stops following the camera. Restored on release.</summary>
    private void ApplyWindowView(bool instant)
    {
        if (mouseLook) mouseLook.canLook = false;
        SetCursorVisible(true);
        SetCardsFollow(false);
        MoveToLocal(windowViewLocalPosition, Quaternion.Euler(windowViewLocalEuler), instant);
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
