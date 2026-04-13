using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Firebase.Firestore;
using Firebase.Extensions;
using UnityEngine;

namespace AURAID.Training
{
    /// <summary>
    /// Singleton responsible for Training mode registration + session lifecycle.
    /// Handles Firestore persistence and caches current trainee / session IDs.
    /// </summary>
    public class TrainingManager : MonoBehaviour
    {
        public static TrainingManager Instance { get; private set; }

        /// <summary>Self-reported confidence uses the same 0–5 scale for &quot;before&quot; and &quot;after&quot; (report UI, Firestore).</summary>
        public const int ConfidenceScaleMax = 5;

        const string CurrentTraineeKey = "AURAID_CurrentTraineeID";
        const string CurrentSessionKey = "AURAID_CurrentSessionID";

        FirebaseFirestore _firestore;

        /// <summary>Clamped copy of <c>confidence_before</c> for the active session (in-memory; set in <see cref="CreateSession"/>).</summary>
        int _sessionConfidenceBefore;

        public string CurrentTraineeId { get; private set; }
        public string CurrentSessionId { get; private set; }

        /// <summary>Trainee&apos;s self-rated confidence at session start (0–<see cref="ConfidenceScaleMax"/>).</summary>
        public int SessionConfidenceBefore => _sessionConfidenceBefore;
        /// <summary>Cached trainee name (set when CreateTrainee succeeds). Used for report generation.</summary>
        public string CurrentTraineeName { get; private set; }
        /// <summary>Cached trainee email (set when CreateTrainee succeeds). Used for report email.</summary>
        public string CurrentTraineeEmail { get; private set; }
        /// <summary>Session start time (UTC). Set when CreateSession succeeds. Used for report duration.</summary>
        public DateTime? SessionStartTimeUtc { get; private set; }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            // DontDestroyOnLoad only works on root objects; move to root if needed
            if (transform.parent != null)
                transform.SetParent(null);
            DontDestroyOnLoad(gameObject);

            _firestore = FirebaseFirestore.DefaultInstance;

            CurrentTraineeId = PlayerPrefs.GetString(CurrentTraineeKey, string.Empty);
            CurrentSessionId = PlayerPrefs.GetString(CurrentSessionKey, string.Empty);
        }

        #region Public API

