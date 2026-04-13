using System;
using System.Collections.Generic;
using AURAID.Emergency.CPR;
using Firebase.Firestore;
using Firebase.Extensions;
using UnityEngine;

namespace AURAID.Emergency
{
    /// <summary>
    /// Firestore logging for Emergency CPR sessions (separate from <c>trainees</c>).
    /// Path: <c>emergency_sessions/{sessionId}</c> plus subcollections for intake, conversation, and sensor batches.
    /// </summary>
    public class EmergencySessionManager : MonoBehaviour
    {
        public static EmergencySessionManager Instance { get; private set; }

        [Header("Sensor batching")]
        [Tooltip("Flush sensor samples to Firestore after this many buffered rows.")]
        [SerializeField] int sensorBatchSize = 30;
        [Tooltip("Or flush at least this often (seconds) while samples arrive.")]
        [SerializeField] float sensorFlushIntervalSec = 2f;
        [Tooltip("If true, skip sensor writes until EmergencyController reports agent ready for sensors.")]
        [SerializeField] bool recordSensorsOnlyWhenAgentReady = true;

        [Header("Optional")]
        [SerializeField] EmergencyController emergencyController;

        FirebaseFirestore _db;
        string _sessionId;
        bool _sessionActive;
        readonly List<Dictionary<string, object>> _sensorBuffer = new List<Dictionary<string, object>>(64);
        float _lastSensorFlushTime;
        int _sensorBatchIndex;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
            _db = FirebaseFirestore.DefaultInstance;
            if (emergencyController == null)
                emergencyController = FindObjectOfType<EmergencyController>();
        }

        void OnDestroy()
        {
            if (Instance == this)
            {
                EndSession();
                Instance = null;
            }
        }

        /// <summary>Start a new top-level emergency session document.</summary>
        public void BeginSession(string ttsLanguage)
        {
            if (_sessionActive)
                EndSession();

            var sessionRef = _db.Collection("emergency_sessions").Document();
            _sessionId = sessionRef.Id;
            _sessionActive = true;
            _sensorBuffer.Clear();
            _sensorBatchIndex = 0;
            _lastSensorFlushTime = Time.time;

            var data = new Dictionary<string, object>
            {
                { "session_id", _sessionId },
                { "created_at", Timestamp.FromDateTime(DateTime.UtcNow.ToUniversalTime()) },
                { "status", "in_progress" },
                { "scenario_type", "CPR" },
                { "tts_language", string.IsNullOrEmpty(ttsLanguage) ? "en" : ttsLanguage },
            };

            sessionRef.SetAsync(data).ContinueWithOnMainThread(t =>
            {
                if (t.IsFaulted)
                    Debug.LogError("[EmergencySession] BeginSession failed: " + t.Exception);
                else
                    Debug.Log($"[EmergencySession] Created emergency_sessions/{_sessionId}");
            });
        }

        /// <summary>After voice intake + apply or timeout defaults.</summary>
        public void SetResolvedScenario(string contextEnumName, string patientCategoryEnumName, bool isPregnant)
        {
            if (!_sessionActive || string.IsNullOrEmpty(_sessionId)) return;

            var updates = new Dictionary<string, object>
            {
                { "resolved_context", contextEnumName ?? "" },
                { "resolved_patient_category", patientCategoryEnumName ?? "" },
                { "resolved_pregnant", isPregnant },
                { "scenario_resolved_at", Timestamp.FromDateTime(DateTime.UtcNow.ToUniversalTime()) },
            };

            _db.Collection("emergency_sessions").Document(_sessionId).UpdateAsync(updates).ContinueWithOnMainThread(t =>
            {
                if (t.IsFaulted)
                    Debug.LogWarning("[EmergencySession] SetResolvedScenario: " + t.Exception);
            });
        }

        /// <summary>Reads resolved scenario from the CPR agent config (after <see cref="EmergencyInputManager"/> apply).</summary>
        public void LogResolvedFromAgent(RuleBasedCprAgent agent)
        {
            if (agent?.config == null) return;
            var c = agent.config;
            bool pregnant = c.patientCategory == PatientCategory.Adult && c.context == EmergencyContext.Pregnancy;
            SetResolvedScenario(c.context.ToString(), c.patientCategory.ToString(), pregnant);
        }

        /// <summary>Voice intake step: question key + user STT + parsed summary.</summary>
        public void SaveIntakeExchange(string questionKey, string userTranscript, string parsedSummary)
        {
            if (!_sessionActive || string.IsNullOrEmpty(_sessionId)) return;

            var col = _db.Collection("emergency_sessions").Document(_sessionId).Collection("intake_exchanges");
            var data = new Dictionary<string, object>
            {
                { "question_key", questionKey ?? "" },
                { "user_transcript", userTranscript ?? "" },
                { "parsed_summary", parsedSummary ?? "" },
                { "created_at", Timestamp.FromDateTime(DateTime.UtcNow.ToUniversalTime()) },
            };

            col.AddAsync(data).ContinueWithOnMainThread(t =>
            {
                if (t.IsFaulted)
                    Debug.LogWarning("[EmergencySession] SaveIntakeExchange: " + t.Exception);
            });
        }

