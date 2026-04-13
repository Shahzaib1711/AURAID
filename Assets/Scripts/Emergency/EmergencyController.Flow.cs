using System;
using System.Collections;
using AURAID.Emergency.CPR;
using AURAID.Voice;
using UnityEngine;

namespace AURAID.Emergency
{
    public partial class EmergencyController
    {
        public enum EmergencyFlowPhase
        {
            Idle,
            OpeningLine1,
            OpeningLine2,
            AskSituation,
            AskPatient,
            AskPregnancy,
            AskResponsive,
            AskBreathingNormal,
            RecoveryPath,
            ApplyTriage,
            HandPlacementGuidance,
            CprIntro,
            CprActive,
            CprCheckpointPrompt,
            CprCheckpointListen,
            SessionEnded
        }

        [Header("CPR loop")]
        [Tooltip("Seconds between “pause and check breathing” prompts during CPR.")]
        public float cprCheckpointIntervalSec = 120f;

        EmergencyFlowPhase _flowPhase = EmergencyFlowPhase.Idle;
        EmergencyFlowPhase? _awaitingListenPhase;
        Coroutine _mainFlowCo;
        bool _flowFinished;
        bool _cprSensorFeedActive;
        bool _userResponsive;
        bool _userBreathingNormal;
        Coroutine _cprLoopCo;

        /// <summary>After smart-assist &quot;possible movement — check breathing&quot;, accept yes/no as answer before resuming CPR.</summary>
        bool _awaitingBreathingAnswerAfterMovementPrompt;
        float _movementBreathingAnswerDeadline;
        Coroutine _postBreathingEndCo;

        void ForceEmergencyCprAfterTimeout()
        {
            if (_mainFlowCo != null)
            {
                StopCoroutine(_mainFlowCo);
                _mainFlowCo = null;
            }
            StopPostTtsListenCoroutine();
            _flowFinished = true;
            isConfigured = true;
            emergencyInput?.ApplyDefaults();
            EmergencySessionManager.Instance?.LogResolvedFromAgent(cprAgent);

            PatientCategory cat = cprAgent != null && cprAgent.config != null
                ? cprAgent.config.patientCategory
                : PatientCategory.Adult;

            if (_cprLoopCo != null)
                StopCoroutine(_cprLoopCo);

            if (CanPlayEmergencyTts && voice != null && voice.tts != null)
                StartCoroutine(TimeoutPathHandPlacementThenCprCoroutine(cat));
            else
                ActivateCprSensorsAndCheckpointLoop();
        }

        void ActivateCprSensorsAndCheckpointLoop()
        {
            _cprSensorFeedActive = true;
            // Eligible for first "Good compressions" TTS as soon as quality is Good (not a full interval after start).
            _lastGoodCprEncouragementVoice = Time.time - goodCprEncouragementVoiceIntervalSec;
            if (_cprLoopCo != null)
                StopCoroutine(_cprLoopCo);
            _cprLoopCo = StartCoroutine(CprCheckpointLoopCoroutine());
        }

        /// <summary>Timeout path: same hand-placement TTS as main flow, then sensors + checkpoint loop.</summary>
        IEnumerator TimeoutPathHandPlacementThenCprCoroutine(PatientCategory category)
        {
            bool isAr = ttsLanguage == "ar";
            foreach (string line in GetHandPlacementLines(isAr, category))
                yield return SpeakLine(line);
            yield return SpeakLine(isAr
                ? "ابق معي. ابدأ الضغط على الصدر الآن. بقوة وبسرعة."
                : "Stay with me. Start chest compressions now. Push hard and fast.");
            ActivateCprSensorsAndCheckpointLoop();
        }

        /// <summary>True while FSR/BNO samples should drive compression feedback.</summary>
        public bool IsCprSensorFeedActive => _cprSensorFeedActive;

        /// <summary>Legacy compatibility name for the CPR sensor gate.</summary>
        public bool IsAgentReadyForSensors => IsCprSensorFeedActive;

        /// <summary>Stop CPR sensors, checkpoint loop, and session when user reports breathing anytime during CPR.</summary>
        internal void EndCprBecauseUserReportsBreathing()
        {
            _awaitingBreathingAnswerAfterMovementPrompt = false;
            _cprSensorFeedActive = false;
            StopCprLoopCoroutine();
            _flowPhase = EmergencyFlowPhase.SessionEnded;
            if (_postBreathingEndCo != null)
            {
                StopCoroutine(_postBreathingEndCo);
                _postBreathingEndCo = null;
            }
            _postBreathingEndCo = StartCoroutine(CoNarrateEndCprBecauseBreathingConfirmed());
        }

