using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Persists which sin (suit) cutscenes the player has already reached, so they stay
/// "seen" across separate launches of the game.
///
/// Storage: a small JSON file in <see cref="Application.persistentDataPath"/>
/// (%AppData%/LocalLow/&lt;Company&gt;/&lt;Product&gt;/seen_cutscenes.json on Windows).
/// Suits are stored by their enum *name*, not their numeric value, so reordering
/// <see cref="CP.Suit"/> later never corrupts an existing save. Unknown names in an
/// older save are simply skipped.
///
/// This class only remembers the suits — it deliberately does nothing else.
/// </summary>
public static class SeenCutscenesSave
{
    private const string FileName = "seen_cutscenes.json";

    // JsonUtility can't serialize a bare List at the top level, so it goes through this wrapper.
    [Serializable]
    private class SaveData
    {
        public List<string> seenSuits = new List<string>();
    }

    /// <summary>Full path of the save file on disk.</summary>
    public static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

    /// <summary>True if a save file already exists.</summary>
    public static bool Exists() => File.Exists(FilePath);

    /// <summary>
    /// Reads the saved suits. Returns an empty list when there is no save yet or the
    /// file is unreadable/corrupt — a missing save is a normal first launch, not an error.
    /// </summary>
    public static List<CP.Suit> Load()
    {
        List<CP.Suit> result = new List<CP.Suit>();

        try
        {
            if (!File.Exists(FilePath)) return result;

            string json = File.ReadAllText(FilePath);
            if (string.IsNullOrWhiteSpace(json)) return result;

            SaveData data = JsonUtility.FromJson<SaveData>(json);
            if (data == null || data.seenSuits == null) return result;

            foreach (string rawSuit in data.seenSuits)
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
            h.Out("SeenCutscenesSave: failed to load", e.Message);
            return new List<CP.Suit>();
        }

        return result;
    }

    /// <summary>Overwrites the save file with the given set of suits.</summary>
    public static void Save(IEnumerable<CP.Suit> suits)
    {
        SaveData data = new SaveData();

        if (suits != null)
        {
            foreach (CP.Suit suit in suits)
            {
                string name = suit.ToString();
                if (!data.seenSuits.Contains(name))
                    data.seenSuits.Add(name);
            }
        }

        try
        {
            string directory = Application.persistentDataPath;
            if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);

            File.WriteAllText(FilePath, JsonUtility.ToJson(data, true));
        }
        catch (Exception e)
        {
            h.Out("SeenCutscenesSave: failed to save", e.Message);
        }
    }

    /// <summary>Deletes the save file, so every cutscene counts as unseen again.</summary>
    public static void Clear()
    {
        try
        {
            if (File.Exists(FilePath)) File.Delete(FilePath);
        }
        catch (Exception e)
        {
            h.Out("SeenCutscenesSave: failed to clear", e.Message);
        }
    }
}
