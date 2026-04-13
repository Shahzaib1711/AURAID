using UnityEngine;

namespace AURAID.UI
{
    /// <summary>
    /// Ensures the main camera does not clear to Skybox on Quest so AR passthrough can show.
    /// Add this to any GameObject in the MainMenu (or XR rig) that is active at startup.
    /// </summary>
    public class QuestPassthroughCameraFix : MonoBehaviour
    {
        [Tooltip("Only apply on Android (Quest). Leave true for builds.")]
        public bool onlyOnAndroid = true;

        [Tooltip("Log when fix runs (check device logcat for AURAID).")]
        public bool logWhenApplied = true;

        void Awake()
        {
            if (onlyOnAndroid && Application.platform != RuntimePlatform.Android)
                return;

            var cam = Camera.main;
            if (cam == null)
            {
                // Camera might not be tagged MainCamera yet; try again after a frame
                return;
            }

            ApplyFix(cam);
        }

        void Start()
        {
            if (onlyOnAndroid && Application.platform != RuntimePlatform.Android)
                return;

            var cam = Camera.main;
            if (cam != null)
                ApplyFix(cam);
        }

        void ApplyFix(Camera cam)
        {
            if (cam.clearFlags == CameraClearFlags.Skybox)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0, 0, 0, 0);
                if (logWhenApplied)
                    Debug.Log("AURAID: QuestPassthroughCameraFix applied (Skybox -> SolidColor transparent).");
            }
            else if (cam.clearFlags == CameraClearFlags.SolidColor && cam.backgroundColor.a > 0.01f)
            {
                cam.backgroundColor = new Color(0, 0, 0, 0);
                if (logWhenApplied)
                    Debug.Log("AURAID: QuestPassthroughCameraFix applied (cleared background alpha).");
            }
        }
    }
}
