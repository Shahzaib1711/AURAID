using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace AURAID.Editor
{
    /// <summary>
    /// Stops Meta XR Simulator from auto-running when you hit Play.
    /// Simulator is "active" when env var XR_SELECTED_RUNTIME_JSON points at the simulator JSON.
    /// We clear/restore those vars before Play so OpenXR doesn't use the simulator.
    /// To use the simulator again: Meta menu > Meta XR Simulator > Activate (or use the Play toolbar).
    /// </summary>
    [InitializeOnLoad]
    public static class DisableMetaXRSimulatorOnPlay
    {
        const string PrefKey = "AURAID.DisableMetaXRSimulatorOnPlay";
        const string XR_RUNTIME_JSON = "XR_RUNTIME_JSON";
        const string XR_RUNTIME_JSON_PREV = "XR_RUNTIME_JSON_PREV";
        const string XR_SELECTED_RUNTIME_JSON = "XR_SELECTED_RUNTIME_JSON";
        const string XR_SELECTED_RUNTIME_JSON_PREV = "XR_SELECTED_RUNTIME_JSON_PREV";
        const string META_XRSIM_CONFIG_JSON = "META_XRSIM_CONFIG_JSON";
        const string META_XRSIM_CONFIG_JSON_PREV = "META_XRSIM_CONFIG_JSON_PREV";

        public static bool DisableSimulatorOnPlay
        {
            get => EditorPrefs.GetBool(PrefKey, true);
            set => EditorPrefs.SetBool(PrefKey, value);
        }

        static DisableMetaXRSimulatorOnPlay()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.ExitingEditMode) return;
            if (!DisableSimulatorOnPlay) return;

            string selected = System.Environment.GetEnvironmentVariable(XR_SELECTED_RUNTIME_JSON);
            if (string.IsNullOrEmpty(selected)) return;
            if (!selected.Contains("meta_openxr_simulator") && !selected.Contains("MetaXRSimulator"))
                return;

            string prevSelected = System.Environment.GetEnvironmentVariable(XR_SELECTED_RUNTIME_JSON_PREV);
            string prevRuntime = System.Environment.GetEnvironmentVariable(XR_RUNTIME_JSON_PREV);
            string prevConfig = System.Environment.GetEnvironmentVariable(META_XRSIM_CONFIG_JSON_PREV);

            if (string.IsNullOrEmpty(prevSelected)) prevSelected = "";
            if (string.IsNullOrEmpty(prevRuntime)) prevRuntime = "";
            if (string.IsNullOrEmpty(prevConfig)) prevConfig = "";

            System.Environment.SetEnvironmentVariable(XR_SELECTED_RUNTIME_JSON, prevSelected);
            System.Environment.SetEnvironmentVariable(XR_RUNTIME_JSON, prevRuntime);
            System.Environment.SetEnvironmentVariable(META_XRSIM_CONFIG_JSON, prevConfig);

            Debug.Log("[AURAID] Meta XR Simulator disabled for this Play session. Use Meta > Meta XR Simulator > Activate to enable.");
        }

        [MenuItem("AURAID/Disable Meta XR Simulator on Play", false, 200)]
        static void ToggleDisable()
        {
            DisableSimulatorOnPlay = !DisableSimulatorOnPlay;
            Menu.SetChecked("AURAID/Disable Meta XR Simulator on Play", DisableSimulatorOnPlay);
        }

        [MenuItem("AURAID/Disable Meta XR Simulator on Play", true)]
        static bool ToggleDisableValidate()
        {
            Menu.SetChecked("AURAID/Disable Meta XR Simulator on Play", DisableSimulatorOnPlay);
            return true;
        }
    }
}
