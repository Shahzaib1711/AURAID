using UnityEngine;
using UnityEngine.EventSystems;

namespace AURAID.UI
{
    /// <summary>
    /// Ensures there is always exactly one enabled EventSystem across all loaded scenes.
    /// Attach this to the main EventSystem (for example, the one under MR Interaction Setup).
    /// It will keep that EventSystem active and disable any others it finds at startup.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class SingleEventSystemEnforcer : MonoBehaviour
    {
        void Awake()
        {
            var all = FindObjectsOfType<EventSystem>();
            if (all == null || all.Length <= 1)
                return;

            // Prefer to keep the EventSystem on this GameObject.
            EventSystem keeper = null;

            foreach (var es in all)
            {
                if (es == null)
                    continue;

                if (es.gameObject == gameObject)
                {
                    keeper = es;
                    break;
                }
            }

            // If none found on this GameObject, keep the first active one.
            if (keeper == null)
            {
                foreach (var es in all)
                {
                    if (es != null && es.enabled && es.gameObject.activeInHierarchy)
                    {
                        keeper = es;
                        break;
                    }
                }
            }

            // Fallback to first in list.
            if (keeper == null && all.Length > 0)
                keeper = all[0];

            foreach (var es in all)
            {
                if (es == null)
                    continue;

                bool shouldKeep = es == keeper;
                es.gameObject.SetActive(shouldKeep);
            }
        }
    }
}

