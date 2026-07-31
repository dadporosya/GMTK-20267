using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Editor menu item that wipes the sin-achievement progress owned by <see cref="GamePlusManager"/>,
/// so every sin counts as not achieved again — the next time each sin's cutscene plays it will be
/// treated as the first time and its suit tracker will recolor to achivedCardColor.
///
/// It deletes the save file in <see cref="Application.persistentDataPath"/> AND clears the
/// achievedSins list on any GamePlusManager in the open scenes (including inactive ones), because a
/// list authored in the inspector would otherwise immediately re-mark those sins as achieved.
/// Works both in and out of play mode; in play mode it also puts the suit trackers back to their
/// pre-achievement color right away.
/// </summary>
public static class ResetSinAchivementsEditor
{
    private const string MenuPath = "Tools/Sin Achievements/Reset Sin Achievements";

    [MenuItem(MenuPath)]
    private static void ResetSinAchivements()
    {
        bool confirmed = EditorUtility.DisplayDialog(
            "Reset Sin Achievements",
            "Set every sin as NOT achieved?\n\n" +
            "This deletes the save file:\n" + GamePlusManager.FilePath + "\n\n" +
            "and clears the achievedSins list on every GamePlusManager in the open scenes.",
            "Reset",
            "Cancel");

        if (!confirmed) return;

        // 1. The save on disk — the thing that actually carries achievements between sessions.
        GamePlusManager.ClearSaveFile();

        // 2. Any manager in the open scenes, so an inspector-authored list doesn't restore them.
        int cleared = 0;
        foreach (GamePlusManager manager in Object.FindObjectsByType<GamePlusManager>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!manager) continue;

            if (Application.isPlaying)
            {
                // Also reverts the trackers' color, so the reset is visible without leaving play mode.
                manager.ClearAchievements();
            }
            else
            {
                Undo.RecordObject(manager, "Reset Sin Achievements");
                manager.achievedSins.Clear();
                EditorUtility.SetDirty(manager);
                PrefabUtility.RecordPrefabInstancePropertyModifications(manager);
            }

            cleared++;
        }

        if (!Application.isPlaying && cleared > 0)
            EditorSceneManager.MarkAllScenesDirty();

        Debug.Log($"Sin achievements reset — save file deleted and {cleared} GamePlusManager(s) cleared.");
    }
}
