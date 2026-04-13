using System.Collections.Generic;
using UnityEngine;

namespace AURAID.Training
{
    /// <summary>
    /// Controls the individual slides under the TrainingScreens root
    /// (e.g. What is CPR, CPR Start, ABCD Approach, etc.).
    /// Ensures only one child screen is active at a time and provides
    /// simple Next/Previous navigation methods to wire to buttons.
    /// </summary>
    public class TrainingScreensController : MonoBehaviour
    {
        [Tooltip("Optional explicit list of screens. If empty, all direct children of this GameObject will be used.")]
        [SerializeField] List<GameObject> screens = new List<GameObject>();

        [Header("After last slide")]
        [Tooltip("When user clicks Next on the last slide, this flow manager is used to go to QnA. Assign the TrainingFlowManager in the scene.")]
        [SerializeField] TrainingFlowManager flowManager;

        int _currentIndex;

        void Awake()
        {
            if (screens == null || screens.Count == 0)
            {
                screens = new List<GameObject>();
                for (int i = 0; i < transform.childCount; i++)
                {
                    var child = transform.GetChild(i).gameObject;
                    screens.Add(child);
                }
            }
        }

        void OnEnable()
        {
            // When the TrainingScreens root becomes active, default to the first slide.
            ShowFirst();
        }

        public void ShowFirst()
        {
            if (screens == null || screens.Count == 0) return;
            _currentIndex = 0;
            ShowCurrent();
        }

        public void ShowNext()
        {
            if (screens == null || screens.Count == 0) return;

            // On the last slide, Next goes to QnA (before practical) instead of staying here.
            if (_currentIndex >= screens.Count - 1)
            {
                if (flowManager == null)
                    flowManager = FindObjectOfType<TrainingFlowManager>();
                if (flowManager != null)
                    flowManager.ShowQnABefore();
                return;
            }

            _currentIndex = Mathf.Clamp(_currentIndex + 1, 0, screens.Count - 1);
            ShowCurrent();
        }

        public void ShowPrevious()
        {
            if (screens == null || screens.Count == 0) return;
            _currentIndex = Mathf.Clamp(_currentIndex - 1, 0, screens.Count - 1);
            ShowCurrent();
        }

        void ShowCurrent()
        {
            for (int i = 0; i < screens.Count; i++)
            {
                if (screens[i] == null) continue;
                screens[i].SetActive(i == _currentIndex);
            }
        }
    }
}

