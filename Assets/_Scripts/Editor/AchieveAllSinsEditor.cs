using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Editor menu item that marks EVERY sin as achieved — the mirror image of
/// <see cref="ResetSinAchivementsEditor"/>. Use it to reach the "all sins collected" state (secret
/// ending, fully recolored suit trackers) without having to play through every sin cutscene.
///
/// It writes the full suit list into the tamper-signed save file owned by <see cref="GamePlusManager"/>
/// AND fills the achievedSins list on any GamePlusManager in the open scenes (including inactive
/// ones), so the state holds even when a manager has loadFromDisk turned off.
/// Works both in and out of play mode; in play mode it also recolors the suit trackers right away.
/// </summary>
public static class AchieveAllSinsEditor
{
    private const string MenuPath = "Tools/Sin Achievements/Achieve All Sins";

    [MenuItem(MenuPath)]
    private static void AchieveAllSins()
    {
        // Outside play mode TableManager isn't up, so this falls back to every CP.Suit value.
        List<CP.Suit> suits = GamePlusManager.SuitsWithCutscenes();

        bool confirmed = EditorUtility.DisplayDialog(
            "Achieve All Sins",
            $"Set all {suits.Count} sins as ACHIEVED?\n\n" +
            "This overwrites the save file:\n" + GamePlusManager.FilePath + "\n\n" +
            "and fills the achievedSins list on every GamePlusManager in the open scenes.",
            "Achieve All",
            "Cancel");

        if (!confirmed) return;

        // 1. The save on disk — the thing that actually carries achievements between sessions.
        GamePlusManager.Save(suits);

        // 2. Any manager in the open scenes, so the state applies without a reload (and even when
        //    loadFromDisk is off).
        int updated = 0;
        foreach (GamePlusManager manager in Object.FindObjectsByType<GamePlusManager>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!manager) continue;

            if (Application.isPlaying)
            {
                // TryAchieve also re-saves; harmless, and keeps the runtime path identical to gameplay.
                foreach (CP.Suit suit in suits) manager.TryAchieve(suit);

                // Recolor now, so the change is visible without leaving play mode.
                manager.ApplyAchievedColorToAll();
            }
            else
            {
                Undo.RecordObject(manager, "Achieve All Sins");

                foreach (CP.Suit suit in suits)
                    if (!manager.achievedSins.Contains(suit))
                        manager.achievedSins.Add(suit);

                EditorUtility.SetDirty(manager);
                PrefabUtility.RecordPrefabInstancePropertyModifications(manager);
            }

            updated++;
        }

        if (!Application.isPlaying && updated > 0)
            EditorSceneManager.MarkAllScenesDirty();

        Debug.Log($"All sins achieved — {suits.Count} suit(s) saved and {updated} GamePlusManager(s) updated.");
    }
}
