using System.Collections.Generic;
using UnityEngine;

namespace AURAID.Training
{
    /// <summary>
    /// Same Next/Previous slide logic as TrainingScreens: one panel visible at a time under QuizScreens.
    /// Typical setup: one intro panel (e.g. &quot;Quiz Time&quot;) then Question 1, 2, … — <see cref="ShowFirst"/> opens the intro;
    /// wire <see cref="BeginQuizQuestions"/> to the Start button. For one intro before questions, keep the default first-question index of <c>1</c>.
    /// Next on the last question goes to Report. Attach to QuizScreens.
    /// </summary>
    public class QuizScreensController : MonoBehaviour
    {
        [Tooltip("Optional explicit list of panels in order: intro first (if any), then Question 1, 2, ... If empty, all direct children top-to-bottom are used.")]
        [SerializeField] List<GameObject> screens = new List<GameObject>();

        [Header("Quiz intro (one screen, e.g. Quiz Time)")]
        [Tooltip("Index of the first question panel. With exactly ONE intro before the questions, leave at 1 (panel 0 = intro, panel 1 = Question 1). Only change if you add more intro-only panels before Question 1.")]
        [SerializeField, Min(0)] int firstQuestionScreenIndex = 1;

        [Header("After last question")]
        [Tooltip("When user clicks Next on the last question, go to Report. Assign TrainingFlowManager.")]
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
            ShowFirst();
        }

        public void ShowFirst()
        {
            if (QuizManager.Instance != null)
                QuizManager.Instance.ResetQuiz();
            if (screens == null || screens.Count == 0) return;
            _currentIndex = 0;
            ShowCurrent();
        }

        /// <summary>
        /// Wire your intro <c>Start</c> (or Begin quiz) button here. Jumps from the &quot;Quiz Time&quot; (or other intro) panel
        /// to the first question panel (<see cref="firstQuestionScreenIndex"/>).
        /// </summary>
        public void BeginQuizQuestions()
        {
            if (screens == null || screens.Count == 0) return;
            _currentIndex = Mathf.Clamp(firstQuestionScreenIndex, 0, screens.Count - 1);
            ShowCurrent();
        }

        public void ShowNext()
        {
            if (screens == null || screens.Count == 0) return;

            if (_currentIndex >= screens.Count - 1)
            {
                if (QuizManager.Instance != null && TrainingManager.Instance != null)
                {
                    var (correct, total) = QuizManager.Instance.GetScore();
                    TrainingManager.Instance.SaveQuizScore(correct, total);
                }
                if (flowManager != null)
                    flowManager.ShowReport();
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
