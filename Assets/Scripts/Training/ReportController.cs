using UnityEngine;
using UnityEngine.UI;
using TMPro;
using AURAID.UI;

namespace AURAID.Training
{
    /// <summary>
    /// Report screen: quiz/confidence display. Save and Exit — end session with report email, go to standby.
    /// Re attempt — same report email for the completed session, new Firestore session for same trainee, then training slides.
    /// </summary>
    public class ReportController : MonoBehaviour
    {
        [Header("Display")]
        [Tooltip("Optional: show quiz score (e.g. '8 / 10').")]
        [SerializeField] TMP_Text quizScoreText;
        [Tooltip("Optional: show confidence before from session start (e.g. 'Confidence Before: 2/5'). Assign the same label as in your UI.")]
        [SerializeField] TMP_Text confidenceBeforeText;
        [Tooltip("Optional: show confidence after (e.g. 'Confidence After: 4/5').")]
        [SerializeField] TMP_Text confidenceAfterText;
        [Tooltip("Optional: show quiz accuracy (e.g. '80%').")]
        [SerializeField] TMP_Text accuracyText;
        [Tooltip("Optional: show session ID.")]
        [SerializeField] TMP_Text sessionIdText;
        [Tooltip("Optional: show improvement (e.g. '+2' or '—').")]
        [SerializeField] TMP_Text improvementText;
        [Tooltip("Optional: overall feedback line.")]
        [SerializeField] TMP_Text overallFeedbackText;
        [Tooltip("Optional: avg compression rate (e.g. '110 / min').")]
        [SerializeField] TMP_Text avgRateText;
        [Tooltip("Optional: avg compression depth (e.g. '5.2 cm').")]
        [SerializeField] TMP_Text avgDepthText;
        [Tooltip("Optional: correct zone % (depth+rate in range).")]
        [SerializeField] TMP_Text correctZoneText;

        [Header("Confidence after (0-5) for EndSession")]
        [Tooltip("If set, value is used for EndSession. Otherwise use confidenceAfterValue.")]
        [SerializeField] Slider confidenceAfterSlider;
        [Tooltip("Used if no slider; 0-5.")]
        [SerializeField] int confidenceAfterValue = 0;

        [Header("Actions")]
        [Tooltip("Save and Exit: end session, write report_data for Cloud Function (PDF emailed to trainee), then return to standby screen.")]
        [SerializeField] Button saveReportButton;
        [Tooltip("Re attempt: email report for this session, start a new session (same trainee), go to training info slides.")]
        [SerializeField] Button reAttemptButton;
        [Tooltip("Stored on the new session document (same as Registration defaultConfidenceBefore).")]
        [SerializeField] int reattemptConfidenceBefore = 0;

        [Header("Optional duplicate")]
        [Tooltip("If assigned, same behavior as Save and Exit (legacy name).")]
        [SerializeField] Button saveAndQuitButton;

        void OnEnable()
        {
            RefreshScoreDisplay();
            if (confidenceAfterSlider != null)
                confidenceAfterSlider.onValueChanged.AddListener(OnConfidenceAfterSliderChanged);
            if (saveReportButton != null)
                saveReportButton.onClick.AddListener(OnSaveReportClicked);
            if (reAttemptButton != null)
                reAttemptButton.onClick.AddListener(OnReAttemptClicked);
            if (saveAndQuitButton != null)
                saveAndQuitButton.onClick.AddListener(OnSaveAndQuitClicked);
        }

        void OnDisable()
        {
            if (confidenceAfterSlider != null)
                confidenceAfterSlider.onValueChanged.RemoveListener(OnConfidenceAfterSliderChanged);
            if (saveReportButton != null)
                saveReportButton.onClick.RemoveListener(OnSaveReportClicked);
            if (reAttemptButton != null)
                reAttemptButton.onClick.RemoveListener(OnReAttemptClicked);
            if (saveAndQuitButton != null)
                saveAndQuitButton.onClick.RemoveListener(OnSaveAndQuitClicked);
        }

