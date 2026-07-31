using System.Collections.Generic;
using PrimeTween;
using TMPro;
using UnityEngine;

/// <summary>
/// A single on-table suit counter, living on the SuitTracker prefab
/// (Assets/_Prefabs/Cards/SuitTracker). It shows a suit's name (title), its animated
/// sprite tag, and the current count for that suit.
///
/// TableManager keeps one SuitTracker per suit and pushes count changes in through
/// <see cref="SetCount"/>; each change plays <see cref="countChangeAnim"/> so a count tick
/// reads visibly. The suit sprite flip-books through its frames exactly like the cards do
/// (see Card / CardDataBase), via a <see cref="ChangingTextAnimationn"/> on the suit text.
/// </summary>
public class SuitTracker : MonoBehaviour
{
    [Header("Texts")]
    [Tooltip("Shows the suit's name (e.g. \"Lust\").")]
    [SerializeField] private TMP_Text titleTmp;
    [Tooltip("Shows the suit's sprite tag; flip-books through its sprite frames like the cards do.")]
    [SerializeField] private TMP_Text suitTmp;
    [Tooltip("Shows the current count for this suit.")]
    [SerializeField] private TMP_Text countTmp;

    [Header("Animations")]
    [Tooltip("Played once whenever the count changes. Auto-found on countTmp if left empty.")]
    [SerializeField] private AnimationBase countChangeAnim;
    [Tooltip("Suit sprite flip-book (mirrors the cards). Auto-found on suitTmp if left empty.")]
    [SerializeField] private ChangingTextAnimationn suitAnim;

    [Header("State")]
    [Tooltip("The suit this tracker represents. Set per-instance so TableManager can key its dictionary by it.")]
    public CP.Suit targetSuit;
    public int currentCount;


    [Header("Meshes")]
    public GameObject front;
    public GameObject back;

    // The MasterShaderGraphForCard color property recolored when this suit is achieved
    // (see GamePlusManager).
    private static readonly int TextureColorId = Shader.PropertyToID("_TextureColor");

    // Live tweens per material, so a new recolor takes over from whatever is currently showing
    // instead of fighting it.
    private readonly Dictionary<Material, Tween> _colorTweens = new Dictionary<Material, Tween>();

    // Each mesh material's TextureColor as it was before any achievement recolor, captured the
    // first time that material is touched, so ResetTextureColor can put it back exactly.
    private readonly Dictionary<Material, Color> _originalColors = new Dictionary<Material, Color>();

    private void Awake()
    {
        ResolveRefs();
    }

    // Resolve the animation components from their text objects if they weren't wired in the inspector
    // (same fallback pattern the Card uses for its ChangingTextAnimations).
    private void ResolveRefs()
    {
        if (!countChangeAnim && countTmp) countChangeAnim = countTmp.GetComponentInChildren<AnimationBase>();
        if (!suitAnim && suitTmp) suitAnim = suitTmp.GetComponentInChildren<ChangingTextAnimationn>();
    }

    /// <summary>
    /// Points this tracker at <paramref name="suit"/>: writes the suit name to the title,
    /// starts the suit sprite flip-book, and shows <paramref name="count"/> (without playing
    /// the count-change animation, since this is the initial state).
    /// </summary>
    public void Initialize(CP.Suit suit, int count = 0)
    {
        h.Out("Tracker INit ", suit, count);
        ResolveRefs();
        targetSuit = suit;

        if (titleTmp) titleTmp.text = suit.ToString();

        PlaySuitFlipbook();

        currentCount = count;
        RefreshCount();
    }

    /// <summary>Updates the shown count and plays the count-change animation.</summary>
    public void SetCount(int count)
    {
        currentCount = count;
        RefreshCount();
        PlayCountChangeAnimation();
    }

    private void RefreshCount()
    {
        if (countTmp) countTmp.text = currentCount.ToString();
    }

