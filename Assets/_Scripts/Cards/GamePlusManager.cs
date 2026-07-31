using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

/// <summary>
/// Remembers which sins (suits) the player has already "achieved" — i.e. whose sin cutscene has
/// actually been played — and persists that to a tamper-protected JSON file so the progress
/// carries over into the next play sessions.
///
/// It also owns the visual side of that progress: an achieved suit's <see cref="SuitTracker"/>
/// has the <c>TextureColor</c> shader property of its front/back meshes recolored to
/// <see cref="achivedCardColor"/>. The recolor happens once, at the moment the sin is achieved
/// (played together with the tracker's count-change animation), and is re-applied to every
/// already-achieved tracker at the start of the scene.
///
/// Storage: <see cref="Application.persistentDataPath"/>/sin_achievements.json. The suit list is
/// base64-encoded and signed with a SHA-256 hash, so hand-editing the file invalidates it and the
/// save is treated as missing rather than trusted. Suits are stored by enum *name*, so reordering
/// <see cref="CP.Suit"/> never corrupts an existing save.
///
/// This is deliberately separate from <see cref="SeenCutscenesSave"/> / TableManager.playedCutScenes,
/// which decides whether a sin's dialogue was already heard. This manager only tracks the card
/// achievement colouring.
/// </summary>
public class GamePlusManager : MonoBehaviour
{
    public static GamePlusManager Instance;

    [Header("Achievement Look")]
    [Tooltip("Color written into the TextureColor material property of an achieved suit tracker's " +
             "front and back meshes.")]
    public Color achivedCardColor = Color.white;

    [Tooltip("How long the recolor takes when a sin is achieved. Set it to match the tracker's " +
             "count-change animation so the two read as one beat. 0 = snap instantly.")]
    [SerializeField] private float colorFadeDuration = 0.5f;

    [Header("Startup")]
    [Tooltip("If on, every already-achieved suit's tracker is colored (instantly) when the scene starts.")]
    [SerializeField] private bool colorTrackersOnStart = true;

    [Tooltip("If off, the save file on disk is ignored, so every sin counts as unachieved (useful while testing).")]
    [SerializeField] private bool loadFromDisk = true;

    [Header("State")]
    [Tooltip("Suits whose cutscene has already been played at least once. Loaded from disk on Awake " +
             "and written back every time a new sin is achieved.")]
    public List<CP.Suit> achievedSins = new List<CP.Suit>();

    private const string FileName = "sin_achievements.json";
    // Mixed into the signature so the file can't be re-signed by just hashing the payload.
    private const string Signature_Salt = "GMTK2026::SinAchivements::v1";

    /// <summary>Full path of the save file on disk.</summary>
    public static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

    private void Awake()
    {
        h.CreateStaticInstance(this, ref Instance);

        LoadAchievedSins();
    }

    private void Start()
    {
        if (colorTrackersOnStart) StartCoroutine(ColorAchievedTrackersWhenReady());
    }

    // TableManager builds its suitTrackers dictionary in its own Start, so waiting a frame keeps
    // this independent of script execution order.
    private IEnumerator ColorAchievedTrackersWhenReady()
    {
        yield return null;
        ApplyAchievedColorToAll();
    }

    // ---------------------------------------------------------------- achievements

    /// <summary>True if this suit's cutscene has already been played in this or an earlier session.</summary>
    public bool IsAchieved(CP.Suit suit) => achievedSins.Contains(suit);

    /// <summary>
    /// Records <paramref name="suit"/> as achieved and writes the whole list to disk immediately, so
    /// the progress survives a crash as well as a clean quit.
    /// Returns true only when this was the FIRST time the suit was achieved — callers use that to
    /// decide whether the "new achievement" visuals should play.
    /// </summary>
    public bool TryAchieve(CP.Suit suit)
    {
        if (achievedSins.Contains(suit)) return false;

        achievedSins.Add(suit);
        Save();
        h.Out("SinAchivementManager: achieved sin", suit);
        return true;
    }

    // ---------------------------------------------------------------- colouring

    /// <summary>
    /// Recolors one tracker's front/back meshes to <see cref="achivedCardColor"/>.
    /// Pass <paramref name="instant"/> = true to snap instead of fading (used at scene start).
    /// </summary>
    public void ApplyAchievedColor(SuitTracker tracker, bool instant = false)
    {
        if (!tracker) return;
        tracker.SetTextureColor(achivedCardColor, instant ? 0f : colorFadeDuration);
    }

