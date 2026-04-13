using System.Collections;
using AURAID.Emergency.CPR;
using AURAID.Voice;
using TMPro;
using UnityEngine;

namespace AURAID.Emergency
{
    public partial class EmergencyController : MonoBehaviour
    {
        [Header("Agent")]
        public RuleBasedCprAgent cprAgent;

        [Header("Scenario input (patient + context)")]
        [SerializeField] EmergencyInputManager emergencyInput;

        [Header("Voice emergency flow")]
        [Tooltip("Voice-guided triage → responsive/breathing check → CPR with FSR/BNO only.")]
        public bool useVoiceScenarioIntake = true;
        [Tooltip("If voice triage never completes (e.g. stuck), force CPR after this many seconds. Must be longer than a full voice session — not 18s, or questions will be skipped.")]
        public float scenarioFallbackTimeout = 600f;
        [Tooltip("Extra pause after TTS stops before starting the microphone.")]
        public float pauseAfterTtsBeforeListenSec = 0.45f;

        [Header("UI (optional)")]
        public TMP_Text feedbackText;

        [Header("Voice")]
        public VoiceConversationManager voice;

        [Header("Voice throttle (CPR feedback only)")]
        public float voiceCooldownSec = 4f;
        [Tooltip("During steady good compressions, repeat this encouragement on TTS at most this often (first one can play as soon as quality is Good). Corrections still use voiceCooldownSec.")]
        [SerializeField] float goodCprEncouragementVoiceIntervalSec = 22f;
        public string ttsLanguage = "en";

        [Header("Sensor fail-safe (CPR)")]
        [Tooltip("If no sensor sample arrives for this long during CPR, enter degraded voice-only guidance.")]
        public float sensorHeartbeatTimeoutSec = 2f;
        [Tooltip("While degraded mode is active, repeat voice-only reminder at this interval.")]
        public float degradedVoiceRepeatSec = 10f;

        float lastVoiceTime = -999f;
        string lastSpoken = "";
        float _lastGoodCprEncouragementVoice = -999f;
        float _lastSensorSampleAt = -999f;
        float _lastDegradedVoiceAt = -999f;
        bool _sensorFaultActive;

        EmergencyContext selectedContext = EmergencyContext.Standard;
        PatientCategory selectedCategory = PatientCategory.Adult;
        bool isPregnant;

        bool isConfigured;
        bool inputReceived;
        Coroutine _emergencyFlowCo;
        Coroutine _postTtsListenCo;

        [Header("STT (optional manual)")]
        public KeyCode listenKey = KeyCode.V;
        public float listenDurationSec = 5f;

        /// <summary>TTS prompts (needs VoiceConversationManager + TtsClient + AudioSource).</summary>
        bool CanPlayEmergencyTts =>
            useVoiceScenarioIntake && voice != null && voice.tts != null &&
            (voice.audioSource != null || voice.GetComponent<AudioSource>() != null);

        bool CanListenStt =>
            voice != null && voice.recorder != null && voice.stt != null;

        void Awake()
        {
            if (FindObjectOfType<EmergencySessionManager>() == null)
                gameObject.AddComponent<EmergencySessionManager>();

            if (cprAgent != null)
                cprAgent.OnFeedback += HandleFeedback;
            EnsureVoiceTranscriptHook();

            if (emergencyInput == null)
                emergencyInput = GetComponentInChildren<EmergencyInputManager>(true);
            if (emergencyInput != null)
                emergencyInput.AppliedToAgent += OnScenarioAppliedFromInputManager;
        }

        void OnDestroy()
        {
            if (emergencyInput != null)
                emergencyInput.AppliedToAgent -= OnScenarioAppliedFromInputManager;
        }

