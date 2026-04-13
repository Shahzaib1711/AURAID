using UnityEngine;
using UnityEngine.EventSystems;
#if UNITY_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace AURAID.Training
{
    /// <summary>
    /// Ensures an EventSystem exists when this scene runs (e.g. TrainingMode played alone).
    /// Does nothing if one is already present (e.g. when loaded additively after MainMenu).
    /// Also assigns Camera.main to World Space canvases in this scene so raycasts work when testing alone.
    /// Add this to a root GameObject in the TrainingMode scene.
    /// </summary>
    public class EnsureEventSystemWhenNeeded : MonoBehaviour
    {
        void Awake()
        {
            if (EventSystem.current == null)
                CreateEventSystem();

            AssignCameraToWorldSpaceCanvases();
        }

        void CreateEventSystem()
        {
            var go = new GameObject("EventSystem (Training)");
            go.AddComponent<EventSystem>();

#if UNITY_INPUT_SYSTEM
            if (UnityEngine.InputSystem.InputSystem.settings != null)
                go.AddComponent<InputSystemUIInputModule>();
            else
                go.AddComponent<StandaloneInputModule>();
#else
            go.AddComponent<StandaloneInputModule>();
#endif
        }

        [Tooltip("World scale for Training canvases when scene runs alone. Slightly larger (e.g. 0.01) looks sharper.")]
        [SerializeField] float worldSpaceCanvasScale = 0.01f;

        void AssignCameraToWorldSpaceCanvases()
        {
            var cam = Camera.main;
            if (cam == null) return;

            foreach (var canvas in FindObjectsOfType<Canvas>(true))
            {
                if (canvas.renderMode != RenderMode.WorldSpace) continue;
                if (canvas.worldCamera == null)
                    canvas.worldCamera = cam;
                var rt = canvas.transform as RectTransform;
                if (rt != null && rt.localScale.x < 0.01f && rt.localScale.y < 0.01f)
                    rt.localScale = Vector3.one * Mathf.Max(0.006f, worldSpaceCanvasScale);
            }
        }
    }
}
