using System.Reflection;
using UnityEngine;

namespace AURAID.UI
{
    /// <summary>
    /// Forces the Meta Immersive Debugger overlay off at runtime so it does not appear in Training mode (or any build).
    /// Runs before the debugger's own setup; also disables the manager GameObject if it was already created.
    /// </summary>
    public static class DisableImmersiveDebuggerAtRuntime
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void OnBeforeSceneLoad()
        {
            TryDisableViaSettings();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void OnAfterSceneLoad()
        {
            // If the overlay was still created (e.g. setting not applied in time), hide it
            DisableManagerGameObject();
        }

        static void TryDisableViaSettings()
        {
            try
            {
                var asm = Assembly.Load("Meta.XR.ImmersiveDebugger");
                if (asm == null) return;

                var type = asm.GetType("Meta.XR.ImmersiveDebugger.RuntimeSettings");
                if (type == null) return;

                var instanceProp = type.GetProperty("Instance", BindingFlags.NonPublic | BindingFlags.Static);
                if (instanceProp == null) return;

                object instance = instanceProp.GetValue(null);
                if (instance == null) return;

                var enabledProp = type.GetProperty("ImmersiveDebuggerEnabled", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (enabledProp == null) return;

                enabledProp.SetValue(instance, false);
            }
            catch
            {
                // Ignore; we will fall back to disabling the GameObject
            }
        }

        static void DisableManagerGameObject()
        {
            var go = GameObject.Find("ImmersiveDebuggerManager");
            if (go != null)
            {
                go.SetActive(false);
            }
        }
    }
}