        void OnEnable()
        {
            selectedContext = EmergencyContext.Standard;
            selectedCategory = PatientCategory.Adult;
            isPregnant = false;

            isConfigured = false;
            inputReceived = false;
            _sensorFaultActive = false;
            _lastSensorSampleAt = Time.time;
            _lastDegradedVoiceAt = -999f;
            StopPostTtsListenCoroutine();

            if (_emergencyFlowCo != null)
            {
                StopCoroutine(_emergencyFlowCo);
                _emergencyFlowCo = null;
            }
            if (_mainFlowCo != null)
            {
                StopCoroutine(_mainFlowCo);
                _mainFlowCo = null;
            }
            Debug.Log("[EmergencyController] Emergency flow started.");

            EmergencySessionManager.Instance?.BeginSession(ttsLanguage);

            // Do not start ScenarioFallbackTimeoutCoroutine here — it would fire mid-triage (old 18s bug).
            // MainEmergencyFlowCoroutine starts the stuck-fallback timer only after voice TTS is confirmed.
            _mainFlowCo = StartCoroutine(MainEmergencyFlowCoroutine());
        }

        void OnDisable()
        {
            EmergencySessionManager.Instance?.EndSession();
            _cprSensorFeedActive = false;

            if (_emergencyFlowCo != null)
            {
                StopCoroutine(_emergencyFlowCo);
                _emergencyFlowCo = null;
            }
            if (_mainFlowCo != null)
            {
                StopCoroutine(_mainFlowCo);
                _mainFlowCo = null;
            }
            StopCprLoopCoroutine();
            StopPostTtsListenCoroutine();
            if (_postBreathingEndCo != null)
            {
                StopCoroutine(_postBreathingEndCo);
                _postBreathingEndCo = null;
            }
        }

        void StopCprLoopCoroutine()
        {
            if (_cprLoopCo != null)
            {
                StopCoroutine(_cprLoopCo);
                _cprLoopCo = null;
            }
        }

        /// <summary>
        /// Only started when voice triage is active. Waits <see cref="scenarioFallbackTimeout"/>; if main flow never
        /// sets <see cref="_flowFinished"/>, forces default CPR (same as stuck user).
        /// </summary>
        IEnumerator ScenarioFallbackTimeoutCoroutine()
        {
            yield return new WaitForSeconds(scenarioFallbackTimeout);

            if (!_flowFinished)
            {
                Debug.LogWarning("[EmergencyController] Voice triage stuck past scenarioFallbackTimeout — forcing CPR path with defaults + sensors.");
                ForceEmergencyCprAfterTimeout();
            }

            _emergencyFlowCo = null;
        }

        void StopEmergencyScenarioFallbackCoroutineIfAny()
        {
            if (_emergencyFlowCo != null)
            {
                StopCoroutine(_emergencyFlowCo);
                _emergencyFlowCo = null;
            }
        }

        public void NotifyEmergencyInputReceived()
        {
            inputReceived = true;
            Debug.Log("[EmergencyController] AURAID emergency input received (patient/context touched).");
        }

        /// <summary>Called by sensor integrations whenever a new sample is received.</summary>
        public void NotifySensorSampleReceived()
        {
            _lastSensorSampleAt = Time.time;
            if (!_sensorFaultActive)
                return;

            _sensorFaultActive = false;
            _lastDegradedVoiceAt = Time.time;
            bool isAr = ttsLanguage == "ar";
            string msg = isAr
                ? "عاد اتصال المستشعرات. سأتابع التوجيه اللحظي أثناء الضغطات."
                : "Sensor connection restored. Resuming real-time compression guidance.";
            SetFeedback(msg);
            SpeakThrottled(msg);
            EmergencySessionManager.Instance?.SaveSystemEvent("sensor_fault_cleared", msg);
        }