        /// <summary>Free-form user/agent lines after intake (e.g. follow-up STT).</summary>
        public void SaveConversationTurn(string role, string text)
        {
            if (!_sessionActive || string.IsNullOrEmpty(_sessionId)) return;
            if (string.IsNullOrWhiteSpace(text)) return;

            var col = _db.Collection("emergency_sessions").Document(_sessionId).Collection("conversation_turns");
            var data = new Dictionary<string, object>
            {
                { "role", role ?? "user" },
                { "text", text.Trim() },
                { "created_at", Timestamp.FromDateTime(DateTime.UtcNow.ToUniversalTime()) },
            };

            col.AddAsync(data).ContinueWithOnMainThread(t =>
            {
                if (t.IsFaulted)
                    Debug.LogWarning("[EmergencySession] SaveConversationTurn: " + t.Exception);
            });
        }

        /// <summary>Called from sensor integration (e.g. after PushSample).</summary>
        public void RecordSensorSample(SensorsSample s)
        {
            if (!_sessionActive || string.IsNullOrEmpty(_sessionId)) return;
            emergencyController?.NotifySensorSampleReceived();
            if (recordSensorsOnlyWhenAgentReady && emergencyController != null && !emergencyController.IsAgentReadyForSensors)
                return;

            _sensorBuffer.Add(SampleToMap(s));
            bool byCount = _sensorBuffer.Count >= sensorBatchSize;
            bool byTime = Time.time - _lastSensorFlushTime >= sensorFlushIntervalSec;
            if (byCount || byTime)
                FlushSensorBuffer();
        }

        /// <summary>Logs non-conversational emergency events (e.g., sensor fault start/recovery).</summary>
        public void SaveSystemEvent(string eventKey, string message)
        {
            if (!_sessionActive || string.IsNullOrEmpty(_sessionId)) return;

            var col = _db.Collection("emergency_sessions").Document(_sessionId).Collection("system_events");
            var data = new Dictionary<string, object>
            {
                { "event_key", eventKey ?? "" },
                { "message", message ?? "" },
                { "created_at", Timestamp.FromDateTime(DateTime.UtcNow.ToUniversalTime()) },
            };

            col.AddAsync(data).ContinueWithOnMainThread(t =>
            {
                if (t.IsFaulted)
                    Debug.LogWarning("[EmergencySession] SaveSystemEvent: " + t.Exception);
            });
        }

        static Dictionary<string, object> SampleToMap(SensorsSample s)
        {
            return new Dictionary<string, object>
            {
                { "t", s.time },
                { "fsr_force", s.fsrForce },
                { "heart_rate_bpm", s.heartRateBpm },
                { "spo2", s.spo2 },
                { "motion", s.motion },
                { "bno_motion", s.bnoMotion },
                { "arm_tilt_deg", s.armTiltFromVerticalDeg },
                { "responsive", s.responsive },
                { "respiration_value", s.respirationValue },
                { "breathing_detected", s.breathingDetected },
                { "gasping_detected", s.gaspingDetected },
                { "cyanosis_detected", s.cyanosisDetected },
            };
        }

        void FlushSensorBuffer()
        {
            if (_sensorBuffer.Count == 0) return;

            var batchCopy = new List<Dictionary<string, object>>(_sensorBuffer);
            _sensorBuffer.Clear();
            _lastSensorFlushTime = Time.time;
            int idx = _sensorBatchIndex++;
            string sid = _sessionId;

            var data = new Dictionary<string, object>
            {
                { "batch_index", idx },
                { "created_at", Timestamp.FromDateTime(DateTime.UtcNow.ToUniversalTime()) },
                { "samples", batchCopy },
            };

            _db.Collection("emergency_sessions").Document(sid).Collection("sensor_batches").AddAsync(data).ContinueWithOnMainThread(t =>
            {
                if (t.IsFaulted)
                    Debug.LogWarning("[EmergencySession] sensor_batches flush: " + t.Exception);
            });
        }

        /// <summary>Mark session complete and flush pending sensors.</summary>
        public void EndSession()
        {
            if (!_sessionActive || string.IsNullOrEmpty(_sessionId)) return;

            FlushSensorBuffer();

            string sid = _sessionId;
            _sessionActive = false;
            _sessionId = null;

            var updates = new Dictionary<string, object>
            {
                { "ended_at", Timestamp.FromDateTime(DateTime.UtcNow.ToUniversalTime()) },
                { "status", "completed" },
            };

            _db.Collection("emergency_sessions").Document(sid).UpdateAsync(updates).ContinueWithOnMainThread(t =>
            {
                if (t.IsFaulted)
                    Debug.LogWarning("[EmergencySession] EndSession: " + t.Exception);
                else
                    Debug.Log($"[EmergencySession] Completed emergency_sessions/{sid}");
            });
        }

    }
}