        void RefreshScoreDisplay()
        {
            bool isAr = FindObjectOfType<TrainingLanguageController>()?.IsArabic ?? false;
            var font = (isAr && MainMenuLocalization.ArabicFont != null) ? MainMenuLocalization.ArabicFont : null;

            int correct = 0, total = 0;
            if (QuizManager.Instance != null)
                (correct, total) = QuizManager.Instance.GetScore();

            if (quizScoreText != null)
                quizScoreText.text = $"{correct} / {total}";
            if (accuracyText != null)
            {
                int pct = total > 0 ? Mathf.RoundToInt(100f * correct / total) : 0;
                accuracyText.text = isAr ? $"{pct}%" : $"Accuracy: {pct}%";
                if (font != null) { accuracyText.font = font; accuracyText.isRightToLeftText = true; }
            }
            if (sessionIdText != null && TrainingManager.Instance != null)
            {
                string sid = TrainingManager.Instance.CurrentSessionId ?? "";
                sessionIdText.text = isAr ? $"الجلسة: {sid}" : $"Session: {sid}";
                if (font != null) { sessionIdText.font = font; sessionIdText.isRightToLeftText = true; }
            }
            int before = GetConfidenceBefore();
            int after = GetConfidenceAfter();
            int improvement = after - before;
            if (improvementText != null)
            {
                string imp = (improvement >= 0 ? "+" : "") + improvement.ToString();
                improvementText.text = isAr ? $"التحسن: {imp}" : $"Improvement: {imp}";
                if (font != null) { improvementText.font = font; improvementText.isRightToLeftText = true; }
            }

            var analyzer = FindObjectOfType<PerformanceAnalyzer>(true);
            if (avgRateText != null)
            {
                float rate = analyzer != null ? analyzer.AverageRateBpm : 0f;
                avgRateText.text = isAr ? $"متوسط المعدل: {rate:F0} / دقيقة" : $"Avg Rate: {rate:F0} / min";
                if (font != null) { avgRateText.font = font; avgRateText.isRightToLeftText = true; }
            }
            if (avgDepthText != null)
            {
                float depth = analyzer != null ? analyzer.AverageDepthCm : 0f;
                avgDepthText.text = isAr ? $"متوسط العمق: {depth:F1} سم" : $"Avg Depth: {depth:F1} cm";
                if (font != null) { avgDepthText.font = font; avgDepthText.isRightToLeftText = true; }
            }
            if (correctZoneText != null)
            {
                float zone = 0f;
                if (analyzer != null && analyzer.TotalCompressions > 0)
                    zone = (analyzer.CorrectDepthPercent + analyzer.CorrectRatePercent) * 0.5f;
                correctZoneText.text = isAr ? $"المنطقة الصحيحة: {zone:F0}%" : $"Correct Zone: {zone:F0}%";
                if (font != null) { correctZoneText.font = font; correctZoneText.isRightToLeftText = true; }
            }
            if (overallFeedbackText != null)
            {
                string msg = isAr
                    ? "التغذية الراجعة: حافظ على 100–120/دقيقة والعمق بين 5–6 سم."
                    : "Feedback: Maintain 100–120/min and keep depth between 5–6 cm.";
                overallFeedbackText.text = msg;
                if (font != null) { overallFeedbackText.font = font; overallFeedbackText.isRightToLeftText = true; }
            }

            if (confidenceBeforeText != null)
            {
                confidenceBeforeText.text = isAr
                    ? $"الثقة قبل: {before}/{TrainingManager.ConfidenceScaleMax}"
                    : $"Confidence Before: {before}/{TrainingManager.ConfidenceScaleMax}";
                if (font != null)
                {
                    confidenceBeforeText.font = font;
                    confidenceBeforeText.isRightToLeftText = true;
                }
            }

            int conf = after;
            if (confidenceAfterText != null)
            {
                confidenceAfterText.text = isAr
                    ? $"الثقة بعد: {conf}/{TrainingManager.ConfidenceScaleMax}"
                    : $"Confidence After: {conf}/{TrainingManager.ConfidenceScaleMax}";
                if (font != null)
                {
                    confidenceAfterText.font = font;
                    confidenceAfterText.isRightToLeftText = true;
                }
            }
        }

        void OnConfidenceAfterSliderChanged(float _) => RefreshScoreDisplay();

        int GetConfidenceBefore()
        {
            if (TrainingManager.Instance == null) return 0;
            return Mathf.Clamp(TrainingManager.Instance.SessionConfidenceBefore, 0, TrainingManager.ConfidenceScaleMax);
        }