        IEnumerator CoNarrateEndCprBecauseBreathingConfirmed()
        {
            bool isAr = ttsLanguage == "ar";
            SetFeedback(isAr
                ? "تم التأكيد: يتنفس. توقف عن الضغطات وراقب المصاب."
                : "Noted as breathing — stop compressions and monitor the person.");
            yield return SpeakLine(isAr
                ? "توقف عن الضغطات. راقب الشخص حتى وصول المساعدة."
                : "Stop compressions. Monitor the person until help arrives.");
            yield return SpeakLine(isAr
                ? "إذا كان التنفس منتظماً، ضعه في وضعية الاستلقاء الجانبي إن كنت مدرباً على ذلك. أبقِ المجرى الهوائي مفتوحاً وابقَ بجانبه."
                : "If breathing is steady, place them in the recovery position if you are trained. Keep the airway open and stay with them.");
            yield return SpeakLine(isAr
                ? "ابقَ على خط الطوارئ إن أمكن، واستمر بمراقبة التنفس والاستجابة."
                : "Stay on the line with emergency services if you can, and keep watching their breathing and responsiveness.");
            EmergencySessionManager.Instance?.EndSession();
            _postBreathingEndCo = null;
        }

        /// <summary>
        /// Voice flow: opening → triage (what happened, age, pregnancy) → responsive → breathing check
        /// → if safe, recovery and end → else apply triage → hand placement (where/how) → start compressions
        /// and enable FSR/BNO real-time feedback → 2 min checkpoints.
        /// </summary>
        IEnumerator MainEmergencyFlowCoroutine()
        {
            TryAutoWireVoiceFromScene();

            _flowFinished = false;
            _cprSensorFeedActive = false;

            bool isAr = ttsLanguage == "ar";
            if (!useVoiceScenarioIntake || !CanPlayEmergencyTts)
            {
                _flowFinished = true;
                SetFeedback(isAr ? "وضع الطوارئ — الصوت غير متاح." : "Emergency mode — voice unavailable; waiting for timeout or UI.");
                yield break;
            }

            // Stuck fallback only after we know TTS can run — avoids the old bug where OnEnable started a short timer
            // that stopped this coroutine before triage questions finished.
            _emergencyFlowCo = StartCoroutine(ScenarioFallbackTimeoutCoroutine());

            yield return new WaitForSecondsRealtime(0.55f);

            // 1) Start
            yield return SpeakLine(isAr
                ? "تم تفعيل وضع الطوارئ. حافظ على هدوئك. سأرشدك خطوة بخطوة."
                : "Emergency mode activated. Stay calm. I will guide you.");
            yield return SpeakLine(isAr
                ? "اتصل بخدمات الطوارئ إن لم تكن قد فعلت."
                : "Call emergency services if you have not already.");

            // 2) Quick triage — situation first, then age, then pregnancy (when adult)
            _flowPhase = EmergencyFlowPhase.AskSituation;
            PromptSituationWhatHappened();
            yield return WaitForTranscriptPhase(EmergencyFlowPhase.AskSituation);

            _flowPhase = EmergencyFlowPhase.AskPatient;
            PromptPatientQuestionNew();
            yield return WaitForTranscriptPhase(EmergencyFlowPhase.AskPatient);

            if (selectedContext == EmergencyContext.Unknown)
            {
                yield return SpeakLine(isAr
                    ? "فهمت. سنمضي بحذر وبأمان."
                    : "I understand. We will proceed safely.");
            }

            _flowPhase = EmergencyFlowPhase.AskPregnancy;
            if (selectedCategory == PatientCategory.Adult)
            {
                PromptPregnancyOptional();
                yield return WaitForTranscriptPhase(EmergencyFlowPhase.AskPregnancy);
            }
            else
            {
                isPregnant = false;
            }

            // 3) Initial check
            _flowPhase = EmergencyFlowPhase.AskResponsive;
            PromptResponsive();
            yield return WaitForTranscriptPhase(EmergencyFlowPhase.AskResponsive);

            _flowPhase = EmergencyFlowPhase.AskBreathingNormal;
            PromptBreathingNormal();
            yield return WaitForTranscriptPhase(EmergencyFlowPhase.AskBreathingNormal);

            // 4) Decision
            if (_userResponsive || _userBreathingNormal)
            {
                _flowPhase = EmergencyFlowPhase.RecoveryPath;
                yield return SpeakLine(isAr
                    ? "ضع الشخص في وضعية الاستلقاء الجانبي وراقب التنفس. لا حاجة للضغطات الآن."
                    : "Place the person in the recovery position and monitor breathing. No chest compressions needed now.");
                StopEmergencyScenarioFallbackCoroutineIfAny();
                _flowFinished = true;
                isConfigured = true;
                EmergencySessionManager.Instance?.EndSession();
                yield break;
            }

            // 5) Triage → agent config (depth / context). Sensors stay off.
            _flowPhase = EmergencyFlowPhase.ApplyTriage;
            ApplyTriageToEmergencyInput();

            // 6) Hand placement (where / how) — voice only, before FSR/BNO feedback
            yield return SpeakHandPlacementGuidance(isAr);

            // 7) Start compressions + real-time sensor feedback
            _flowPhase = EmergencyFlowPhase.CprIntro;
            yield return SpeakLine(isAr
                ? "ابق معي. سأوجّهك أثناء الضغط حسب المستشعرات."
                : "Stay with me. I will guide you during compressions using the sensors.");
            yield return SpeakLine(isAr
                ? "ابدأ الضغط على الصدر الآن. بقوة وبسرعة."
                : "Start chest compressions now. Push hard and fast.");

            _flowPhase = EmergencyFlowPhase.CprActive;
            _cprSensorFeedActive = true;
            _lastGoodCprEncouragementVoice = Time.time - goodCprEncouragementVoiceIntervalSec;
            _sensorFaultActive = false;
            _lastSensorSampleAt = Time.time;
            _lastDegradedVoiceAt = -999f;
            isConfigured = true;
            EmergencySessionManager.Instance?.LogResolvedFromAgent(cprAgent);
            StopEmergencyScenarioFallbackCoroutineIfAny();

            if (_cprLoopCo != null)
                StopCoroutine(_cprLoopCo);
            _cprLoopCo = StartCoroutine(CprCheckpointLoopCoroutine());

            _flowFinished = true;
        }