    /// <summary>
    /// Colors the tracker of every suit in <see cref="achievedSins"/>. Called on scene start so
    /// previously achieved sins come back already marked. Always instant.
    /// </summary>
    public void ApplyAchievedColorToAll()
    {
        if (TableManager.Instance == null || TableManager.Instance.suitTrackers == null) return;

        foreach (CP.Suit suit in achievedSins)
        {
            if (TableManager.Instance.suitTrackers.TryGetValue(suit, out SuitTracker tracker) && tracker)
                tracker.SetTextureColor(achivedCardColor, 0f);
        }
    }

    // ---------------------------------------------------------------- persistence

    // JsonUtility can't serialize a bare List at the top level, so it goes through this wrapper.
    [Serializable]
    private class SaveData
    {
        public List<string> achievedSins = new List<string>();
    }

    // What actually lands on disk: the payload is base64 of the SaveData json, signed so a
    // hand-edited file is detectable.
    [Serializable]
    private class ProtectedFile
    {
        public string payload;
        public string signature;
    }

    // Merges whatever is on disk into achievedSins. Anything already set in the inspector is kept,
    // so authored "pretend this was achieved" entries still work.
    private void LoadAchievedSins()
    {
        if (!loadFromDisk) return;

        foreach (CP.Suit suit in Load())
        {
            if (!achievedSins.Contains(suit))
                achievedSins.Add(suit);
        }

        h.Out("SinAchivementManager: loaded achieved sins", achievedSins);
    }

    /// <summary>
    /// Reads the saved suits. Returns an empty list when there is no save yet, or when the file is
    /// unreadable / corrupt / fails its signature check — a missing save is a normal first launch,
    /// and a tampered one is deliberately treated the same way rather than trusted.
    /// </summary>
    public static List<CP.Suit> Load()
    {
        List<CP.Suit> result = new List<CP.Suit>();

        try
        {
            if (!File.Exists(FilePath)) return result;

            string raw = File.ReadAllText(FilePath);
            if (string.IsNullOrWhiteSpace(raw)) return result;

            ProtectedFile file = JsonUtility.FromJson<ProtectedFile>(raw);
            if (file == null || string.IsNullOrEmpty(file.payload)) return result;

            if (file.signature != Sign(file.payload))
            {
                h.Out("SinAchivementManager: save signature mismatch — ignoring the file.");
                return result;
            }

            string json = Encoding.UTF8.GetString(Convert.FromBase64String(file.payload));
            SaveData data = JsonUtility.FromJson<SaveData>(json);
            if (data == null || data.achievedSins == null) return result;

            foreach (string rawSuit in data.achievedSins)
            {
                if (string.IsNullOrEmpty(rawSuit)) continue;

                // Names that no longer exist in the enum are ignored rather than throwing.
                if (!Enum.TryParse(rawSuit, true, out CP.Suit suit)) continue;
                if (result.Contains(suit)) continue;

                result.Add(suit);
            }
        }
        catch (Exception e)
        {
            h.Out("SinAchivementManager: failed to load", e.Message);
            return new List<CP.Suit>();
        }

        return result;
    }

    /// <summary>Overwrites the save file with the current <see cref="achievedSins"/>.</summary>
    public void Save() => Save(achievedSins);

    /// <summary>Overwrites the save file with the given set of suits.</summary>
    public static void Save(IEnumerable<CP.Suit> suits)
    {
        SaveData data = new SaveData();

        if (suits != null)
        {
            foreach (CP.Suit suit in suits)
            {
                string name = suit.ToString();
                if (!data.achievedSins.Contains(name))
                    data.achievedSins.Add(name);
            }
        }

        try
        {
            string directory = Application.persistentDataPath;
            if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);

            string payload = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonUtility.ToJson(data)));
            ProtectedFile file = new ProtectedFile { payload = payload, signature = Sign(payload) };

            File.WriteAllText(FilePath, JsonUtility.ToJson(file, true));
        }
        catch (Exception e)
        {
            h.Out("SinAchivementManager: failed to save", e.Message);
        }
    }

    // SHA-256 over payload + salt, hex encoded.
    private static string Sign(string payload)
    {
        using (SHA256 sha = SHA256.Create())
        {
            byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(payload + Signature_Salt));

            StringBuilder sb = new StringBuilder(hash.Length * 2);
            foreach (byte b in hash) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
    }

    /// <summary>Deletes the save file and forgets every achievement, so all sins count as new again.</summary>
    [ContextMenu("Clear Sin Achievements Save")]
    public void ClearAchievements()
    {
        achievedSins.Clear();

        try
        {
            if (File.Exists(FilePath)) File.Delete(FilePath);
        }
        catch (Exception e)
        {
            h.Out("SinAchivementManager: failed to clear", e.Message);
        }

        h.Out("SinAchivementManager: cleared achieved sins");
    }
}
