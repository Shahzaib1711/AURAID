using System.Collections.Generic;
using UnityEngine;

namespace AURAID.Training
{
    /// <summary>
    /// Same Next/Previous slide logic as TrainingScreensController, but for the QnA Panel
    /// (which is not inside TrainingScreens). Attach to the QnA Panel root.
    /// When user clicks Next on the last slide, goes to Practical (before) or Quiz (after).
    /// The flow sets which exit to use when showing this panel.
    /// </summary>
    public class QnAPanelController : MonoBehaviour
    {
        [Tooltip("Optional explicit list of screens. If empty, all direct children (except Always Visible) will be used.")]
        [SerializeField] List<GameObject> screens = new List<GameObject>();

        [Tooltip("Objects to keep visible alongside the current slide (e.g. avatar). Not treated as screens.")]
        [SerializeField] List<GameObject> alwaysVisible = new List<GameObject>();

        [Tooltip("Required for last-slide Next (Go to Practical or Quiz).")]
        [SerializeField] TrainingFlowManager flowManager;

        int _currentIndex;
        int _lastSlideNextAction; // 0 = GoToPractical, 1 = GoToQuiz

        void Awake()
        {
            if (alwaysVisible != null)
                alwaysVisible.RemoveAll(go => go == null);

            if (screens == null || screens.Count == 0)
            {
                screens = new List<GameObject>();
                for (int i = 0; i < transform.childCount; i++)
                {
                    var child = transform.GetChild(i).gameObject;
                    if (alwaysVisible != null && alwaysVisible.Contains(child))
                        continue;
                    screens.Add(child);
                }
            }
        }

        /// <summary>Call when showing QnA *before* practical. Next on last slide will go to Practical.</summary>
        public void SetLastSlideNextGoToPractical()
        {
            _lastSlideNextAction = 0;
        }

        /// <summary>Call when showing QnA *after* practical. Next on last slide will go to Quiz.</summary>
        public void SetLastSlideNextGoToQuiz()
        {
            _lastSlideNextAction = 1;
        }

        void OnEnable()
        {
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

            if (_currentIndex >= screens.Count - 1)
            {
                if (flowManager != null)
                {
                    if (_lastSlideNextAction == 0)
                        flowManager.ShowPractical();
                    else
                        flowManager.ShowQuiz();
                }
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
            if (alwaysVisible != null)
            {
                foreach (var go in alwaysVisible)
                {
                    if (go != null)
                        go.SetActive(true);
                }
            }
        }
    }
}
