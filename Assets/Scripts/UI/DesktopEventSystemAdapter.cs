using UnityEngine;
using UnityEngine.EventSystems;

namespace AURAID.UI
{
    /// <summary>
    /// Ensures that, when running in the Editor on desktop, the EventSystem
    /// uses a StandaloneInputModule so TMP_InputField can receive keyboard input.
    /// On device (Android/Quest) it leaves the XR / OVR input modules untouched.
    /// Attach this to the main EventSystem (e.g. under MR Interaction Setup).
    /// </summary>
    [DefaultExecutionOrder(-1001)]
    public class DesktopEventSystemAdapter : MonoBehaviour
    {
        void Awake()
        {
#if UNITY_EDITOR
            // In the Unity editor on desktop, force a StandaloneInputModule so
            // keyboard typing works with TMP_InputField.
            if (Application.platform == RuntimePlatform.WindowsEditor ||
                Application.platform == RuntimePlatform.OSXEditor ||
                Application.platform == RuntimePlatform.LinuxEditor)
            {
                var es = GetComponent<EventSystem>();
                if (es == null) return;

                var standalone = GetComponent<StandaloneInputModule>();
                if (standalone == null)
                    standalone = gameObject.AddComponent<StandaloneInputModule>();

                // Disable other input modules so the EventSystem uses StandaloneInputModule.
                foreach (var module in GetComponents<BaseInputModule>())
                {
                    if (module != null && module != standalone)
                        module.enabled = false;
                }
            }
#endif
        }
    }
}