        /// <summary>
        /// Resolve trainee by email (reuse existing trainee ID) or create a new trainee.
        /// </summary>
        public void CreateTrainee(string name, string email, Action onSuccess = null, Action<string> onError = null)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                onError?.Invoke("Name is required.");
                return;
            }
            if (string.IsNullOrWhiteSpace(email) || !email.Contains("@"))
            {
                onError?.Invoke("A valid email is required.");
                return;
            }

            string normalizedEmail = NormalizeEmail(email);
            string trimmedName = name.Trim();

            // 1) Preferred lookup key for dedupe across devices.
            _firestore.Collection("trainees")
                .WhereEqualTo("email_normalized", normalizedEmail)
                .Limit(1)
                .GetSnapshotAsync()
                .ContinueWithOnMainThread((Task<QuerySnapshot> task) =>
                {
                    if (task.IsFaulted || task.IsCanceled)
                    {
                        Debug.LogError($"AURAID TrainingManager: Trainee lookup (email_normalized) failed: {task.Exception}");
                        onError?.Invoke(task.Exception?.Message ?? "Trainee lookup failed.");
                        return;
                    }

                    if (task.Result != null && task.Result.Count > 0)
                    {
                        var firstDoc = task.Result.Documents.FirstOrDefault();
                        if (firstDoc != null)
                            ReuseExistingTrainee(firstDoc.Reference, trimmedName, normalizedEmail, onSuccess, onError);
                        return;
                    }

                    // 2) Backward-compatibility lookup for older docs that only stored "email".
                    _firestore.Collection("trainees")
                        .WhereEqualTo("email", normalizedEmail)
                        .Limit(1)
                        .GetSnapshotAsync()
                        .ContinueWithOnMainThread((Task<QuerySnapshot> fallbackTask) =>
                        {
                            if (fallbackTask.IsFaulted || fallbackTask.IsCanceled)
                            {
                                Debug.LogError($"AURAID TrainingManager: Trainee lookup (email) failed: {fallbackTask.Exception}");
                                onError?.Invoke(fallbackTask.Exception?.Message ?? "Trainee lookup failed.");
                                return;
                            }

                            if (fallbackTask.Result != null && fallbackTask.Result.Count > 0)
                            {
                                var firstDoc = fallbackTask.Result.Documents.FirstOrDefault();
                                if (firstDoc != null)
                                    ReuseExistingTrainee(firstDoc.Reference, trimmedName, normalizedEmail, onSuccess, onError);
                                return;
                            }

                            // 3) No trainee found: create new with Firestore-generated ID.
                            CreateNewTrainee(trimmedName, normalizedEmail, onSuccess, onError);
                        });
                });
        }

        void ReuseExistingTrainee(DocumentReference traineeRef, string name, string normalizedEmail, Action onSuccess, Action<string> onError)
        {
            string traineeId = traineeRef.Id;
            CurrentTraineeId = traineeId;
            CurrentTraineeName = name;
            CurrentTraineeEmail = normalizedEmail;

            var updates = new Dictionary<string, object>
            {
                { "name", name },
                { "email", normalizedEmail },
                { "email_normalized", normalizedEmail },
                { "last_seen_at", Timestamp.FromDateTime(DateTime.UtcNow.ToUniversalTime()) },
            };

            traineeRef.SetAsync(updates, SetOptions.MergeAll).ContinueWithOnMainThread(updateTask =>
            {
                if (updateTask.IsFaulted || updateTask.IsCanceled)
                {
                    Debug.LogError($"AURAID TrainingManager: Reuse trainee update failed: {updateTask.Exception}");
                    onError?.Invoke(updateTask.Exception?.Message ?? "Failed to update existing trainee.");
                    return;
                }

                PlayerPrefs.SetString(CurrentTraineeKey, traineeId);
                PlayerPrefs.Save();

                Debug.Log($"AURAID TrainingManager: Reusing trainee {traineeId} for {normalizedEmail}");
                onSuccess?.Invoke();
            });
        }

        void CreateNewTrainee(string name, string normalizedEmail, Action onSuccess, Action<string> onError)
        {
            var doc = _firestore.Collection("trainees").Document();
            string traineeId = doc.Id;
            CurrentTraineeId = traineeId;
            CurrentTraineeName = name;
            CurrentTraineeEmail = normalizedEmail;

            var data = new Dictionary<string, object>
            {
                { "name", name },
                { "email", normalizedEmail },
                { "email_normalized", normalizedEmail },
                { "created_at", Timestamp.FromDateTime(DateTime.UtcNow.ToUniversalTime()) },
            };

            doc.SetAsync(data).ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted || task.IsCanceled)
                {
                    Debug.LogError($"AURAID TrainingManager: CreateTrainee (new) failed: {task.Exception}");
                    onError?.Invoke(task.Exception?.Message ?? "CreateTrainee failed.");
                    return;
                }

                PlayerPrefs.SetString(CurrentTraineeKey, traineeId);
                PlayerPrefs.Save();

                Debug.Log($"AURAID TrainingManager: Created trainee {traineeId}");
                onSuccess?.Invoke();
            });
        }

        /// <summary>
        /// Create a new session document under the current trainee.
        /// </summary>
        public void CreateSession(int confidenceBefore, Action onSuccess = null, Action<string> onError = null)
        {
            if (string.IsNullOrEmpty(CurrentTraineeId))
            {
                onError?.Invoke("No current trainee. Call CreateTrainee first.");
                return;
            }

            int beforeClamped = Mathf.Clamp(confidenceBefore, 0, ConfidenceScaleMax);
            _sessionConfidenceBefore = beforeClamped;

            string sessionId = GenerateSessionId();
            CurrentSessionId = sessionId;
            SessionStartTimeUtc = DateTime.UtcNow;

            var sessionsCol = _firestore.Collection("trainees")
                .Document(CurrentTraineeId)
                .Collection("sessions");

            var doc = sessionsCol.Document(sessionId);

            var data = new Dictionary<string, object>
            {
                { "session_start_time", Timestamp.FromDateTime(DateTime.UtcNow.ToUniversalTime()) },
                { "confidence_before", beforeClamped },
                { "confidence_after", 0 },
                { "quiz_score", 0 },
                { "total_questions", 0 },
                { "status", "in_progress" },
                { "scenario_type", "CPR" },
            };

            doc.SetAsync(data).ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted || task.IsCanceled)
                {
                    Debug.LogError($"AURAID TrainingManager: CreateSession failed: {task.Exception}");
                    onError?.Invoke(task.Exception?.Message ?? "CreateSession failed.");
                    return;
                }

                PlayerPrefs.SetString(CurrentSessionKey, sessionId);
                PlayerPrefs.Save();

                Debug.Log($"AURAID TrainingManager: Created session {sessionId} for trainee {CurrentTraineeId}");
                onSuccess?.Invoke();
            });
        }

        /// <summary>
        /// Save a QnA exchange (user question + agent answer) under the current session.
        /// Path: trainees/{traineeId}/sessions/{sessionId}/qna_exchanges/{autoId}
        /// </summary>
        public void SaveQnAExchange(string question, string answer, Action onSuccess = null, Action<string> onError = null)
        {
            if (string.IsNullOrEmpty(CurrentTraineeId) || string.IsNullOrEmpty(CurrentSessionId))
            {
                onError?.Invoke("No active session. QnA exchange not saved.");
                return;
            }

            var col = _firestore.Collection("trainees")
                .Document(CurrentTraineeId)
                .Collection("sessions")
                .Document(CurrentSessionId)
                .Collection("qna_exchanges");

            var data = new Dictionary<string, object>
            {
                { "question", question ?? "" },
                { "answer", answer ?? "" },
                { "created_at", Timestamp.FromDateTime(DateTime.UtcNow.ToUniversalTime()) },
            };

            col.AddAsync(data).ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted || task.IsCanceled)
                {
                    Debug.LogWarning($"AURAID TrainingManager: SaveQnAExchange failed: {task.Exception}");
                    onError?.Invoke(task.Exception?.Message ?? "SaveQnAExchange failed.");
                    return;
                }
                onSuccess?.Invoke();
            });
        }

        /// <summary>
        /// Save quiz score to the current session (e.g. when user clicks Next on last question).
        /// Updates only quiz_score and total_questions on the session document.
        /// </summary>
        public void SaveQuizScore(int quizScore, int totalQuestions, Action onSuccess = null, Action<string> onError = null)
        {
            if (string.IsNullOrEmpty(CurrentTraineeId) || string.IsNullOrEmpty(CurrentSessionId))
            {
                onError?.Invoke("No active session. Quiz score not saved.");
                return;
            }

            var doc = _firestore.Collection("trainees")
                .Document(CurrentTraineeId)
                .Collection("sessions")
                .Document(CurrentSessionId);

            var updates = new Dictionary<string, object>
            {
                { "quiz_score", quizScore },
                { "total_questions", totalQuestions },
            };

            doc.UpdateAsync(updates).ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted || task.IsCanceled)
                {
                    Debug.LogWarning($"AURAID TrainingManager: SaveQuizScore failed: {task.Exception}");
                    onError?.Invoke(task.Exception?.Message ?? "SaveQuizScore failed.");
                    return;
                }
                Debug.Log($"AURAID TrainingManager: Saved quiz score {quizScore}/{totalQuestions} for session {CurrentSessionId}");
                onSuccess?.Invoke();
            });
        }

        /// <summary>
        /// Mark the current session as completed and store final values.
        /// If reportData is provided, it is written to the session so a Cloud Function can generate a PDF and email it.
        /// </summary>
        public void EndSession(int confidenceAfter, int quizScore, int totalQuestions, Dictionary<string, object> reportData = null, Action onSuccess = null, Action<string> onError = null)
        {
            if (string.IsNullOrEmpty(CurrentTraineeId) || string.IsNullOrEmpty(CurrentSessionId))
            {
                onError?.Invoke("No active session to end.");
                return;
            }

            var doc = _firestore.Collection("trainees")
                .Document(CurrentTraineeId)
                .Collection("sessions")
                .Document(CurrentSessionId);

            int afterClamped = Mathf.Clamp(confidenceAfter, 0, ConfidenceScaleMax);
            int improvement = afterClamped - _sessionConfidenceBefore;

            var updates = new Dictionary<string, object>
            {
                { "confidence_after", afterClamped },
                { "confidence_improvement", improvement },
                { "quiz_score", quizScore },
                { "total_questions", totalQuestions },
                { "status", "completed" }
            };
            if (reportData != null && reportData.Count > 0)
            {
                updates["report_data"] = reportData;
                updates["report_requested_at"] = Timestamp.FromDateTime(DateTime.UtcNow.ToUniversalTime());
            }

            doc.UpdateAsync(updates).ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted || task.IsCanceled)
                {
                    Debug.LogError($"AURAID TrainingManager: EndSession failed: {task.Exception}");
                    onError?.Invoke(task.Exception?.Message ?? "EndSession failed.");
                    return;
                }

                Debug.Log($"AURAID TrainingManager: Ended session {CurrentSessionId} for trainee {CurrentTraineeId}");
                onSuccess?.Invoke();
            });
        }

        #endregion

        #region Helpers

        string GenerateSessionId()
        {
            return Guid.NewGuid().ToString("N");
        }

        static string NormalizeEmail(string email) => (email ?? string.Empty).Trim().ToLowerInvariant();

        #endregion
    }
}