    /// <summary>
    /// Plays the count-change animation on its own. Public so beats that want the tracker to react
    /// without its number changing can trigger it — e.g. the sin cutscene's "adding suit" moment,
    /// where the tracker matching the cutscene's suit animates but its count stays put.
    ///
    /// When <paramref name="markSinAchieved"/> is on (the sin cutscene passes it), this doubles as
    /// the achievement moment: the first time this suit's cutscene is chosen, the tracker's front
    /// and back meshes are recolored to <see cref="GamePlusManager.achivedCardColor"/> — started
    /// right here so it runs alongside the animation — and the suit is written to the save file.
    /// If the sin was already achieved before, only the animation plays and the color is left alone.
    /// </summary>
    public void PlayCountChangeAnimation(bool markSinAchieved = false)
    {
        ResolveRefs();
        if (countChangeAnim) countChangeAnim.PlayInstantly();

        if (!markSinAchieved) return;

        GamePlusManager achievements = GamePlusManager.Instance;
        if (achievements == null)
        {
            h.Out("SuitTracker: no GamePlusManager in the scene — achievement color skipped.");
            return;
        }

        // TryAchieve saves to disk and returns true only on the first time for this suit.
        if (!achievements.TryAchieve(targetSuit)) return;

        achievements.ApplyAchievedColor(this);
    }

    /// <summary>
    /// Writes <paramref name="color"/> into the TextureColor material property of every renderer
    /// under <see cref="front"/> and <see cref="back"/>, fading over <paramref name="duration"/>
    /// seconds (0 = snap). Renderer.material is used, so each tracker gets its own material instance
    /// and the shared asset on disk is never touched.
    /// </summary>
    public void SetTextureColor(Color color, float duration = 0f)
    {
        foreach (Renderer rend in GetMeshRenderers())
        {
            Material mat = rend.material;
            if (!mat || !mat.HasProperty(TextureColorId)) continue;

            // Remember how this material looked before the first recolor (see ResetTextureColor).
            if (!_originalColors.ContainsKey(mat)) _originalColors[mat] = mat.GetColor(TextureColorId);

            // Drop any recolor still running on this material so the new one starts clean.
            if (_colorTweens.TryGetValue(mat, out Tween running) && running.isAlive) running.Stop();

            if (duration <= 0f)
            {
                mat.SetColor(TextureColorId, color);
                _colorTweens.Remove(mat);
                continue;
            }

            _colorTweens[mat] = Tween.Custom(mat, mat.GetColor(TextureColorId), color, duration,
                (Material m, Color c) => m.SetColor(TextureColorId, c));
        }
    }

    /// <summary>
    /// Puts every mesh material's TextureColor back to what it was before the achievement recolor.
    /// Used when sin achievements are reset (see GamePlusManager.ClearAchievements) so a reset made
    /// during play mode is visible right away. No-op if this tracker was never recolored.
    /// </summary>
    public void ResetTextureColor()
    {
        foreach (KeyValuePair<Material, Color> kv in _originalColors)
        {
            if (!kv.Key) continue;

            if (_colorTweens.TryGetValue(kv.Key, out Tween running) && running.isAlive) running.Stop();
            kv.Key.SetColor(TextureColorId, kv.Value);
        }

        _colorTweens.Clear();
        _originalColors.Clear();
    }

    // Every renderer belonging to the tracker's card meshes (front/back and anything below them).
    private List<Renderer> GetMeshRenderers()
    {
        List<Renderer> renderers = new List<Renderer>();

        foreach (GameObject go in new[] { front, back })
        {
            if (!go) continue;
            foreach (Renderer rend in go.GetComponentsInChildren<Renderer>(true))
                if (rend && !renderers.Contains(rend)) renderers.Add(rend);
        }

        return renderers;
    }

    // Builds one sprite-tag frame per suit sprite frame (id 1..CP.SuitFrameCount) and cycles
    // them on suitTmp, mirroring how Card/CardDataBase flip-book the suit sprites. The frames
    // loop while the ChangingTextAnimationn's own loop flag (set on the component) is on.
    private void PlaySuitFlipbook()
    {
        if (suitAnim)
        {
            List<string> frames = new List<string>();
            for (int id = 1; id <= CP.SuitFrameCount; id++)
                frames.Add(CP.SuitTag(targetSuit, id));

            suitAnim.frames = frames;
            StartCoroutine(suitAnim.Play());
        }
        else if (suitTmp)
        {
            // No flip-book component: just show the first sprite frame.
            suitTmp.text = CP.SuitTag(targetSuit, 1);
        }
    }
}
