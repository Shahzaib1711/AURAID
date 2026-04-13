using System;
using System.Collections.Generic;

namespace AURAID.Training
{
    [Serializable]
    public class TrainingReportData
    {
        public string trainee_name;
        public string trainee_email;
        public string session_date;
        public string duration_minutes;

        public int total_compressions;
        public float compression_rate_bpm;
        public float pressure_accuracy_percent;
        public float hand_placement_accuracy_percent;

        public string reaction_time_display;

        public float overall_score_percent;
        public string performance_level;
        public string status_label;

        public string summary;
        public string suggestions_for_improvement;

        public int quiz_attempted;
        public int quiz_total;
        public float quiz_score_percent;

        public List<Dictionary<string, object>> quiz_items;

        // 🔥 NEW: Chart data
        public List<float> compression_timestamps;
        public List<float> compression_forces;

        public Dictionary<string, object> ToFirestoreMap()
        {
            var map = new Dictionary<string, object>
            {
                { "trainee_name", trainee_name ?? "" },
                { "trainee_email", trainee_email ?? "" },
                { "session_date", session_date ?? "" },
                { "duration_minutes", duration_minutes ?? "" },
                { "total_compressions", total_compressions },
                { "compression_rate_bpm", compression_rate_bpm },
                { "pressure_accuracy_percent", pressure_accuracy_percent },
                { "hand_placement_accuracy_percent", hand_placement_accuracy_percent },
                { "reaction_time_display", reaction_time_display ?? "N/A" },
                { "overall_score_percent", overall_score_percent },
                { "performance_level", performance_level ?? "" },
                { "status_label", status_label ?? "" },
                { "summary", summary ?? "" },
                { "suggestions_for_improvement", suggestions_for_improvement ?? "" },
                { "quiz_attempted", quiz_attempted },
                { "quiz_total", quiz_total },
                { "quiz_score_percent", quiz_score_percent }
            };

            if (quiz_items != null && quiz_items.Count > 0)
                map["quiz_items"] = new List<object>(quiz_items);

            if (compression_timestamps != null)
                map["compression_timestamps"] = compression_timestamps;

            if (compression_forces != null)
                map["compression_forces"] = compression_forces;

            return map;
        }
    }
}