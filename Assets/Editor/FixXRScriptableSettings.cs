using System.IO;
using UnityEditor;
using UnityEngine;

namespace AURAID.Editor
{
    /// <summary>Backs up and moves XR ScriptableSettings assets that can cause InvalidCastException so Unity can recreate them. Restart Unity after running.</summary>
    public static class FixXRScriptableSettings
    {
        static readonly string[] AssetsToMove =
        {
            "Assets/XRI/Settings/Resources/InteractionLayerSettings.asset",
            "Assets/XRI/Settings/Resources/XRDeviceSimulatorSettings.asset",
            "Assets/XRI/Settings/XRInteractionEditorSettings.asset",
            "Assets/XR/Settings/XRSimulationSettings.asset",
        };

        [MenuItem("Tools/AURAID/Fix XR ScriptableSettings", false, 101)]
        static void Fix()
        {
            string backupDir = "Assets/XRI/Settings/Resources/BackupScriptableSettings";
            if (!Directory.Exists(backupDir))
                Directory.CreateDirectory(backupDir);

            int moved = 0;
            foreach (string path in AssetsToMove)
            {
                if (!File.Exists(path)) continue;
                string fileName = Path.GetFileName(path);
                string backupPath = Path.Combine(backupDir, fileName);
                if (AssetDatabase.MoveAsset(path, backupPath) == "")
                {
                    moved++;
                    Debug.Log($"AURAID: Moved {path} to {backupPath}");
                }
                else
                    Debug.LogWarning($"AURAID: Could not move {path}");
            }

            AssetDatabase.Refresh();
            if (moved > 0)
                EditorUtility.DisplayDialog("AURAID: Fix XR ScriptableSettings",
                    $"{moved} asset(s) moved to BackupScriptableSettings. Restart Unity so they can be recreated.", "OK");
            else
                EditorUtility.DisplayDialog("AURAID: Fix XR ScriptableSettings",
                    "No assets needed moving, or move failed. See Console.", "OK");
        }
    }
}