        internal void TickSensorFailSafeDuringCpr(bool isArabic)
        {
            if (!_cprSensorFeedActive || _flowPhase != EmergencyFlowPhase.CprActive)
                return;
            if (sensorHeartbeatTimeoutSec <= 0f)
                return;

            bool stale = (Time.time - _lastSensorSampleAt) > sensorHeartbeatTimeoutSec;
            if (!stale)
                return;

            if (!_sensorFaultActive)
            {
                _sensorFaultActive = true;
                _lastDegradedVoiceAt = -999f;
                string first = isArabic
                    ? "انقطع تدفق المستشعرات. استمر في الضغطات بسرعة 100 إلى 120 في الدقيقة حتى يعود الاتصال."
                    : "Sensor stream lost. Continue compressions at 100 to 120 per minute until sensors reconnect.";
                SetFeedback(first);
                SpeakThrottled(first);
                EmergencySessionManager.Instance?.SaveSystemEvent("sensor_fault_start", first);
            }

            if (Time.time - _lastDegradedVoiceAt >= Mathf.Max(3f, degradedVoiceRepeatSec))
            {
                _lastDegradedVoiceAt = Time.time;
                string repeat = isArabic
                    ? "المستشعرات غير متصلة. استمر في الضغطات: 100 إلى 120 في الدقيقة وعمق 5 إلى 6 سم."
                    : "Sensors still offline. Keep compressions at 100 to 120 per minute and depth 5 to 6 centimeters.";
                SetFeedback(repeat);
                SpeakThrottled(repeat);
            }
        }

        void OnScenarioAppliedFromInputManager()
        {
            StopEmergencyScenarioFallbackCoroutineIfAny();
            inputReceived = true;
            Debug.Log("[EmergencyController] Triage config updated from UI (CPR sensors still follow voice flow or timeout).");
        }

        void EnsureVoiceTranscriptHook()
        {
            if (voice == null) return;
            voice.OnUserTranscript -= HandleUserTranscript;
            voice.OnUserTranscript += HandleUserTranscript;
        }

        void TryAutoWireVoiceFromScene()
        {
            if (voice == null)
                voice = FindObjectOfType<VoiceConversationManager>();
            if (voice == null)
            {
                Debug.LogWarning("[EmergencyController] No VoiceConversationManager in scene. Assign on EmergencyController or add Voice root.");
                return;
            }
            if (voice.tts == null)
                voice.tts = FindObjectOfType<TtsClient>();
            if (voice.audioSource == null)
                voice.audioSource = voice.GetComponent<AudioSource>() ?? FindObjectOfType<AudioSource>();
            if (voice.recorder == null)
                voice.recorder = FindObjectOfType<MicrophoneRecorder>();
            if (voice.stt == null)
                voice.stt = FindObjectOfType<SttClient>();
            EnsureVoiceTranscriptHook();
        }

        void StopPostTtsListenCoroutine()
        {
            if (_postTtsListenCo != null)
            {
                StopCoroutine(_postTtsListenCo);
                _postTtsListenCo = null;
            }
        }

#if ENABLE_LEGACY_INPUT_MANAGER
        void Update()
        {
            if (listenKey != KeyCode.None && Input.GetKeyDown(listenKey) && voice != null)
                voice.ListenAndTranscribe(listenDurationSec);
        }
#else
        void Update() { }
#endif