        int GetConfidenceAfter()
        {
            if (confidenceAfterSlider != null)
                return Mathf.Clamp(Mathf.RoundToInt(confidenceAfterSlider.value), 0, TrainingManager.ConfidenceScaleMax);
            return Mathf.Clamp(confidenceAfterValue, 0, TrainingManager.ConfidenceScaleMax);
        }

        /// <summary>
        /// Builds the full training report, ends the session, and writes report_data to Firestore.
        /// A Cloud Function will generate a PDF and email it to the trainee.
        /// </summary>
        void OnSaveReportClicked()
        {
            if (TrainingManager.Instance == null) return;

            int correct = 0, total = 0;
            if (QuizManager.Instance != null)
                (correct, total) = QuizManager.Instance.GetScore();
            int confidenceAfter = GetConfidenceAfter();

            TrainingReportData report = TrainingReportBuilder.Build();
            var reportMap = report.ToFirestoreMap();
            int beforeSave = GetConfidenceBefore();
            reportMap["confidence_before"] = beforeSave;
            reportMap["confidence_after"] = confidenceAfter;
            reportMap["confidence_improvement"] = confidenceAfter - beforeSave;

            TrainingManager.Instance.EndSession(
                confidenceAfter,
                correct,
                total,
                reportData: reportMap,
                onSuccess: () =>
                {
                    Debug.Log($"AURAID: Session ended. Report will be sent to {report.trainee_email}");
                    if (overallFeedbackText != null)
                        overallFeedbackText.text = $"Report will be sent to {report.trainee_email}";
                    ReturnToStandbyAfterReportSaved();
                },
                onError: err => Debug.LogWarning("AURAID Save Report: " + err)
            );
        }

        /// <summary>
        /// Same report + email as Save and Exit for the current session, then a new session for the same trainee and back to training slides.
        /// </summary>
        void OnReAttemptClicked()
        {
            if (TrainingManager.Instance == null) return;

            int correct = 0, total = 0;
            if (QuizManager.Instance != null)
                (correct, total) = QuizManager.Instance.GetScore();
            int confidenceAfter = GetConfidenceAfter();

            TrainingReportData report = TrainingReportBuilder.Build();
            var reportMap = report.ToFirestoreMap();
            int beforeRe = GetConfidenceBefore();
            reportMap["confidence_before"] = beforeRe;
            reportMap["confidence_after"] = confidenceAfter;
            reportMap["confidence_improvement"] = confidenceAfter - beforeRe;

            TrainingManager.Instance.EndSession(
                confidenceAfter,
                correct,
                total,
                reportData: reportMap,
                onSuccess: () =>
                {
                    Debug.Log($"AURAID: Session completed; report will be emailed to {report.trainee_email}. Starting new session for reattempt.");
                    TrainingManager.Instance.CreateSession(
                        reattemptConfidenceBefore,
                        onSuccess: BeginReattemptTrainingFlow,
                        onError: err => Debug.LogWarning("AURAID Re attempt CreateSession: " + err));
                },
                onError: err => Debug.LogWarning("AURAID Re attempt EndSession: " + err)
            );
        }

        void BeginReattemptTrainingFlow()
        {
            QuizManager.Instance?.ResetQuiz();
            var perf = FindObjectOfType<PerformanceAnalyzer>(true);
            perf?.Reset();
            TrainingPracticalVoicePrefetch.Reset();

            var flow = FindObjectOfType<TrainingFlowManager>(true);
            if (flow != null)
                flow.ShowTrainingInfo();
            else
                Debug.LogWarning("AURAID ReportController: TrainingFlowManager not found; cannot open training screens.");
        }

        /// <summary>Hides training UI and shows the main menu standby screen (same entry as cold start).</summary>
        static void ReturnToStandbyAfterReportSaved()
        {
            var flow = FindObjectOfType<UIFlowManager>(true);
            if (flow == null)
            {
                Debug.LogWarning("AURAID ReportController: UIFlowManager not found; cannot return to standby.");
                return;
            }
            flow.HideTrainingRoot();
            flow.ShowStandby();
        }

        void OnSaveAndQuitClicked()
        {
            OnSaveReportClicked();
        }
    }
}