        IEnumerator CprCheckpointLoopCoroutine()
        {
            bool isAr = ttsLanguage == "ar";
            while (enabled && _cprSensorFeedActive)
            {
                float elapsed = 0f;
                while (elapsed < cprCheckpointIntervalSec && enabled && _cprSensorFeedActive)
                {
                    TickSensorFailSafeDuringCpr(isAr);
                    elapsed += Time.deltaTime;
                    yield return null;
                }
                if (!_cprSensorFeedActive || !enabled) yield break;

                _cprSensorFeedActive = false;
                _flowPhase = EmergencyFlowPhase.CprCheckpointPrompt;
                yield return SpeakLine(isAr ? "توقف قليلاً. تحقق من التنفس." : "Pause. Check for breathing.");
                yield return SpeakLine(isAr ? "هل يتنفس الشخص؟ قل نعم أو لا." : "Is the person breathing? Say yes or no.");

                _flowPhase = EmergencyFlowPhase.CprCheckpointListen;
                _awaitingListenPhase = EmergencyFlowPhase.CprCheckpointListen;
                ScheduleListenAfterTtsFlow(EmergencyFlowPhase.CprCheckpointListen);

                float waited = 0f;
                while (_awaitingListenPhase != null && waited < listenDurationSec + 25f)
                {
                    waited += Time.deltaTime;
                    yield return null;
                }

                if (_userBreathingNormal)
                {
                    _cprSensorFeedActive = false;
                    yield return SpeakLine(isAr
                        ? "توقف عن الضغطات. راقب الشخص حتى وصول المساعدة."
                        : "Stop compressions. Monitor the person until help arrives.");
                    yield return SpeakLine(isAr
                        ? "إذا كان التنفس منتظماً، ضعه في وضعية الاستلقاء الجانبي إن كنت مدرباً على ذلك. أبقِ المجرى الهوائي مفتوحاً."
                        : "If breathing is steady, place them in the recovery position if trained, and keep the airway open.");
                    yield return SpeakLine(isAr
                        ? "ابقَ مع المصاب واستدعِ المساعدة إن لم تكن قد فعلت. يمكنك البقاء على خط الطوارئ."
                        : "Stay with them and call for help if you have not already. You can stay on the line with dispatch.");
                    EmergencySessionManager.Instance?.EndSession();
                    yield break;
                }

                yield return SpeakLine(isAr ? "تابع الضغطات." : "Continue compressions.");
                _cprSensorFeedActive = true;
                _lastGoodCprEncouragementVoice = Time.time - goodCprEncouragementVoiceIntervalSec;
                _flowPhase = EmergencyFlowPhase.CprActive;
            }
        }

