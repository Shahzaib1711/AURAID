using UnityEngine;
using UnityEngine.XR;

namespace AURAID.UI
{
    /// <summary>
    /// Disables XR-only components when not running in an active XR session (e.g. Editor without simulator).
    /// Stops "Hand Tracking Subsystem not found" and "GetStereoProjectionMatrix invalid" spam.
    /// Add to a GameObject that loads with the main menu (e.g. same scene or earlier).
    /// </summary>
    public class XREditorGuard : MonoBehaviour
    {
        [Tooltip("When true, XR-only components are disabled if XR device is not active (e.g. Editor play without headset).")]
        public bool disableXRComponentsWhenInactive = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void OnSceneLoaded()
        {
            var guard = FindObjectOfType<XREditorGuard>();
            if (guard != null)
                guard.Apply();
            else
                ApplyStatic();
        }

        void Awake()
        {
            if (disableXRComponentsWhenInactive)
                Apply();
        }

        void Apply()
        {
            if (!NeedsDisabling()) return;

            int disabled = 0;
            foreach (var mb in FindObjectsOfType<MonoBehaviour>())
            {
                if (mb == null) continue;
                string name = mb.GetType().Name;
                if (name == "OVROverlayCanvas" || name == "XRInputModalityManager")
                {
                    if (mb.enabled)
                    {
                        mb.enabled = false;
                        disabled++;
                    }
                }
            }
            if (disabled > 0)
                Debug.Log($"[AURAID] XR not active: disabled {disabled} XR-only component(s) to avoid errors. Enable Meta XR Simulator or run on device for full XR.");
        }

        static void ApplyStatic()
        {
            if (XRSettings.isDeviceActive) return;
            foreach (var mb in FindObjectsOfType<MonoBehaviour>())
            {
                if (mb == null) continue;
                string name = mb.GetType().Name;
                if (name == "OVROverlayCanvas" || name == "XRInputModalityManager")
                    mb.enabled = false;
            }
        }

        static bool NeedsDisabling()
        {
#if UNITY_EDITOR
            return !XRSettings.isDeviceActive;
#else
            return false;
#endif
        }
    }
}