        void HandleUserTranscript(string transcript)
        {
            if (string.IsNullOrWhiteSpace(transcript)) return;
            string t = transcript.Trim();

            if (_awaitingBreathingAnswerAfterMovementPrompt && Time.time > _movementBreathingAnswerDeadline)
                _awaitingBreathingAnswerAfterMovementPrompt = false;

            if (_awaitingListenPhase != null)
            {
                ProcessFlowTranscript(t);
                return;
            }

            if (_awaitingBreathingAnswerAfterMovementPrompt && _cprSensorFeedActive)
            {
                // Resolve explicit "not breathing / no" before yes — "not breathing" still matches breathing keywords.
                if (MovementBreathingAnswerSoundsLikeNo(t))
                {
                    _awaitingBreathingAnswerAfterMovementPrompt = false;
                    bool isAr = ttsLanguage == "ar";
                    string msg = isAr
                        ? "تابع ضغطات الصدر بقوة وبسرعة بين 100 و 120 في الدقيقة. تأكد من العمق والارتداد الكامل."
                        : "Continue chest compressions hard and fast at 100 to 120 per minute. Keep depth and full recoil.";
                    SetFeedback(isAr ? "قال المستخدم: " + t : "You said: " + t);
                    EmergencySessionManager.Instance?.SaveConversationTurn("user", t);
                    EmergencySessionManager.Instance?.SaveConversationTurn("agent", msg);
                    SpeakThrottled(msg);
                    return;
                }
                if (VoiceScenarioParser.ParseYesNo(t, ttsLanguage) || UserSaidBreathingKeyword(t))
                {
                    _awaitingBreathingAnswerAfterMovementPrompt = false;
                    _userBreathingNormal = true;
                    EndCprBecauseUserReportsBreathing();
                    return;
                }
            }

            if (_cprSensorFeedActive && UserSaidBreathingKeyword(t))
            {
                _userBreathingNormal = true;
                EndCprBecauseUserReportsBreathing();
                return;
            }

            bool isAr2 = ttsLanguage == "ar";
            SetFeedback(isAr2 ? "قال المستخدم: " + t : "You said: " + t);
            EmergencySessionManager.Instance?.SaveConversationTurn("user", t);
            string agentEcho = isAr2 ? "فهمت. قال المستخدم: " + t : "Got it. You said: " + t;
            EmergencySessionManager.Instance?.SaveConversationTurn("agent", agentEcho);
            SpeakThrottled(agentEcho);
        }

        bool UserSaidBreathingKeyword(string t)
        {
            string s = t.ToLowerInvariant();
            if (ttsLanguage == "ar")
                return t.Contains("تنفس") || t.Contains("يتنفس");
            return s.Contains("breathing") || s.Contains("breathe") || s.Contains("they're breathing") || s.Contains("they are breathing");
        }

        void HandleFeedback(CprQuality quality, string message)
        {
            if (!isActiveAndEnabled)
                return;

            string display = LocalizeIfArabic(message);
            SetFeedback(display);

            // Good compressions: on-screen + logs every feedback tick, but TTS was intentionally muted
            // (IsBigEventForVoice excludes Good) to avoid spam — users then hear long silence. Re-voice on an interval.
            if (quality == CprQuality.Good)
            {
                if (Time.time - _lastGoodCprEncouragementVoice >= goodCprEncouragementVoiceIntervalSec)
                {
                    _lastGoodCprEncouragementVoice = Time.time;
                    SpeakThrottled(display, allowRepeatMessage: true);
                }
            }
            else if (IsBigEventForVoice(quality, message))
                SpeakThrottled(display);

            if (quality == CprQuality.PossibleMovementCheckBreathing)
            {
                _awaitingBreathingAnswerAfterMovementPrompt = true;
                _movementBreathingAnswerDeadline = Time.time + Mathf.Max(45f, listenDurationSec + 25f);
            }
        }

        string LocalizeIfArabic(string message)
        {
            if (ttsLanguage != "ar" || string.IsNullOrEmpty(message)) return message;
            return EmergencyLocalization.ToArabic(message);
        }

        void SpeakThrottled(string msg, bool allowRepeatMessage = false)
        {
            if (voice == null) return;
            if (!allowRepeatMessage && msg == lastSpoken) return;
            if (Time.time - lastVoiceTime < voiceCooldownSec) return;

            lastSpoken = msg;
            lastVoiceTime = Time.time;
            voice.AskUser(msg, ttsLanguage);
        }

        bool IsBigEventForVoice(CprQuality quality, string message)
        {
            if (string.IsNullOrEmpty(message)) return false;
            switch (quality)
            {
                case CprQuality.PauseTooLong:
                case CprQuality.TooSlow:
                case CprQuality.TooFast:
                case CprQuality.TooLight:
                case CprQuality.TooHard:
                case CprQuality.IncompleteRecoil:
                case CprQuality.ArmsBent:
                case CprQuality.PostureNudge:
                case CprQuality.PossibleMovementCheckBreathing:
                    return true;
                case CprQuality.Good:
                default:
                    return false;
            }
        }

