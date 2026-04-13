using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace AURAID.Editor
{
    /// <summary>Disables "Unity Editor Debugger" on build so the Continue/Stop/Break popup does not appear on device.</summary>
    public class DisableEditorDebuggerOnBuild : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (EditorUserBuildSettings.allowDebugging)
            {
                EditorUserBuildSettings.allowDebugging = false;
                UnityEngine.Debug.Log("AURAID: Disabled Editor Debugger for this build (allowDebugging = false).");
            }
        }
    }
}
