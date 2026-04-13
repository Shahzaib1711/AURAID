using System;
using System.Collections.Generic;
using UnityEngine;

namespace AURAID.Training
{
    public static class TrainingReportBuilder
    {
        public static TrainingReportData Build()
        {
            var report = new TrainingReportData();
            var manager = TrainingManager.Instance;
            var analyzer = UnityEngine.Object.FindObjectOfType<PerformanceAnalyzer>(true);

            report.trainee_name = manager?.CurrentTraineeName ?? "—";
            report.trainee_email = manager?.CurrentTraineeEmail ?? "—";

            if (manager?.SessionStartTimeUtc != null)
            {
                var start = manager.SessionStartTimeUtc.Value;
                var duration = DateTime.UtcNow - start;

                report.session_date = start.ToString("yyyy-MM-dd HH:mm") + " UTC";
                report.duration_minutes = $"{(int)duration.TotalMinutes} min {duration.Seconds} sec";
            }

            if (analyzer != null)
            {
                report.total_compressions = analyzer.TotalCompressions;
                report.compression_rate_bpm = analyzer.AverageRateBpm;
                report.pressure_accuracy_percent = analyzer.CorrectDepthPercent;
                report.hand_placement_accuracy_percent = analyzer.CorrectDepthPercent;

                report.compression_timestamps = analyzer.CompressionTimes;
                report.compression_forces = analyzer.CompressionForces;
            }

            // Quiz
            int correct = 0, total = 0;
            if (QuizManager.Instance != null)
            {
                (correct, total) = QuizManager.Instance.GetScore();
                report.quiz_items = QuizManager.Instance.BuildQuizReportItems();
            }

            report.quiz_attempted = total;
            report.quiz_total = total;
            report.quiz_score_percent = total > 0 ? (100f * correct / total) : 0f;

            float practical = analyzer != null
                ? (analyzer.CorrectDepthPercent + analyzer.CorrectRatePercent) * 0.5f
                : 0f;

            report.overall_score_percent = (practical * 0.6f) + (report.quiz_score_percent * 0.4f);

            report.performance_level = GetPerformanceLevel(report.overall_score_percent);
            report.status_label = GetStatus(report.overall_score_percent);

            report.summary = BuildSummary(analyzer, report.overall_score_percent);
            report.suggestions_for_improvement = GetSuggestions(analyzer);

            return report;
        }

        static string GetPerformanceLevel(float s)
        {
            if (s >= 85) return "Excellent";
            if (s >= 70) return "Good";
            if (s >= 50) return "Fair";
            return "Needs Improvement";
        }

        static string GetStatus(float s)
        {
            if (s >= 85) return "PASS";
            if (s >= 60) return "AVERAGE";
            return "NEEDS IMPROVEMENT";
        }

        static string BuildSummary(PerformanceAnalyzer a, float score)
        {
            if (a == null || a.TotalCompressions == 0)
                return "No sufficient CPR data recorded.";

            if (score >= 80) return "Strong CPR performance with good consistency.";
            if (score >= 60) return "Moderate performance. Improvements needed.";

            return "CPR performance needs improvement. Focus on rhythm and consistency.";
        }

        static string GetSuggestions(PerformanceAnalyzer a)
        {
            if (a == null || a.TotalCompressions == 0)
                return "Complete a CPR session to receive feedback.";

            var list = new List<string>();

            if (a.CorrectRatePercent < 80)
                list.Add($"Your rate is low ({a.AverageRateBpm:F0}/min). Aim for 100–120.");

            if (a.CorrectDepthPercent < 80)
                list.Add("Improve compression depth (5–6 cm).");

            if (a.GoodRecoilPercent < 80)
                list.Add("Allow full chest recoil.");

            if (list.Count == 0)
                return "Excellent performance. Maintain consistency.";

            return string.Join(" ", list);
        }
    }
}