        void SetFeedback(string msg)
        {
            if (feedbackText != null)
                feedbackText.text = msg;

            Debug.Log("[Emergency] " + msg);
        }

        /// <summary>Legacy / tools: set context by enum index (same order as <see cref="EmergencyContext"/>).</summary>
        public void OnSelectContext(int index)
        {
            selectedContext = (EmergencyContext)index;
            inputReceived = true;
        }

        public void OnSelectPatient(int index)
        {
            selectedCategory = (PatientCategory)index;
            inputReceived = true;
        }

        public void OnSelectPregnancy(bool value)
        {
            isPregnant = value;
            inputReceived = true;
        }

        public void OnSelectPregnancyYes() => OnSelectPregnancy(true);
        public void OnSelectPregnancyNo() => OnSelectPregnancy(false);

        /// <summary>Applies triage from UI selections to <see cref="EmergencyInputManager"/> only (does not open CPR sensors).</summary>
        public void ApplyScenarioToAgent()
        {
            if (emergencyInput == null)
            {
                Debug.LogWarning("[EmergencyController] EmergencyInputManager not assigned.");
                return;
            }

            if (isPregnant && selectedCategory == PatientCategory.Adult)
                selectedContext = EmergencyContext.Pregnancy;

            emergencyInput.SetContext(selectedContext);
            emergencyInput.SetPatientCategory(selectedCategory);
            emergencyInput.ApplyToAgent();
            StopPostTtsListenCoroutine();
            Debug.Log($"[EmergencyController] UI triage applied → Context: {selectedContext}, Category: {selectedCategory}, Pregnant: {isPregnant}.");
        }

        static class VoiceScenarioParser
        {
            public static EmergencyContext ParseContext(string t, string lang)
            {
                string s = t.ToLowerInvariant();
                bool ar = lang == "ar";

                if (ar && (ContainsAny(t, "غرق", "تغرق") || s.Contains("drown")))
                    return EmergencyContext.Drowning;
                if (s.Contains("drown") || s.Contains("pool") || s.Contains("water"))
                    return EmergencyContext.Drowning;

                if (ar && ContainsAny(t, "تنفس", "ضيق", "اختناق"))
                    return EmergencyContext.ShortnessOfBreath;
                if (s.Contains("breath") || s.Contains("asthma") || s.Contains("sob") || s.Contains("shortness"))
                    return EmergencyContext.ShortnessOfBreath;

                if (ar && ContainsAny(t, "جرعة", "مخدر", "تسمم"))
                    return EmergencyContext.DrugOverdose;
                if (s.Contains("overdose") || s.Contains("drug") || s.Contains("opioid"))
                    return EmergencyContext.DrugOverdose;

                if (ar && ContainsAny(t, "حامل", "حمل"))
                    return EmergencyContext.Pregnancy;
                if (s.Contains("pregnant") || s.Contains("pregnancy"))
                    return EmergencyContext.Pregnancy;

                if (ar && ContainsAny(t, "إصابة", "حادث", "صدمة"))
                    return EmergencyContext.Trauma;
                if (s.Contains("trauma") || s.Contains("accident") || s.Contains("fall") || s.Contains("crash"))
                    return EmergencyContext.Trauma;

                if (ar && ContainsAny(t, "نوبة", "صرع"))
                    return EmergencyContext.PostSeizure;
                if (s.Contains("seizure") || s.Contains("convulsion"))
                    return EmergencyContext.PostSeizure;

                if (ar && ContainsAny(t, "لا أعلم", "مش متأكد", "غير متأكد"))
                    return EmergencyContext.Unknown;
                if (s.Contains("unknown") || s.Contains("not sure") || s.Contains("don't know") || s.Contains("dont know"))
                    return EmergencyContext.Unknown;

                if (s.Contains("standard") || s.Contains("normal") || s.Contains("typical") || s.Contains("collapse") || s.Contains("cardiac"))
                    return EmergencyContext.Standard;

                Debug.LogWarning("[EmergencyController] Context not recognized; using Standard.");
                return EmergencyContext.Standard;
            }