        void PromptPatientQuestionNew()
        {
            string q = ttsLanguage == "ar"
                ? "هل المصاب بالغ أم طفل أم رضيع؟"
                : "Is the person an adult, a child, or an infant?";
            _awaitingListenPhase = EmergencyFlowPhase.AskPatient;
            voice.AskUser(q, ttsLanguage);
            ScheduleListenAfterTtsFlow(EmergencyFlowPhase.AskPatient);
        }

        void PromptSituationWhatHappened()
        {
            string q = ttsLanguage == "ar"
                ? "ماذا حدث؟ يمكنك قول انهيار، أو غرق، أو اختناق بالطعام، أو أنك غير متأكد."
                : "What happened? You can say collapse, drowning, choking, or that you are not sure.";
            _awaitingListenPhase = EmergencyFlowPhase.AskSituation;
            voice.AskUser(q, ttsLanguage);
            ScheduleListenAfterTtsFlow(EmergencyFlowPhase.AskSituation);
        }

        void PromptPregnancyOptional()
        {
            string q = ttsLanguage == "ar"
                ? "هل المصابة حامل؟ قل نعم أو لا أو تخطي."
                : "Is the person pregnant? Say yes, no, or skip.";
            _awaitingListenPhase = EmergencyFlowPhase.AskPregnancy;
            voice.AskUser(q, ttsLanguage);
            ScheduleListenAfterTtsFlow(EmergencyFlowPhase.AskPregnancy);
        }

        void PromptResponsive()
        {
            string q = ttsLanguage == "ar"
                ? "هل المصاب مستجيب؟ قل نعم أو لا."
                : "Is the person responsive? Say yes or no.";
            _awaitingListenPhase = EmergencyFlowPhase.AskResponsive;
            voice.AskUser(q, ttsLanguage);
            ScheduleListenAfterTtsFlow(EmergencyFlowPhase.AskResponsive);
        }

        void PromptBreathingNormal()
        {
            string q = ttsLanguage == "ar"
                ? "هل يتنفس بشكل طبيعي؟ قل نعم أو لا."
                : "Is the person breathing normally? Say yes or no.";
            _awaitingListenPhase = EmergencyFlowPhase.AskBreathingNormal;
            voice.AskUser(q, ttsLanguage);
            ScheduleListenAfterTtsFlow(EmergencyFlowPhase.AskBreathingNormal);
        }

        IEnumerator SpeakLine(string line)
        {
            if (voice == null || voice.tts == null) yield break;
            voice.AskUser(line, ttsLanguage);
            yield return voice.WaitUntilSpeechPipelineIdle();
            yield return new WaitForSeconds(pauseAfterTtsBeforeListenSec);
        }

        IEnumerator WaitForTranscriptPhase(EmergencyFlowPhase phase)
        {
            float timeout = listenDurationSec + 30f;
            float t = 0f;
            while (_awaitingListenPhase == phase && t < timeout)
            {
                t += Time.deltaTime;
                yield return null;
            }
            if (_awaitingListenPhase == phase)
            {
                ApplySafeDefaultForPhase(phase);
                _awaitingListenPhase = null;
                Debug.LogWarning("[EmergencyController] Voice answer timeout on " + phase + " — using safe default.");
            }
        }

        void ApplySafeDefaultForPhase(EmergencyFlowPhase phase)
        {
            switch (phase)
            {
                case EmergencyFlowPhase.AskPatient:
                    selectedCategory = PatientCategory.Adult;
                    break;
                case EmergencyFlowPhase.AskSituation:
                    selectedContext = EmergencyContext.Standard;
                    break;
                case EmergencyFlowPhase.AskPregnancy:
                    isPregnant = false;
                    break;
                case EmergencyFlowPhase.AskResponsive:
                    _userResponsive = false;
                    break;
                case EmergencyFlowPhase.AskBreathingNormal:
                    _userBreathingNormal = false;
                    break;
                case EmergencyFlowPhase.CprCheckpointListen:
                    _userBreathingNormal = false;
                    break;
            }
        }

        /// <summary>
        /// When STT is unavailable, assume worst-case for emergency (unresponsive, not breathing normally) and advance the flow.
        /// </summary>
        void ApplySttFallbackAndAdvance(EmergencyFlowPhase phase)
        {
            switch (phase)
            {
                case EmergencyFlowPhase.AskPatient:
                    selectedCategory = PatientCategory.Adult;
                    break;
                case EmergencyFlowPhase.AskSituation:
                    selectedContext = EmergencyContext.Standard;
                    break;
                case EmergencyFlowPhase.AskPregnancy:
                    isPregnant = false;
                    break;
                case EmergencyFlowPhase.AskResponsive:
                    _userResponsive = false;
                    break;
                case EmergencyFlowPhase.AskBreathingNormal:
                    _userBreathingNormal = false;
                    break;
                case EmergencyFlowPhase.CprCheckpointListen:
                    _userBreathingNormal = false;
                    break;
                default:
                    return;
            }

            _awaitingListenPhase = null;
        }

        void ScheduleListenAfterTtsFlow(EmergencyFlowPhase phase)
        {
            StopPostTtsListenCoroutine();
            _postTtsListenCo = StartCoroutine(ListenAfterTtsFlowCoroutine(phase));
        }

        IEnumerator ListenAfterTtsFlowCoroutine(EmergencyFlowPhase expectedPhase)
        {
            if (voice != null)
                yield return voice.WaitUntilSpeechPipelineIdle();
            else
                yield return new WaitForSeconds(1.2f);

            yield return new WaitForSeconds(pauseAfterTtsBeforeListenSec);

            if (!isActiveAndEnabled || _awaitingListenPhase != expectedPhase)
            {
                _postTtsListenCo = null;
                yield break;
            }

            if (!CanListenStt)
            {
                Debug.LogWarning("[EmergencyController] STT unavailable for phase " + expectedPhase + " — advancing with safe worst-case defaults.");
                ApplySttFallbackAndAdvance(expectedPhase);
                _postTtsListenCo = null;
                yield break;
            }

            voice.ListenAndTranscribe(listenDurationSec);
            _postTtsListenCo = null;
        }

        void ProcessFlowTranscript(string t)
        {
            inputReceived = true;
            bool isAr = ttsLanguage == "ar";
            SetFeedback(isAr ? "قال المستخدم: " + t : "You said: " + t);

            if (_awaitingListenPhase == EmergencyFlowPhase.AskPatient)
            {
                selectedCategory = VoiceScenarioParser.ParsePatient(t, ttsLanguage);
                EmergencySessionManager.Instance?.SaveIntakeExchange("patient_category", t, selectedCategory.ToString());
                _awaitingListenPhase = null;
                return;
            }

            if (_awaitingListenPhase == EmergencyFlowPhase.AskSituation)
            {
                selectedContext = VoiceScenarioParser.ParseCollapseOrDrowning(t, ttsLanguage);
                EmergencySessionManager.Instance?.SaveIntakeExchange("what_happened", t, selectedContext.ToString());
                _awaitingListenPhase = null;
                return;
            }

            if (_awaitingListenPhase == EmergencyFlowPhase.AskPregnancy)
            {
                if (VoiceScenarioParser.ParseSkip(t, ttsLanguage))
                    isPregnant = false;
                else
                    isPregnant = VoiceScenarioParser.ParseYesNo(t, ttsLanguage);
                EmergencySessionManager.Instance?.SaveIntakeExchange("pregnancy", t, isPregnant.ToString());
                _awaitingListenPhase = null;
                return;
            }

            if (_awaitingListenPhase == EmergencyFlowPhase.AskResponsive)
            {
                _userResponsive = VoiceScenarioParser.ParseYesNo(t, ttsLanguage);
                EmergencySessionManager.Instance?.SaveIntakeExchange("responsive", t, _userResponsive.ToString());
                _awaitingListenPhase = null;
                return;
            }

            if (_awaitingListenPhase == EmergencyFlowPhase.AskBreathingNormal)
            {
                _userBreathingNormal = VoiceScenarioParser.ParseYesNo(t, ttsLanguage) || UserSaidBreathingKeyword(t);
                EmergencySessionManager.Instance?.SaveIntakeExchange("breathing_normal", t, _userBreathingNormal.ToString());
                _awaitingListenPhase = null;
                return;
            }

            if (_awaitingListenPhase == EmergencyFlowPhase.CprCheckpointListen)
            {
                _userBreathingNormal = VoiceScenarioParser.ParseYesNo(t, ttsLanguage) || UserSaidBreathingKeyword(t);
                EmergencySessionManager.Instance?.SaveIntakeExchange("checkpoint_breathing", t, _userBreathingNormal.ToString());
                _awaitingListenPhase = null;
                return;
            }
        }