            public static PatientCategory ParsePatient(string t, string lang)
            {
                string s = t.ToLowerInvariant();
                bool ar = lang == "ar";

                if (ar && ContainsAny(t, "رضيع", "طفل رضيع"))
                    return PatientCategory.Infant;
                if (s.Contains("infant") || s.Contains("baby") || s.Contains("newborn"))
                    return PatientCategory.Infant;

                if (ar && ContainsAny(t, "طفل") && !ContainsAny(t, "رضيع"))
                    return PatientCategory.Child;
                if (s.Contains("child") || s.Contains("kid") || s.Contains("toddler"))
                    return PatientCategory.Child;

                if (ar && ContainsAny(t, "مراهق", "مراهقة"))
                    return PatientCategory.Teenager;
                if (s.Contains("teen"))
                    return PatientCategory.Teenager;

                if (ar && ContainsAny(t, "بالغ", "رجل", "امرأة"))
                    return PatientCategory.Adult;
                if (s.Contains("adult") || s.Contains("grown"))
                    return PatientCategory.Adult;

                Debug.LogWarning("[EmergencyController] Patient age not recognized; using Adult.");
                return PatientCategory.Adult;
            }

            public static bool ParseYesNo(string t, string lang)
            {
                string s = t.ToLowerInvariant();
                bool ar = lang == "ar";

                if (ar && ContainsAny(t, "نعم", "أجل", "ايوه", "آه"))
                    return true;
                if (ar && ContainsAny(t, "لا", "لأ"))
                    return false;

                if (s.Contains("yes") || s.Contains("yeah") || s.Contains("yep") || s.Contains("pregnant"))
                    return true;
                if (s.Contains("no") || s.Contains("not") || s.Contains("nope"))
                    return false;

                Debug.LogWarning("[EmergencyController] Yes/No not clear; assuming no.");
                return false;
            }

            public static EmergencyContext ParseCollapseOrDrowning(string t, string lang)
            {
                string s = t.ToLowerInvariant();
                bool ar = lang == "ar";
                if (ar && ContainsAny(t, "لا أعلم", "مش متأكد", "غير متأكد", "لا أدري"))
                    return EmergencyContext.Unknown;
                if (s.Contains("unknown") || s.Contains("not sure") || s.Contains("don't know") || s.Contains("dont know") || s.Contains("unclear"))
                    return EmergencyContext.Unknown;
                if (ar && ContainsAny(t, "غرق", "تغرق", "ماء"))
                    return EmergencyContext.Drowning;
                if (s.Contains("drown") || s.Contains("water") || s.Contains("pool"))
                    return EmergencyContext.Drowning;
                // Choking / airway obstruction — CPR config uses Standard; prompt mentions food/stuck throat.
                if (ar && ContainsAny(t, "يختنق", "اختناق بالطعام", "عالق بالحلق", "طعام عالق", "جسم غريب"))
                    return EmergencyContext.Standard;
                if (s.Contains("chok") || s.Contains("choke") || s.Contains("heimlich") || s.Contains("food stuck"))
                    return EmergencyContext.Standard;
                if (ar && ContainsAny(t, "انهيار", "سقط", "مغمى"))
                    return EmergencyContext.Standard;
                if (s.Contains("collapse") || s.Contains("collapsed") || s.Contains("cardiac") || s.Contains("faint"))
                    return EmergencyContext.Standard;
                return EmergencyContext.Standard;
            }

            public static bool ParseSkip(string t, string lang)
            {
                string s = t.ToLowerInvariant();
                if (lang == "ar" && ContainsAny(t, "تخطي", "تجاوز"))
                    return true;
                return s.Contains("skip") || s.Contains("pass");
            }

            static bool ContainsAny(string haystack, params string[] needles)
            {
                if (string.IsNullOrEmpty(haystack)) return false;
                foreach (var n in needles)
                {
                    if (!string.IsNullOrEmpty(n) && haystack.Contains(n))
                        return true;
                }
                return false;
            }
        }
    }
}