        internal void ApplyTriageToEmergencyInput()
        {
            if (emergencyInput == null)
            {
                Debug.LogWarning("[EmergencyController] EmergencyInputManager missing.");
                return;
            }
            if (isPregnant && selectedCategory == PatientCategory.Adult)
                selectedContext = EmergencyContext.Pregnancy;
            emergencyInput.SetContext(selectedContext);
            emergencyInput.SetPatientCategory(selectedCategory);
            emergencyInput.ApplyToAgent();
        }

        IEnumerator SpeakHandPlacementGuidance(bool isAr)
        {
            _flowPhase = EmergencyFlowPhase.HandPlacementGuidance;
            foreach (string line in GetHandPlacementLines(isAr, selectedCategory))
                yield return SpeakLine(line);
        }

        /// <summary>Two short lines: kneel / surface, then hand position (category-specific).</summary>
        static string[] GetHandPlacementLines(bool isAr, PatientCategory category)
        {
            switch (category)
            {
                case PatientCategory.Infant:
                    return isAr
                        ? new[]
                        {
                            "ضع الرضيع على سطح صلب ومستوٍ.",
                            "استخدم إصبعين على وسط الصدر على عضمة القص أسفل خط الحلمة. اضغط بسرعة وبثبات.",
                        }
                        : new[]
                        {
                            "Place the infant on a firm, flat surface.",
                            "Use two fingers on the center of the chest on the breastbone, just below the nipple line. Push fast and steady.",
                        };
                case PatientCategory.Child:
                    return isAr
                        ? new[]
                        {
                            "اجلس بجانب الطفل. ضع عقب يدك على وسط الصدر على عضمة القص.",
                            "اضغط بعمق مستقيم. استخدم يداً واحدة أو اثنتين حسب الحجم. أبقِ ذراعيك مستقيمتين.",
                        }
                        : new[]
                        {
                            "Kneel beside the child. Place the heel of one hand in the center of the chest on the breastbone.",
                            "Push straight down. Use one or two hands as needed. Keep your arms straight and shoulders over your hands.",
                        };
                default:
                    return isAr
                        ? new[]
                        {
                            "اجلس بجانب المصاب. ضع عقب يدك على وسط الصدر — النصف السفلي من عضمة القص.",
                            "ضع يدك الأخرى فوقها واشبك أصابعك. أبقِ ذراعيك مستقيمتين وكتفيك فوق يديك مباشرة.",
                        }
                        : new[]
                        {
                            "Kneel beside the person. Place the heel of one hand on the center of the chest — the lower half of the breastbone.",
                            "Put your other hand on top and interlock your fingers. Keep your arms straight with your shoulders directly over your hands.",
                        };
            }
        }

        static bool TranscriptContainsAny(string haystack, params string[] needles)
        {
            if (string.IsNullOrEmpty(haystack)) return false;
            foreach (var n in needles)
            {
                if (string.IsNullOrEmpty(n)) continue;
                if (haystack.IndexOf(n, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }

        /// <summary>For the movement smart-assist follow-up: treat as &quot;not breathing / resume CPR&quot; without catching vague phrases.</summary>
        bool MovementBreathingAnswerSoundsLikeNo(string t)
        {
            if (string.IsNullOrWhiteSpace(t)) return false;
            if (ttsLanguage == "ar")
            {
                if (TranscriptContainsAny(t, "لا يتنفس", "ما يتنفس", "ما في تنفس", "مافيش تنفس", "لا تنفس", "ما بيتنفس"))
                    return true;
                if (TranscriptContainsAny(t, "نعم", "أجل", "يتنفس", "بيتنفس", "يتنفسون"))
                    return false;
                return TranscriptContainsAny(t, "لا", "لأ");
            }
            string s = t.ToLowerInvariant();
            if (s.Contains("not breathing") || s.Contains("isn't breathing") || s.Contains("isnt breathing") ||
                s.Contains("still not breathing") || s.Contains("non breathing"))
                return true;
            if (s.Contains("yes") || s.Contains("yeah") || s.Contains("yep")) return false;
            if (UserSaidBreathingKeyword(t)) return false;
            return s.Contains("no") || s.Contains("nope");
        }
    }
}
